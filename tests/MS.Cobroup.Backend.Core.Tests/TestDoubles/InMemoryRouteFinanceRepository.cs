using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.RouteFinances;

namespace MS.Cobroup.Backend.Core.Tests.TestDoubles;

public sealed class InMemoryRouteFinanceRepository : IRouteFinanceRepository
{
    public RouteBalanceTotals Totals { get; set; } = new(0m, 0m, 0m, 0m);
    public List<RouteFinancialEntry> Entries { get; } = [];
    public List<RouteDayClosing> DayClosings { get; } = [];
    public List<FinancialMovementAuditLog> AuditLogs { get; } = [];

    public Task<RouteBalanceTotals> GetTotalsAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken) =>
        Task.FromResult(Totals);

    public Task<IReadOnlyCollection<RouteFinancialEntry>> ListEntriesAsync(
        Guid tenantId,
        Guid routeId,
        IReadOnlyCollection<RouteFinancialEntryType> types,
        CancellationToken cancellationToken)
    {
        var result = Entries
            .Where(e => e.TenantId == tenantId && e.RouteId == routeId && (types.Count == 0 || types.Contains(e.Type)))
            .ToList();
        return Task.FromResult<IReadOnlyCollection<RouteFinancialEntry>>(result);
    }

    public Task<RouteFinancialEntry?> FindEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken) =>
        Task.FromResult(Entries.SingleOrDefault(e => e.TenantId == tenantId && e.Id == entryId));

    public Task AddEntryAsync(RouteFinancialEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task RemoveEntryAsync(RouteFinancialEntry entry, CancellationToken cancellationToken)
    {
        Entries.Remove(entry);
        return Task.CompletedTask;
    }

    public Task<RouteDayClosing?> FindDayClosingAsync(Guid tenantId, Guid routeId, DateOnly businessDate, CancellationToken cancellationToken) =>
        Task.FromResult(DayClosings.SingleOrDefault(c => c.TenantId == tenantId && c.RouteId == routeId && c.BusinessDate == businessDate));

    public Task SaveDayClosingAsync(RouteDayClosing closing, CancellationToken cancellationToken)
    {
        var existing = DayClosings.FirstOrDefault(c => c.TenantId == closing.TenantId && c.RouteId == closing.RouteId && c.BusinessDate == closing.BusinessDate);
        if (existing is not null)
        {
            DayClosings.Remove(existing);
        }
        DayClosings.Add(closing);
        return Task.CompletedTask;
    }

    public Task AddAuditLogAsync(FinancialMovementAuditLog auditLog, CancellationToken cancellationToken)
    {
        AuditLogs.Add(auditLog);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<FinancialMovementAuditLog>> ListAuditLogsAsync(
        Guid tenantId,
        Guid? routeId,
        DateTimeOffset? fromTimestampUtc,
        DateTimeOffset? toTimestampUtc,
        CancellationToken cancellationToken)
    {
        var query = AuditLogs.Where(a => a.TenantId == tenantId);
        if (routeId.HasValue)
            query = query.Where(a => a.RouteId == routeId.Value);
        if (fromTimestampUtc.HasValue)
            query = query.Where(a => a.TimestampUtc >= fromTimestampUtc.Value);
        if (toTimestampUtc.HasValue)
            query = query.Where(a => a.TimestampUtc <= toTimestampUtc.Value);

        return Task.FromResult<IReadOnlyCollection<FinancialMovementAuditLog>>(query.OrderByDescending(a => a.TimestampUtc).ToList());
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
