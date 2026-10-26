using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.RouteFinances;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.Loans;
using MS.Cobroup.Backend.Domain.Payments;
using MS.Cobroup.Backend.Domain.RouteFinances;

namespace MS.Cobroup.Backend.Core.Payments;

public sealed class PaymentService(
    IPaymentRepository paymentRepository,
    ILoanRepository loanRepository,
    IAdministrativeRouteDirectory routeDirectory,
    IOperationalPaymentMethodDirectory paymentMethodDirectory,
    MS.Cobroup.Backend.Domain.Interfaces.Services.IFileStorageService fileStorageService,
    IRouteFinanceRepository financeRepository)
{
    private static readonly HashSet<string> AllowedMimeTypes =
    [
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    ];

    public async Task<PaymentResult> RegisterPaymentAsync(
        CustomerActor actor,
        RegisterPaymentInput input,
        string accessToken,
        CancellationToken cancellationToken)
    {
        // 1. Idempotency Check (AC-12)
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey))
        {
            return PaymentResult.Failure("La clave de idempotencia es requerida.", PaymentErrorType.ValidationError);
        }

        var existing = await paymentRepository.FindByIdempotencyKeyAsync(actor.TenantId, input.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null)
        {
            var existingLoan = await loanRepository.FindAsync(actor.TenantId, existing.LoanId, cancellationToken);
            var balance = existingLoan?.Terms.BalanceAmount ?? 0m;
            var status = existingLoan?.Status ?? LoanStatus.Active;
            return PaymentResult.Ok(PaymentDetails.FromDomain(existing, balance, status));
        }

        // 2. Validate Loan Existence and Access Scope (AC-01, AC-06)
        var loan = await loanRepository.FindAsync(actor.TenantId, input.LoanId, cancellationToken);
        if (loan is null)
        {
            return PaymentResult.Failure("Préstamo no encontrado.", PaymentErrorType.NotFound);
        }

        var routes = await routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return PaymentResult.Failure("No fue posible validar las rutas con la autoridad administrativa.", PaymentErrorType.Conflict);
        }

        var accessibleRoute = routes.Routes.FirstOrDefault(r => r.Id == loan.RouteId);
        if (accessibleRoute is null)
        {
            return PaymentResult.Failure("No tiene autorización para registrar cobros en esta ruta.", PaymentErrorType.Forbidden);
        }

        // Check Day Closing and Route Timezone
        var routeTz = RouteFinanceService.ResolveBusinessTimeZone(accessibleRoute.TimeZoneId);
        var nowUtc = DateTimeOffset.UtcNow;
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, routeTz);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);

        var dayClosing = await financeRepository.FindDayClosingAsync(actor.TenantId, loan.RouteId, localDate, cancellationToken);
        if (dayClosing?.IsClosed ?? false)
        {
            return PaymentResult.Failure("El día se encuentra cerrado para esta ruta. No es posible registrar nuevos cobros.", PaymentErrorType.Conflict);
        }

        // Check 1 Collection per customer per day in route
        var startOfDayUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date, routeTz);
        var endOfDayUtc = TimeZoneInfo.ConvertTimeToUtc(localNow.Date.AddDays(1).AddTicks(-1), routeTz);
        var existingToday = await paymentRepository.FindTodayPaymentAsync(actor.TenantId, loan.CustomerId, loan.RouteId, startOfDayUtc, endOfDayUtc, cancellationToken);
        if (existingToday is not null && existingToday.IdempotencyKey != input.IdempotencyKey.Trim())
        {
            return PaymentResult.Failure("El cliente ya tiene un cobro registrado hoy en esta ruta. Para realizar ajustes, edita el cobro existente.", PaymentErrorType.Conflict);
        }

        if (loan.Status is not LoanStatus.Active || loan.Terms.BalanceAmount <= 0)
        {
            return PaymentResult.Failure("El préstamo ya no se encuentra activo o su saldo es cero.", PaymentErrorType.LoanClosed);
        }

        // 3. Validate Amount
        if (input.Amount <= 0)
        {
            return PaymentResult.Failure("El importe del cobro debe ser positivo.", PaymentErrorType.ValidationError);
        }

        if (input.Amount > loan.Terms.BalanceAmount)
        {
            return PaymentResult.Failure(
                $"El importe del cobro ({input.Amount:N2}) supera el saldo pendiente del préstamo ({loan.Terms.BalanceAmount:N2}).",
                PaymentErrorType.AmountExceedsBalance);
        }

        // 4. Validate Payment Method (AC-13)
        var methodsResult = await paymentMethodDirectory.ListAvailableAsync(accessToken, cancellationToken);
        if (methodsResult.Outcome is not PaymentMethodDirectoryOutcome.Success)
        {
            return PaymentResult.Failure("No fue posible validar los medios de pago con la autoridad administrativa.", PaymentErrorType.Conflict);
        }

        var matchedMethod = methodsResult.Items.FirstOrDefault(m => m.Id == input.PaymentMethodId);
        if (matchedMethod is null)
        {
            return PaymentResult.Failure("El medio de pago seleccionado no está disponible o no pertenece a la organización.", PaymentErrorType.InvalidPaymentMethod);
        }

        // 5. Cascade Allocation onto Installments (AC-11)
        var pendingInstallments = loan.Installments
            .Where(i => i.Status is not InstallmentStatus.Paid)
            .OrderBy(i => i.Number)
            .ToList();

        var paymentId = Guid.NewGuid();
        var remainingToAllocate = input.Amount;
        var allocations = new List<PaymentAllocation>();

        foreach (var installment in pendingInstallments)
        {
            if (remainingToAllocate <= 0)
            {
                break;
            }

            var needed = installment.ExpectedAmount - installment.PaidAmount;
            var allocAmount = Math.Min(remainingToAllocate, needed);

            installment.ApplyPayment(allocAmount);
            var isFullyCovered = installment.Status is InstallmentStatus.Paid;

            allocations.Add(new PaymentAllocation(
                Guid.NewGuid(),
                paymentId,
                installment.Id,
                installment.Number,
                allocAmount,
                isFullyCovered));

            remainingToAllocate -= allocAmount;
        }

        loan.ApplyPayment(input.Amount);

        var payment = Payment.Register(
            actor.TenantId,
            loan.Id,
            loan.CustomerId,
            loan.RouteId,
            actor.UserId,
            input.Amount,
            matchedMethod.Id,
            matchedMethod.Name,
            input.Notes,
            input.IdempotencyKey,
            nowUtc,
            allocations);

        var saveOutcome = await paymentRepository.TryRegisterPaymentAsync(payment, loan, cancellationToken);
        if (saveOutcome is PaymentSaveOutcome.IdempotentReplay)
        {
            var replayed = await paymentRepository.FindByIdempotencyKeyAsync(actor.TenantId, input.IdempotencyKey.Trim(), cancellationToken);
            if (replayed is not null)
            {
                return PaymentResult.Ok(PaymentDetails.FromDomain(replayed, loan.Terms.BalanceAmount, loan.Status));
            }
        }
        else if (saveOutcome is PaymentSaveOutcome.ConcurrencyConflict)
        {
            return PaymentResult.Failure("El cobro no pudo completarse debido a una actualización concurrente en el saldo del préstamo. Por favor, reintente la operación.", PaymentErrorType.Conflict);
        }

        var audit = FinancialMovementAuditLog.Create(
            actor.TenantId,
            loan.RouteId,
            FinancialMovementEntityType.Payment,
            payment.Id,
            FinancialMovementAuditAction.Created,
            $"Cobro de préstamo",
            payment.Amount,
            $"Cobro registrado por valor de {payment.Amount:N2}. Medio: {matchedMethod.Name}",
            actor.UserId,
            actor.UserId.ToString("D"),
            actor.Role.ToString(),
            nowUtc);
        await financeRepository.AddAuditLogAsync(audit, cancellationToken);
        await financeRepository.SaveChangesAsync(cancellationToken);

        return PaymentResult.Ok(PaymentDetails.FromDomain(payment, loan.Terms.BalanceAmount, loan.Status));
    }

    public async Task<PaymentResult> UpdatePaymentAsync(
        CustomerActor actor,
        Guid paymentId,
        UpdatePaymentInput input,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.FindAsync(actor.TenantId, paymentId, cancellationToken);
        if (payment is null)
        {
            return PaymentResult.Failure("Cobro no encontrado.", PaymentErrorType.NotFound);
        }

        var loan = await loanRepository.FindAsync(actor.TenantId, payment.LoanId, cancellationToken);
        if (loan is null)
        {
            return PaymentResult.Failure("Préstamo no encontrado.", PaymentErrorType.NotFound);
        }

        var routes = await routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return PaymentResult.Failure("No fue posible validar las rutas con la autoridad administrativa.", PaymentErrorType.Conflict);
        }

        var accessibleRoute = routes.Routes.FirstOrDefault(r => r.Id == payment.RouteId);
        if (accessibleRoute is null)
        {
            return PaymentResult.Failure("No tiene autorización para operar en esta ruta.", PaymentErrorType.Forbidden);
        }

        var routeTz = RouteFinanceService.ResolveBusinessTimeZone(accessibleRoute.TimeZoneId);
        var nowUtc = DateTimeOffset.UtcNow;
        var localNow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, routeTz).DateTime);
        var paymentDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(payment.RegisteredAtUtc, routeTz).DateTime);

        if (paymentDate != localNow)
        {
            return PaymentResult.Failure("Solo se pueden modificar cobros registrados dentro del mismo día.", PaymentErrorType.ValidationError);
        }

        var dayClosing = await financeRepository.FindDayClosingAsync(actor.TenantId, payment.RouteId, localNow, cancellationToken);
        if (dayClosing?.IsClosed ?? false)
        {
            return PaymentResult.Failure("El día se encuentra cerrado para esta ruta. No es posible modificar cobros.", PaymentErrorType.Conflict);
        }

        if (input.Amount <= 0)
        {
            return PaymentResult.Failure("El importe del cobro debe ser positivo.", PaymentErrorType.ValidationError);
        }

        var methodsResult = await paymentMethodDirectory.ListAvailableAsync(accessToken, cancellationToken);
        if (methodsResult.Outcome is not PaymentMethodDirectoryOutcome.Success)
        {
            return PaymentResult.Failure("No fue posible validar los medios de pago con la autoridad administrativa.", PaymentErrorType.Conflict);
        }

        var matchedMethod = methodsResult.Items.FirstOrDefault(m => m.Id == input.PaymentMethodId);
        if (matchedMethod is null)
        {
            return PaymentResult.Failure("El medio de pago seleccionado no es válido.", PaymentErrorType.InvalidPaymentMethod);
        }

        // Rollback current allocations and payment from loan
        foreach (var alloc in payment.Allocations)
        {
            var inst = loan.Installments.FirstOrDefault(i => i.Id == alloc.InstallmentId);
            inst?.RollbackPayment(alloc.AmountAllocated);
        }
        loan.RollbackPayment(payment.Amount);

        if (input.Amount > loan.Terms.BalanceAmount)
        {
            // Restore original
            foreach (var alloc in payment.Allocations)
            {
                var inst = loan.Installments.FirstOrDefault(i => i.Id == alloc.InstallmentId);
                inst?.ApplyPayment(alloc.AmountAllocated);
            }
            loan.ApplyPayment(payment.Amount);
            return PaymentResult.Failure(
                $"El importe del cobro ({input.Amount:N2}) supera el saldo pendiente del préstamo ({loan.Terms.BalanceAmount:N2}).",
                PaymentErrorType.AmountExceedsBalance);
        }

        // Apply new allocations
        var pendingInstallments = loan.Installments
            .Where(i => i.Status is not InstallmentStatus.Paid)
            .OrderBy(i => i.Number)
            .ToList();

        var remainingToAllocate = input.Amount;
        var newAllocations = new List<PaymentAllocation>();

        foreach (var installment in pendingInstallments)
        {
            if (remainingToAllocate <= 0) break;
            var needed = installment.ExpectedAmount - installment.PaidAmount;
            var allocAmount = Math.Min(remainingToAllocate, needed);
            installment.ApplyPayment(allocAmount);
            var isFullyCovered = installment.Status is InstallmentStatus.Paid;

            newAllocations.Add(new PaymentAllocation(
                Guid.NewGuid(),
                payment.Id,
                installment.Id,
                installment.Number,
                allocAmount,
                isFullyCovered));

            remainingToAllocate -= allocAmount;
        }

        loan.ApplyPayment(input.Amount);
        var prevAmount = payment.Amount;
        payment.Update(input.Amount, matchedMethod.Id, matchedMethod.Name, input.Notes, newAllocations);

        var audit = FinancialMovementAuditLog.Create(
            actor.TenantId,
            loan.RouteId,
            FinancialMovementEntityType.Payment,
            payment.Id,
            FinancialMovementAuditAction.Updated,
            $"Modificación cobro de préstamo",
            payment.Amount,
            $"Cobro modificado. Monto anterior: {prevAmount:N2}, nuevo monto: {payment.Amount:N2}. Medio: {matchedMethod.Name}",
            actor.UserId,
            actor.UserId.ToString("D"),
            actor.Role.ToString(),
            nowUtc);
        await financeRepository.AddAuditLogAsync(audit, cancellationToken);
        await paymentRepository.SaveChangesAsync(cancellationToken);

        return PaymentResult.Ok(PaymentDetails.FromDomain(payment, loan.Terms.BalanceAmount, loan.Status));
    }

    public async Task<PaymentResult> DeletePaymentAsync(
        CustomerActor actor,
        Guid paymentId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.FindAsync(actor.TenantId, paymentId, cancellationToken);
        if (payment is null)
        {
            return PaymentResult.Failure("Cobro no encontrado.", PaymentErrorType.NotFound);
        }

        var loan = await loanRepository.FindAsync(actor.TenantId, payment.LoanId, cancellationToken);
        if (loan is null)
        {
            return PaymentResult.Failure("Préstamo no encontrado.", PaymentErrorType.NotFound);
        }

        var routes = await routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return PaymentResult.Failure("No fue posible validar las rutas con la autoridad administrativa.", PaymentErrorType.Conflict);
        }

        var accessibleRoute = routes.Routes.FirstOrDefault(r => r.Id == payment.RouteId);
        if (accessibleRoute is null)
        {
            return PaymentResult.Failure("No tiene autorización para operar en esta ruta.", PaymentErrorType.Forbidden);
        }

        var routeTz = RouteFinanceService.ResolveBusinessTimeZone(accessibleRoute.TimeZoneId);
        var nowUtc = DateTimeOffset.UtcNow;
        var localNow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, routeTz).DateTime);
        var paymentDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(payment.RegisteredAtUtc, routeTz).DateTime);

        if (paymentDate != localNow)
        {
            return PaymentResult.Failure("Solo se pueden eliminar cobros registrados dentro del mismo día.", PaymentErrorType.ValidationError);
        }

        var dayClosing = await financeRepository.FindDayClosingAsync(actor.TenantId, payment.RouteId, localNow, cancellationToken);
        if (dayClosing?.IsClosed ?? false)
        {
            return PaymentResult.Failure("El día se encuentra cerrado para esta ruta. No es posible eliminar cobros.", PaymentErrorType.Conflict);
        }

        foreach (var alloc in payment.Allocations)
        {
            var inst = loan.Installments.FirstOrDefault(i => i.Id == alloc.InstallmentId);
            inst?.RollbackPayment(alloc.AmountAllocated);
        }
        loan.RollbackPayment(payment.Amount);

        await paymentRepository.DeletePaymentAsync(payment, loan, cancellationToken);

        var audit = FinancialMovementAuditLog.Create(
            actor.TenantId,
            loan.RouteId,
            FinancialMovementEntityType.Payment,
            payment.Id,
            FinancialMovementAuditAction.Deleted,
            $"Eliminación cobro de préstamo",
            payment.Amount,
            $"Cobro eliminado por valor de {payment.Amount:N2}. Medio: {payment.PaymentMethodName}",
            actor.UserId,
            actor.UserId.ToString("D"),
            actor.Role.ToString(),
            nowUtc);
        await financeRepository.AddAuditLogAsync(audit, cancellationToken);
        await financeRepository.SaveChangesAsync(cancellationToken);

        return PaymentResult.Ok(PaymentDetails.FromDomain(payment, loan.Terms.BalanceAmount, loan.Status));
    }

    public async Task<IReadOnlyCollection<PaymentDetails>> ListByLoanAsync(
        CustomerActor actor,
        Guid loanId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var loan = await loanRepository.FindAsync(actor.TenantId, loanId, cancellationToken);
        if (loan is null)
        {
            return [];
        }

        var routes = await routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success || routes.Routes.All(r => r.Id != loan.RouteId))
        {
            throw new UnauthorizedAccessException("No tiene acceso a los pagos de este préstamo.");
        }

        var payments = await paymentRepository.ListByLoanAsync(actor.TenantId, loanId, cancellationToken);
        return payments
            .Select(p => PaymentDetails.FromDomain(p, loan.Terms.BalanceAmount, loan.Status))
            .ToArray();
    }

    public async Task<DailyReconciliationReport> GetDailyReconciliationAsync(
        CustomerActor actor,
        Guid routeId,
        DateOnly date,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var routes = await routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success || routes.Routes.All(r => r.Id != routeId))
        {
            throw new UnauthorizedAccessException("No tiene acceso a la conciliación de esta ruta.");
        }

        var accessibleRoute = routes.Routes.FirstOrDefault(r => r.Id == routeId);
        var tz = RouteFinanceService.ResolveBusinessTimeZone(accessibleRoute?.TimeZoneId ?? "America/Bogota");
        var startLocal = date.ToDateTime(TimeOnly.MinValue);
        var endLocal = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, tz), TimeSpan.Zero);
        var endUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, tz), TimeSpan.Zero);

        var payments = await paymentRepository.ListByRouteAndDateAsync(actor.TenantId, routeId, startUtc, endUtc, cancellationToken);

        var totalCollected = payments.Sum(p => p.Amount);
        var paymentCount = payments.Count;

        var breakdown = payments
            .GroupBy(p => new { p.PaymentMethodId, p.PaymentMethodName })
            .Select(g => new PaymentMethodReconciliationSummary(
                g.Key.PaymentMethodId,
                g.Key.PaymentMethodName,
                g.Sum(p => p.Amount),
                g.Count()))
            .OrderByDescending(b => b.TotalAmount)
            .ToArray();

        return new DailyReconciliationReport(
            routeId,
            date,
            totalCollected,
            paymentCount,
            breakdown);
    }

    public async Task<PaymentResult> StoreAndAttachEvidenceAsync(
        CustomerActor actor,
        Guid paymentId,
        string fileName,
        string declaredContentType,
        Stream fileStream,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.FindAsync(actor.TenantId, paymentId, cancellationToken);
        if (payment is null)
        {
            return PaymentResult.Failure("Cobro no encontrado.", PaymentErrorType.NotFound);
        }

        var routes = await routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success || routes.Routes.All(r => r.Id != payment.RouteId))
        {
            return PaymentResult.Failure("No tiene autorización para adjuntar evidencias en este cobro.", PaymentErrorType.Forbidden);
        }

        Domain.Interfaces.Services.StoredFileResult stored;
        try
        {
            stored = await fileStorageService.StoreEvidenceAsync(
                actor.TenantId,
                paymentId,
                fileName,
                declaredContentType,
                fileStream,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return PaymentResult.Failure(ex.Message, PaymentErrorType.ValidationError);
        }

        var evidence = PaymentEvidence.Create(
            payment.Id,
            actor.TenantId,
            fileName,
            stored.ContentType,
            stored.ByteSize,
            stored.StoragePath,
            stored.FileHashSha256,
            actor.UserId,
            DateTimeOffset.UtcNow);

        try
        {
            await paymentRepository.AddEvidenceAsync(evidence, cancellationToken);
        }
        catch
        {
            await fileStorageService.DeleteEvidenceAsync(actor.TenantId, stored.StoragePath, CancellationToken.None);
            throw;
        }

        var loan = await loanRepository.FindAsync(actor.TenantId, payment.LoanId, cancellationToken);
        return PaymentResult.Ok(PaymentDetails.FromDomain(payment, loan?.Terms.BalanceAmount ?? 0m, loan?.Status ?? LoanStatus.Active));
    }

    public async Task<PaymentResult> DeleteEvidenceAsync(
        CustomerActor actor,
        Guid paymentId,
        Guid evidenceId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (actor.Role is not UserRole.Admin)
        {
            return PaymentResult.Failure("Solo el administrador puede solicitar el retiro de una evidencia fotográfica.", PaymentErrorType.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return PaymentResult.Failure("El motivo del retiro de la evidencia es obligatorio.", PaymentErrorType.ValidationError);
        }

        var evidence = await paymentRepository.FindEvidenceAsync(actor.TenantId, paymentId, evidenceId, cancellationToken);
        if (evidence is null || evidence.IsDeleted)
        {
            return PaymentResult.Failure("Evidencia no encontrada.", PaymentErrorType.NotFound);
        }

        evidence.MarkDeleted(actor.UserId, reason, DateTimeOffset.UtcNow);
        await paymentRepository.SaveChangesAsync(cancellationToken);

        var payment = await paymentRepository.FindAsync(actor.TenantId, paymentId, cancellationToken);
        var loan = payment is not null ? await loanRepository.FindAsync(actor.TenantId, payment.LoanId, cancellationToken) : null;

        return PaymentResult.Ok(PaymentDetails.FromDomain(payment!, loan?.Terms.BalanceAmount ?? 0m, loan?.Status ?? LoanStatus.Active));
    }
}
