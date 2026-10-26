using MS.Cobroup.Backend.Domain.RouteFinances;

namespace MS.Cobroup.Backend.Domain.Interfaces.Repositories;

public interface IRouteFinanceRepository
{
    Task<RouteBalanceTotals> GetTotalsAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RouteFinancialEntry>> ListEntriesAsync(
        Guid tenantId,
        Guid routeId,
        IReadOnlyCollection<RouteFinancialEntryType> types,
        CancellationToken cancellationToken);

    Task<RouteFinancialEntry?> FindEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken);

    Task AddEntryAsync(RouteFinancialEntry entry, CancellationToken cancellationToken);

    Task RemoveEntryAsync(RouteFinancialEntry entry, CancellationToken cancellationToken);

    Task<RouteDayClosing?> FindDayClosingAsync(Guid tenantId, Guid routeId, DateOnly businessDate, CancellationToken cancellationToken);

    Task SaveDayClosingAsync(RouteDayClosing closing, CancellationToken cancellationToken);

    Task AddAuditLogAsync(FinancialMovementAuditLog auditLog, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<FinancialMovementAuditLog>> ListAuditLogsAsync(
        Guid tenantId,
        Guid? routeId,
        DateTimeOffset? fromTimestampUtc,
        DateTimeOffset? toTimestampUtc,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record RouteBalanceTotals(
    decimal LoanDisbursements,
    decimal PaymentCollections,
    decimal IncomeInvestments,
    decimal Expenses);
