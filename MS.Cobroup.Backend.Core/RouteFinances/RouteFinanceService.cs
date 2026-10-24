using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.RouteFinances;
using MS.Cobroup.Backend.Core.Customers;

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
        return new RouteFinanceResult(
            RouteFinanceOutcome.Success,
            Entries: entries.Select(entry => ToDetails(entry, command.Actor, now, routeResult.Route!.TimeZoneId)).ToArray());
    }

    public async Task<RouteFinanceResult> CreateEntryAsync(
        RouteFinanceCommand command,
        SaveRouteFinancialEntryInput input,
        CancellationToken cancellationToken)
    {
        var routeResult = await FindAccessibleRouteAsync(command.AccessToken, input.RouteId, cancellationToken);
        if (routeResult.Failure is not null) return routeResult.Failure;
        try
        {
            var now = DateTimeOffset.UtcNow;
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
            await repository.SaveChangesAsync(cancellationToken);
            return new RouteFinanceResult(
                RouteFinanceOutcome.Created,
                Entry: ToDetails(entry, command.Actor, now, routeResult.Route!.TimeZoneId));
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
        if (!CanEdit(entry, command.Actor, now, routeResult.Route!.TimeZoneId))
        {
            return RouteFinanceResult.Forbidden();
        }

        try
        {
            entry.Update(input.Type, input.Concept, input.Description, input.Amount,
                command.Actor.UserId, command.ActorDisplayName, now);
            await repository.SaveChangesAsync(cancellationToken);
            return new RouteFinanceResult(
                RouteFinanceOutcome.Success,
                Entry: ToDetails(entry, command.Actor, now, routeResult.Route.TimeZoneId));
        }
        catch (ArgumentException exception)
        {
            return RouteFinanceResult.Invalid(exception.Message);
        }
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
        string timeZoneId) =>
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
            CanEdit(entry, actor, now, timeZoneId));

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

    private static TimeZoneInfo ResolveBusinessTimeZone(string timeZoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch { return TimeZoneInfo.Utc; }
    }
}
