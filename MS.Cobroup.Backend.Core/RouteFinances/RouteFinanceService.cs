using System.Text.Json;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.RouteFinances;
using MS.Cobroup.Backend.Domain.Interfaces.Directories;
using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.Routes;

namespace MS.Cobroup.Backend.Core.RouteFinances;

public sealed class RouteFinanceService(
    IRouteFinanceRepository repository,
    IAdministrativeRouteDirectory routeDirectory,
    IAgendaRepository? agendaRepository = null,
    ILoanRepository? loanRepository = null,
    ICustomerRepository? customerRepository = null)
{
    public async Task<RouteFinanceResult> GetBalanceAsync(
        RouteFinanceCommand command,
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, routeId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;

        var totals = await repository.GetTotalsAsync(command.Actor.TenantId, routeId, cancellationToken);
        var route = routeResult.Route!;
        var net = route.InitialBalance
            - totals.LoanDisbursements
            + totals.PaymentCollections
            + totals.IncomeInvestments
            - totals.Expenses;

        var openClosing = await repository.FindActiveOpenClosingAsync(command.Actor.TenantId, routeId, cancellationToken);
        decimal initialBalance;
        decimal cajaLoans = 0m;
        decimal cajaPayments = 0m;
        decimal cajaIncome = 0m;
        decimal cajaExpenses = 0m;

        if (openClosing is not null && openClosing.OpenedAtUtc.HasValue)
        {
            var cajaTotals = await repository.GetTotalsSinceAsync(command.Actor.TenantId, routeId, openClosing.OpenedAtUtc.Value, cancellationToken);
            cajaLoans = cajaTotals.LoanDisbursements;
            cajaPayments = cajaTotals.PaymentCollections;
            cajaIncome = cajaTotals.IncomeInvestments;
            cajaExpenses = cajaTotals.Expenses;
            var delta = cajaPayments + cajaIncome - cajaLoans - cajaExpenses;
            initialBalance = net - delta;
        }
        else
        {
            // Si la caja está cerrada, balance inicial y actual siempre debe de ir igual
            // y los movimientos de la caja son 0
            initialBalance = net;
        }

        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            Balance: new RouteBalanceDetails(
                route.Id,
                route.Name,
                initialBalance,
                cajaLoans,
                cajaPayments,
                cajaIncome,
                cajaExpenses,
                net));
    }

    public async Task<RouteFinanceResult> ListEntriesAsync(
        RouteFinanceCommand command,
        Guid routeId,
        string section,
        CancellationToken cancellationToken)
    {
        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, routeId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;
        var types = ParseSection(section);
        if (types is null) return RouteFinanceResult.Invalid("La sección financiera indicada no es válida.");
        var entries = await repository.ListEntriesAsync(command.Actor.TenantId, routeId, types, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var isClosed = await IsDayClosedAsync(command.Actor.TenantId, routeId, now, routeResult.Route!, cancellationToken);
        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            Entries: entries.Select(entry => ToDetails(entry, command.Actor, now, routeResult.Route!.TimeZoneId, isClosed)).ToArray());
    }

    public async Task<RouteFinanceResult> CreateEntryAsync(
        RouteFinanceCommand command,
        SaveRouteFinancialEntryInput input,
        CancellationToken cancellationToken)
    {
        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, input.RouteId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;

        var now = DateTimeOffset.UtcNow;
        var boxValidation = await ValidateCashBoxForMovementAsync(
            repository,
            command.Actor.TenantId,
            routeResult.Route!,
            command.Actor.UserId,
            command.ActorDisplayName,
            now,
            cancellationToken);

        if (!boxValidation.IsAllowed)
        {
            return RouteFinanceResult.Invalid(boxValidation.ErrorMessage!);
        }

        if (input.Type == RouteFinancialEntryType.Microinsurance)
        {
            if (!input.LoanId.HasValue || input.LoanId.Value == Guid.Empty)
            {
                return RouteFinanceResult.Invalid("Debe seleccionar el crédito al que se vinculará el microseguro.");
            }

            if (loanRepository is not null)
            {
                var loan = await loanRepository.FindAsync(command.Actor.TenantId, input.LoanId.Value, cancellationToken);
                if (loan is null || loan.RouteId != input.RouteId)
                {
                    return RouteFinanceResult.Invalid("El crédito seleccionado no existe o no pertenece a esta ruta.");
                }
                if (loan.Status != Domain.Loans.LoanStatus.Active)
                {
                    return RouteFinanceResult.Invalid("Solo se puede vincular microseguros a créditos activos.");
                }
                loan.SetMicroinsuranceAmount(input.Amount);
            }
        }

        try
        {
            var concept = input.Concept;
            if (input.Type == RouteFinancialEntryType.Microinsurance && input.LoanId.HasValue && loanRepository is not null && customerRepository is not null)
            {
                var loan = await loanRepository.FindAsync(command.Actor.TenantId, input.LoanId.Value, cancellationToken);
                if (loan is not null)
                {
                    var customer = await customerRepository.FindAsync(command.Actor.TenantId, loan.CustomerId, cancellationToken);
                    if (customer is not null)
                    {
                        concept = $"Microseguro - {customer.FirstNames} {customer.LastNames}".Trim();
                    }
                }
            }

            var entry = RouteFinancialEntry.Create(
                command.Actor.TenantId,
                input.RouteId,
                input.Type,
                concept,
                input.Description,
                input.Amount,
                command.Actor.UserId,
                command.ActorDisplayName,
                now,
                input.LoanId);
            await repository.AddEntryAsync(entry, cancellationToken);

            var audit = FinancialMovementAuditLog.Create(
                command.Actor.TenantId,
                input.RouteId,
                FinancialMovementEntityType.RouteFinancialEntry,
                entry.Id,
                FinancialMovementAuditAction.Created,
                entry.Concept,
                entry.Amount,
                $"Creado {entry.Type}: '{entry.Concept}'. Monto: {entry.Amount:N2}",
                command.Actor.UserId,
                command.ActorDisplayName,
                command.Actor.Role.ToString(),
                now);
            await repository.AddAuditLogAsync(audit, cancellationToken);

            await repository.SaveChangesAsync(cancellationToken);
            return new RouteFinanceResult(
                RouteFinanceOutcome.Created,
                Entry: ToDetails(entry, command.Actor, now, routeResult.Route!.TimeZoneId, false));
        }
        catch (ArgumentException exception)
        {
            return RouteFinanceResult.Invalid(exception.Message);
        }
    }

    public async Task<RouteFinanceResult> UpdateEntryAsync(
        RouteFinanceCommand command,
        Guid entryId,
        SaveRouteFinancialEntryInput input,
        CancellationToken cancellationToken)
    {
        var entry = await repository.FindEntryAsync(command.Actor.TenantId, entryId, cancellationToken);
        if (entry is null) return RouteFinanceResult.NotFound();
        if (input.RouteId != entry.RouteId) return RouteFinanceResult.Invalid("La ruta del movimiento no puede cambiar.");

        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, entry.RouteId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;
        var now = DateTimeOffset.UtcNow;
        var boxValidation = await ValidateCashBoxForMovementAsync(
            repository,
            command.Actor.TenantId,
            routeResult.Route!,
            command.Actor.UserId,
            command.ActorDisplayName,
            now,
            cancellationToken);

        if (!boxValidation.IsAllowed)
        {
            return RouteFinanceResult.Invalid(boxValidation.ErrorMessage!);
        }

        if (!CanEdit(entry, command.Actor, now, routeResult.Route!.TimeZoneId))
        {
            return RouteFinanceResult.Forbidden();
        }

        try
        {
            var prevConcept = entry.Concept;
            var prevAmount = entry.Amount;
            var prevType = entry.Type;

            if (loanRepository is not null)
            {
                var targetLoanId = input.LoanId ?? entry.LoanId;
                if (targetLoanId.HasValue)
                {
                    var loan = await loanRepository.FindAsync(command.Actor.TenantId, targetLoanId.Value, cancellationToken);
                    if (loan is not null)
                    {
                        if (input.Type == RouteFinancialEntryType.Microinsurance)
                        {
                            loan.SetMicroinsuranceAmount(input.Amount);
                        }
                        else if (prevType == RouteFinancialEntryType.Microinsurance)
                        {
                            loan.SetMicroinsuranceAmount(0m);
                        }
                    }
                }
            }

            entry.Update(input.Type, input.Concept, input.Description, input.Amount,
                command.Actor.UserId, command.ActorDisplayName, now, input.LoanId ?? entry.LoanId);

            var audit = FinancialMovementAuditLog.Create(
                command.Actor.TenantId,
                entry.RouteId,
                FinancialMovementEntityType.RouteFinancialEntry,
                entry.Id,
                FinancialMovementAuditAction.Updated,
                entry.Concept,
                entry.Amount,
                $"Modificado {prevType}->{input.Type}, Concepto: '{prevConcept}'->'{input.Concept}', Monto: {prevAmount:N2}->{input.Amount:N2}",
                command.Actor.UserId,
                command.ActorDisplayName,
                command.Actor.Role.ToString(),
                now);
            await repository.AddAuditLogAsync(audit, cancellationToken);

            await repository.SaveChangesAsync(cancellationToken);
            return new RouteFinanceResult(
                RouteFinanceOutcome.Success,
                Entry: ToDetails(entry, command.Actor, now, routeResult.Route.TimeZoneId, false));
        }
        catch (ArgumentException exception)
        {
            return RouteFinanceResult.Invalid(exception.Message);
        }
    }

    public async Task<RouteFinanceResult> DeleteEntryAsync(
        RouteFinanceCommand command,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        var entry = await repository.FindEntryAsync(command.Actor.TenantId, entryId, cancellationToken);
        if (entry is null) return RouteFinanceResult.NotFound();

        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, entry.RouteId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;
        var now = DateTimeOffset.UtcNow;
        var boxValidation = await ValidateCashBoxForMovementAsync(
            repository,
            command.Actor.TenantId,
            routeResult.Route!,
            command.Actor.UserId,
            command.ActorDisplayName,
            now,
            cancellationToken);

        if (!boxValidation.IsAllowed)
        {
            return RouteFinanceResult.Invalid(boxValidation.ErrorMessage!);
        }

        if (!CanEdit(entry, command.Actor, now, routeResult.Route!.TimeZoneId))
        {
            return RouteFinanceResult.Forbidden();
        }

        if (entry.Type == RouteFinancialEntryType.Microinsurance && entry.LoanId.HasValue && loanRepository is not null)
        {
            var loan = await loanRepository.FindAsync(command.Actor.TenantId, entry.LoanId.Value, cancellationToken);
            if (loan is not null)
            {
                loan.SetMicroinsuranceAmount(0m);
            }
        }

        await repository.RemoveEntryAsync(entry, cancellationToken);

        var audit = FinancialMovementAuditLog.Create(
            command.Actor.TenantId,
            entry.RouteId,
            FinancialMovementEntityType.RouteFinancialEntry,
            entry.Id,
            FinancialMovementAuditAction.Deleted,
            entry.Concept,
            entry.Amount,
            $"Eliminado {entry.Type}: '{entry.Concept}'. Monto previo: {entry.Amount:N2}",
            command.Actor.UserId,
            command.ActorDisplayName,
            command.Actor.Role.ToString(),
            now);
        await repository.AddAuditLogAsync(audit, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
        return new RouteFinanceResult(RouteFinanceOutcome.Success);
    }

    public async Task<RouteFinanceResult> CloseDayAsync(
        RouteFinanceCommand command,
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, routeId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;

        var now = DateTimeOffset.UtcNow;
        var businessTimeZone = ResolveBusinessTimeZone(routeResult.Route!.TimeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, businessTimeZone).DateTime);

        var closing = await repository.FindActiveOpenClosingAsync(command.Actor.TenantId, routeId, cancellationToken);
        if (closing is null)
        {
            closing = await repository.FindDayClosingAsync(command.Actor.TenantId, routeId, localDate, cancellationToken);
        }

        var closingBusinessDate = closing?.BusinessDate ?? localDate;

        if (agendaRepository is not null)
        {
            var rawCustomers = await agendaRepository.GetAgendaCustomersAsync(
                command.Actor.TenantId,
                [routeId],
                routeId,
                closingBusinessDate,
                cancellationToken);
            var agendaItems = Agenda.AgendaService.ProcessAgendaItems(rawCustomers, closingBusinessDate, null, null);
            var unrecordedPending = agendaItems.Count(i => i.Status is Domain.Agenda.AgendaStatus.Overdue or Domain.Agenda.AgendaStatus.DueToday);
            if (unrecordedPending > 0)
            {
                return RouteFinanceResult.Invalid(
                    $"No es posible cerrar la caja. Debe registrar el motivo de no pago para todos los créditos pendientes (faltan {unrecordedPending} por registrar).");
            }
        }

        if (closing is null)
        {
            closing = RouteDayClosing.CreateClosed(command.Actor.TenantId, routeId, localDate, command.Actor.UserId, command.ActorDisplayName, now);
        }
        else
        {
            closing.Close(command.Actor.UserId, command.ActorDisplayName, now);
        }

        await repository.SaveDayClosingAsync(closing, cancellationToken);

        var audit = FinancialMovementAuditLog.Create(
            command.Actor.TenantId,
            routeId,
            FinancialMovementEntityType.RouteFinancialEntry,
            closing.Id,
            FinancialMovementAuditAction.Updated,
            "Cierre de Caja",
            0m,
            $"Caja cerrada por {command.ActorDisplayName} para la fecha operativa {closing.BusinessDate:yyyy-MM-dd}",
            command.Actor.UserId,
            command.ActorDisplayName,
            command.Actor.Role.ToString(),
            now);
        await repository.AddAuditLogAsync(audit, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var canReopen = command.Actor.Role == UserRole.Admin || (command.Actor.Role == UserRole.Supervisor && command.Actor.CanReopenDay);
        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            ClosingStatus: new DayClosingStatusDetails(
                routeId,
                routeResult.Route!.Name,
                closing.BusinessDate,
                closing.IsClosed,
                closing.ClosedAtUtc,
                closing.ClosedByDisplayName,
                closing.ReopenedAtUtc,
                closing.ReopenedByDisplayName,
                canReopen,
                closing.OpenedAtUtc,
                closing.OpenedByDisplayName,
                false,
                null,
                closing.GetBoxName(routeResult.Route!.Name)));
    }

    public async Task<RouteFinanceResult> ReopenBoxAsync(
        RouteFinanceCommand command,
        Guid routeId,
        DateOnly? businessDate,
        CancellationToken cancellationToken)
    {
        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, routeId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;

        var canReopen = command.Actor.Role == UserRole.Admin || (command.Actor.Role == UserRole.Supervisor && command.Actor.CanReopenDay);
        if (!canReopen)
        {
            return RouteFinanceResult.Forbidden("Solo la administración puede reabrir una caja cerrada.");
        }

        var openClosing = await repository.FindActiveOpenClosingAsync(command.Actor.TenantId, routeId, cancellationToken);
        if (openClosing is not null)
        {
            return RouteFinanceResult.Invalid("No es posible reabrir la caja porque existe una caja abierta para esta ruta. Debe cerrar la caja abierta primero.");
        }

        var now = DateTimeOffset.UtcNow;
        var closing = businessDate.HasValue
            ? await repository.FindDayClosingAsync(command.Actor.TenantId, routeId, businessDate.Value, cancellationToken)
            : await repository.FindLatestDayClosingAsync(command.Actor.TenantId, routeId, cancellationToken);

        if (closing is null)
        {
            return RouteFinanceResult.NotFound();
        }

        closing.Reopen(command.Actor.UserId, command.ActorDisplayName, now);
        await repository.SaveDayClosingAsync(closing, cancellationToken);

        var audit = FinancialMovementAuditLog.Create(
            command.Actor.TenantId,
            routeId,
            FinancialMovementEntityType.RouteFinancialEntry,
            closing.Id,
            FinancialMovementAuditAction.Updated,
            "Reapertura de Caja",
            0m,
            $"Caja reabierta por {command.ActorDisplayName} para la fecha operativa {closing.BusinessDate:yyyy-MM-dd}",
            command.Actor.UserId,
            command.ActorDisplayName,
            command.Actor.Role.ToString(),
            now);
        await repository.AddAuditLogAsync(audit, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            ClosingStatus: new DayClosingStatusDetails(
                routeId,
                routeResult.Route!.Name,
                closing.BusinessDate,
                closing.IsClosed,
                closing.ClosedAtUtc,
                closing.ClosedByDisplayName,
                closing.ReopenedAtUtc,
                closing.ReopenedByDisplayName,
                true,
                closing.OpenedAtUtc,
                closing.OpenedByDisplayName,
                false,
                null,
                closing.GetBoxName(routeResult.Route!.Name)));
    }

    public Task<RouteFinanceResult> ReopenDayAsync(
        RouteFinanceCommand command,
        Guid routeId,
        CancellationToken cancellationToken) =>
        ReopenBoxAsync(command, routeId, null, cancellationToken);

    public async Task<RouteFinanceResult> GetDayClosingStatusAsync(
        RouteFinanceCommand command,
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, routeId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;

        var now = DateTimeOffset.UtcNow;
        var businessTimeZone = ResolveBusinessTimeZone(routeResult.Route!.TimeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, businessTimeZone).DateTime);
        var canReopen = command.Actor.Role == UserRole.Admin || (command.Actor.Role == UserRole.Supervisor && command.Actor.CanReopenDay);

        var openClosing = await repository.FindActiveOpenClosingAsync(command.Actor.TenantId, routeId, cancellationToken);
        if (openClosing is not null)
        {
            var cutoffUtc = CalculateCutoffUtc(openClosing.BusinessDate, routeResult.Route.DailyClosingTimeUtc, routeResult.Route.TimeZoneId);
            var isCutoffExceeded = routeResult.Route.EnforceDailyClosingSchedule &&
                                   cutoffUtc.HasValue &&
                                   now >= cutoffUtc.Value;

            return new RouteFinanceResult(
                RouteFinanceOutcome.Success,
                ClosingStatus: new DayClosingStatusDetails(
                    routeId,
                    routeResult.Route!.Name,
                    openClosing.BusinessDate,
                    openClosing.IsClosed,
                    openClosing.ClosedAtUtc,
                    openClosing.ClosedByDisplayName,
                    openClosing.ReopenedAtUtc,
                    openClosing.ReopenedByDisplayName,
                    canReopen,
                    openClosing.OpenedAtUtc,
                    openClosing.OpenedByDisplayName,
                    isCutoffExceeded,
                    cutoffUtc,
                    openClosing.GetBoxName(routeResult.Route!.Name)));
        }

        var latestClosing = await repository.FindLatestDayClosingAsync(command.Actor.TenantId, routeId, cancellationToken);
        var closedBoxName = $"{routeResult.Route!.Name} | Caja Cerrada";
        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            ClosingStatus: new DayClosingStatusDetails(
                routeId,
                routeResult.Route!.Name,
                latestClosing?.BusinessDate ?? localDate,
                latestClosing?.IsClosed ?? true,
                latestClosing?.ClosedAtUtc,
                latestClosing?.ClosedByDisplayName,
                latestClosing?.ReopenedAtUtc,
                latestClosing?.ReopenedByDisplayName,
                canReopen,
                latestClosing?.OpenedAtUtc,
                latestClosing?.OpenedByDisplayName,
                false,
                null,
                closedBoxName));
    }

    public async Task<RouteFinanceResult> ListAuditTraceAsync(
        RouteFinanceCommand command,
        Guid? routeId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        if (command.Actor.Role is not (UserRole.Admin or UserRole.Supervisor))
        {
            return RouteFinanceResult.Forbidden("La traza de auditoría de movimientos es confidencial y solo está disponible para administradores y supervisores.");
        }

        var routes = await routeDirectory.ListAccessibleAsync(command.AccessToken, cancellationToken);
        if (routes.Outcome is RouteDirectoryOutcome.Unauthorized) return RouteFinanceResult.Unauthorized();
        if (routes.Outcome is RouteDirectoryOutcome.Forbidden) return RouteFinanceResult.Forbidden();
        if (routes.Outcome is RouteDirectoryOutcome.Unavailable) return RouteFinanceResult.Unavailable();
        if (fromUtc > toUtc) return RouteFinanceResult.Invalid("El rango de fechas no es válido.");
        var accessibleRoutes = routes.Routes;
        if (routeId.HasValue && !accessibleRoutes.Any(route => route.Id == routeId.Value)) return RouteFinanceResult.Forbidden();
        var targetRoute = routeId.HasValue ? accessibleRoutes.FirstOrDefault(r => r.Id == routeId.Value) : null;
        var defaultTz = ResolveBusinessTimeZone(targetRoute?.TimeZoneId ?? "America/Bogota");

        var logs = await repository.ListAuditLogsAsync(command.Actor.TenantId, routeId, fromUtc, toUtc, cancellationToken);
        var accessibleRouteIds = accessibleRoutes.Select(route => route.Id).ToHashSet();
        var details = logs.Where(log => accessibleRouteIds.Contains(log.RouteId)).Select(log =>
        {
            var tz = defaultTz;
            if (targetRoute is null)
            {
                var r = accessibleRoutes.FirstOrDefault(item => item.Id == log.RouteId);
                if (r is not null) tz = ResolveBusinessTimeZone(r.TimeZoneId);
            }
            var localTime = TimeZoneInfo.ConvertTime(log.TimestampUtc, tz);
            var formatted = localTime.ToString("yyyy-MM-dd HH:mm:ss");
            return new FinancialMovementAuditLogDetails(
                log.Id,
                log.RouteId,
                log.EntityType,
                log.EntityId,
                log.Action,
                log.Concept,
                log.Amount,
                log.Details,
                log.PerformedByUserId,
                log.PerformedByDisplayName,
                log.PerformedByUserRole,
                log.TimestampUtc,
                formatted);
        }).ToArray();

        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            AuditLogs: details);
    }

    private async Task<bool> IsDayClosedAsync(
        Guid tenantId,
        Guid routeId,
        DateTimeOffset now,
        AccessibleRoute route,
        CancellationToken cancellationToken)
    {
        var openClosing = await repository.FindActiveOpenClosingAsync(tenantId, routeId, cancellationToken);
        if (openClosing is not null)
        {
            if (route.EnforceDailyClosingSchedule && route.DailyClosingTimeUtc.HasValue)
            {
                var cutoffUtc = CalculateCutoffUtc(openClosing.BusinessDate, route.DailyClosingTimeUtc, route.TimeZoneId);
                if (cutoffUtc.HasValue && now >= cutoffUtc.Value) return true;
            }
            return false;
        }

        var businessTimeZone = ResolveBusinessTimeZone(route.TimeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, businessTimeZone).DateTime);
        var closing = await repository.FindDayClosingAsync(tenantId, routeId, localDate, cancellationToken);
        return closing?.IsClosed ?? false;
    }

    public static DateTimeOffset? CalculateCutoffUtc(
        DateOnly businessDate,
        TimeOnly? closingTimeUtc,
        string timeZoneId)
    {
        if (!closingTimeUtc.HasValue) return null;
        var tz = ResolveBusinessTimeZone(timeZoneId);

        var dummyUtc = DateTime.UtcNow.Date.Add(closingTimeUtc.Value.ToTimeSpan());
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(dummyUtc, tz).TimeOfDay;
        var localTimeOnly = TimeOnly.FromTimeSpan(localTime);

        var cutoffDate = localTimeOnly < new TimeOnly(12, 0)
            ? businessDate.AddDays(1)
            : businessDate;

        var cutoffLocal = cutoffDate.ToDateTime(localTimeOnly);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(cutoffLocal, tz), TimeSpan.Zero);
    }

    public static async Task<CashBoxValidationResult> ValidateCashBoxForMovementAsync(
        IRouteFinanceRepository repository,
        Guid tenantId,
        AccessibleRoute route,
        Guid actorUserId,
        string actorDisplayName,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var tz = ResolveBusinessTimeZone(route.TimeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, tz).DateTime);

        var openClosing = await repository.FindActiveOpenClosingAsync(tenantId, route.Id, cancellationToken);
        if (openClosing is not null)
        {
            if (route.EnforceDailyClosingSchedule && route.DailyClosingTimeUtc.HasValue)
            {
                var cutoffUtc = CalculateCutoffUtc(openClosing.BusinessDate, route.DailyClosingTimeUtc, route.TimeZoneId);
                if (cutoffUtc.HasValue && nowUtc >= cutoffUtc.Value)
                {
                    return CashBoxValidationResult.Blocked("Debes cerrar caja para continuar.");
                }
            }

            return CashBoxValidationResult.Allowed(openClosing);
        }

        // Si la caja está cerrada, al ingresar un nuevo movimiento se abre o reabre automáticamente
        var todayClosing = await repository.FindDayClosingAsync(tenantId, route.Id, localDate, cancellationToken);
        if (todayClosing is not null)
        {
            todayClosing.Reopen(actorUserId, actorDisplayName, nowUtc);
            await repository.SaveDayClosingAsync(todayClosing, cancellationToken);
            return CashBoxValidationResult.Allowed(todayClosing);
        }

        var newBox = RouteDayClosing.CreateOpen(tenantId, route.Id, localDate, actorUserId, actorDisplayName, nowUtc);
        await repository.SaveDayClosingAsync(newBox, cancellationToken);
        return CashBoxValidationResult.Allowed(newBox);
    }

    private async Task<(AccessibleRoute? Route, RouteFinanceResult? Failure)> FindAccessibleRouteAsync(
        string accessToken,
        Guid routeId,
        CancellationToken cancellationToken)
    {
        if (routeId == Guid.Empty) return (null, RouteFinanceResult.Invalid("La ruta es obligatoria."));
        var routes = await routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);
        if (routes.Outcome is RouteDirectoryOutcome.Unauthorized) return (null, RouteFinanceResult.Unauthorized());
        if (routes.Outcome is RouteDirectoryOutcome.Forbidden) return (null, RouteFinanceResult.Forbidden());
        if (routes.Outcome is RouteDirectoryOutcome.Unavailable) return (null, RouteFinanceResult.Unavailable());
        var route = routes.Routes.FirstOrDefault(item => item.Id == routeId);
        return route is null ? (null, RouteFinanceResult.Forbidden()) : (route, null);
    }

    public async Task<RouteFinanceResult> GetEligibleLoansForMicroinsuranceAsync(
        RouteFinanceCommand command,
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, routeId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;

        if (loanRepository is null || customerRepository is null)
        {
            return new RouteFinanceResult(RouteFinanceOutcome.Success, EligibleLoans: []);
        }

        var now = DateTimeOffset.UtcNow;
        var businessTimeZone = ResolveBusinessTimeZone(routeResult.Route!.TimeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, businessTimeZone).DateTime);

        var closing = await repository.FindActiveOpenClosingAsync(command.Actor.TenantId, routeId, cancellationToken);
        var targetDate = closing?.BusinessDate ?? localDate;

        var fromUtc = new DateTimeOffset(targetDate.ToDateTime(TimeOnly.MinValue), businessTimeZone.GetUtcOffset(targetDate.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
        var toUtc = new DateTimeOffset(targetDate.AddDays(1).ToDateTime(TimeOnly.MinValue).AddTicks(-1), businessTimeZone.GetUtcOffset(targetDate.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();

        var searchPage = await loanRepository.SearchAsync(
            command.Actor.TenantId,
            [routeId],
            routeId,
            null,
            Domain.Loans.LoanStatus.Active,
            1,
            200,
            cancellationToken);

        var eligible = searchPage.Items
            .Where(l => l.CreatedAtUtc >= fromUtc && l.CreatedAtUtc <= toUtc)
            .ToList();

        var eligibleLoans = new List<EligibleLoanForMicroinsuranceDto>();
        foreach (var l in eligible)
        {
            var loanEntity = await loanRepository.FindAsync(command.Actor.TenantId, l.Id, cancellationToken);
            if (loanEntity is not null && loanEntity.MicroinsuranceAmount <= 0m)
            {
                eligibleLoans.Add(new EligibleLoanForMicroinsuranceDto(
                    l.Id,
                    l.CustomerId,
                    l.CustomerName,
                    l.PrincipalAmount,
                    l.CreatedAtUtc));
            }
        }

        return new RouteFinanceResult(RouteFinanceOutcome.Success, EligibleLoans: eligibleLoans);
    }

    private static IReadOnlyCollection<RouteFinancialEntryType>? ParseSection(string section) =>
        section.Trim().ToLowerInvariant() switch
        {
            "income-investments" => [RouteFinancialEntryType.Income, RouteFinancialEntryType.Investment, RouteFinancialEntryType.Microinsurance],
            "microinsurance" or "microinsurances" => [RouteFinancialEntryType.Microinsurance],
            "expenses" => [RouteFinancialEntryType.Expense],
            "all" => Enum.GetValues<RouteFinancialEntryType>(),
            _ => null
        };

    private static RouteFinancialEntryDetails ToDetails(
        RouteFinancialEntry entry,
        CustomerActor actor,
        DateTimeOffset now,
        string timeZoneId,
        bool isDayClosed = false) =>
        new(
            entry.Id,
            entry.RouteId,
            entry.Type,
            entry.Concept,
            entry.Description,
            entry.Amount,
            entry.CreatedByUserId,
            entry.CreatedByDisplayName,
            entry.CreatedAtUtc,
            entry.UpdatedByUserId,
            entry.UpdatedByDisplayName,
            entry.UpdatedAtUtc,
            !isDayClosed && CanEdit(entry, actor, now, timeZoneId),
            entry.LoanId);

    private static bool CanEdit(
        RouteFinancialEntry entry,
        CustomerActor actor,
        DateTimeOffset now,
        string timeZoneId)
    {
        if (actor.Role is UserRole.Admin) return true;
        if (actor.Role is not (UserRole.Collector or UserRole.Supervisor)) return false;
        var businessTimeZone = ResolveBusinessTimeZone(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.CreatedAtUtc, businessTimeZone).DateTime)
            == DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, businessTimeZone).DateTime);
    }

    public static TimeZoneInfo ResolveBusinessTimeZone(string timeZoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch { return TimeZoneInfo.Utc; }
    }
}
