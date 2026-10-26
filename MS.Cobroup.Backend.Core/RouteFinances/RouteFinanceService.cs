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
    IAdministrativeRouteDirectory routeDirectory)
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
        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            Balance: new RouteBalanceDetails(
                route.Id,
                route.Name,
                route.InitialBalance,
                totals.LoanDisbursements,
                totals.PaymentCollections,
                totals.IncomeInvestments,
                totals.Expenses,
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
        var isClosed = await IsDayClosedAsync(command.Actor.TenantId, routeId, now, routeResult.Route!.TimeZoneId, cancellationToken);
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
        if (await IsDayClosedAsync(command.Actor.TenantId, input.RouteId, now, routeResult.Route!.TimeZoneId, cancellationToken))
        {
            return RouteFinanceResult.Invalid("El día se encuentra cerrado para esta ruta. No es posible crear nuevos movimientos.");
        }

        try
        {
            var entry = RouteFinancialEntry.Create(
                command.Actor.TenantId,
                input.RouteId,
                input.Type,
                input.Concept,
                input.Description,
                input.Amount,
                command.Actor.UserId,
                command.ActorDisplayName,
                now);
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
        if (await IsDayClosedAsync(command.Actor.TenantId, entry.RouteId, now, routeResult.Route!.TimeZoneId, cancellationToken))
        {
            return RouteFinanceResult.Invalid("El día se encuentra cerrado para esta ruta. No es posible modificar movimientos.");
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

            entry.Update(input.Type, input.Concept, input.Description, input.Amount,
                command.Actor.UserId, command.ActorDisplayName, now);

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
        if (await IsDayClosedAsync(command.Actor.TenantId, entry.RouteId, now, routeResult.Route!.TimeZoneId, cancellationToken))
        {
            return RouteFinanceResult.Invalid("El día se encuentra cerrado para esta ruta. No es posible eliminar movimientos.");
        }

        if (!CanEdit(entry, command.Actor, now, routeResult.Route.TimeZoneId))
        {
            return RouteFinanceResult.Forbidden();
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

        var closing = await repository.FindDayClosingAsync(command.Actor.TenantId, routeId, localDate, cancellationToken);
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
            "Cierre de Día",
            0m,
            $"Día cerrado por {command.ActorDisplayName} para la fecha operativa {localDate:yyyy-MM-dd}",
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
                routeResult.Route.Name,
                localDate,
                closing.IsClosed,
                closing.ClosedAtUtc,
                closing.ClosedByDisplayName,
                closing.ReopenedAtUtc,
                closing.ReopenedByDisplayName,
                canReopen));
    }

    public async Task<RouteFinanceResult> ReopenDayAsync(
        RouteFinanceCommand command,
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, routeId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;

        var canReopen = command.Actor.Role == UserRole.Admin || (command.Actor.Role == UserRole.Supervisor && command.Actor.CanReopenDay);
        if (!canReopen)
        {
            return RouteFinanceResult.Forbidden("No tienes autorización para reabrir un día cerrado. Se requiere permiso administrativo.");
        }

        var now = DateTimeOffset.UtcNow;
        var businessTimeZone = ResolveBusinessTimeZone(routeResult.Route!.TimeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, businessTimeZone).DateTime);

        var closing = await repository.FindDayClosingAsync(command.Actor.TenantId, routeId, localDate, cancellationToken);
        if (closing is not null)
        {
            closing.Reopen(command.Actor.UserId, command.ActorDisplayName, now);
            await repository.SaveDayClosingAsync(closing, cancellationToken);

            var audit = FinancialMovementAuditLog.Create(
                command.Actor.TenantId,
                routeId,
                FinancialMovementEntityType.RouteFinancialEntry,
                closing.Id,
                FinancialMovementAuditAction.Updated,
                "Reapertura de Día",
                0m,
                $"Día reabierto por {command.ActorDisplayName} para la fecha operativa {localDate:yyyy-MM-dd}",
                command.Actor.UserId,
                command.ActorDisplayName,
                command.Actor.Role.ToString(),
                now);
            await repository.AddAuditLogAsync(audit, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }

        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            ClosingStatus: new DayClosingStatusDetails(
                routeId,
                routeResult.Route.Name,
                localDate,
                closing?.IsClosed ?? false,
                closing?.ClosedAtUtc,
                closing?.ClosedByDisplayName,
                closing?.ReopenedAtUtc,
                closing?.ReopenedByDisplayName,
                canReopen));
    }

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

        var closing = await repository.FindDayClosingAsync(command.Actor.TenantId, routeId, localDate, cancellationToken);
        var canReopen = command.Actor.Role == UserRole.Admin || (command.Actor.Role == UserRole.Supervisor && command.Actor.CanReopenDay);

        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            ClosingStatus: new DayClosingStatusDetails(
                routeId,
                routeResult.Route.Name,
                localDate,
                closing?.IsClosed ?? false,
                closing?.ClosedAtUtc,
                closing?.ClosedByDisplayName,
                closing?.ReopenedAtUtc,
                closing?.ReopenedByDisplayName,
                canReopen));
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
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        var businessTimeZone = ResolveBusinessTimeZone(timeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, businessTimeZone).DateTime);
        var closing = await repository.FindDayClosingAsync(tenantId, routeId, localDate, cancellationToken);
        return closing?.IsClosed ?? false;
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

    private static IReadOnlyCollection<RouteFinancialEntryType>? ParseSection(string section) =>
        section.Trim().ToLowerInvariant() switch
        {
            "income-investments" => [RouteFinancialEntryType.Income, RouteFinancialEntryType.Investment],
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
            !isDayClosed && CanEdit(entry, actor, now, timeZoneId));

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
