using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.Loans;
using MS.Cobroup.Backend.Domain.Payments;

namespace MS.Cobroup.Backend.Core.Payments;

public sealed class PaymentService(
    IPaymentRepository paymentRepository,
    ILoanRepository loanRepository,
    IAdministrativeRouteDirectory routeDirectory,
    IOperationalPaymentMethodDirectory paymentMethodDirectory)
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

        if (routes.Routes.All(r => r.Id != loan.RouteId))
        {
            return PaymentResult.Failure("No tiene autorización para registrar cobros en esta ruta.", PaymentErrorType.Forbidden);
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

        var nowUtc = DateTimeOffset.UtcNow;
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

        await paymentRepository.AddAsync(payment, cancellationToken);
        await loanRepository.SaveChangesAsync(cancellationToken);

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

        var startUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

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

    public async Task<PaymentResult> AttachEvidenceAsync(
        CustomerActor actor,
        Guid paymentId,
        string fileName,
        string contentType,
        long byteSize,
        string storagePath,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.FindAsync(actor.TenantId, paymentId, cancellationToken);
        if (payment is null)
        {
            return PaymentResult.Failure("Pago no encontrado.", PaymentErrorType.NotFound);
        }

        var routes = await routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success || routes.Routes.All(r => r.Id != payment.RouteId))
        {
            return PaymentResult.Failure("No tiene acceso al préstamo de este pago.", PaymentErrorType.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedMimeTypes.Contains(contentType.Trim().ToLowerInvariant()))
        {
            return PaymentResult.Failure("Formato de imagen no permitido. Se admite JPG, PNG o WEBP.", PaymentErrorType.ValidationError);
        }

        var evidence = PaymentEvidence.Create(
            paymentId,
            actor.TenantId,
            fileName,
            contentType,
            byteSize,
            storagePath,
            actor.UserId,
            DateTimeOffset.UtcNow);

        await paymentRepository.AddEvidenceAsync(evidence, cancellationToken);

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
        // AC-14: Solo ADMIN puede retirar fotografía de manera auditable
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
