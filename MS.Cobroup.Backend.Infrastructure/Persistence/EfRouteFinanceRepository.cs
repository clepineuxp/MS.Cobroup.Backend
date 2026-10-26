using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.RouteFinances;

namespace MS.Cobroup.Backend.Infrastructure.Persistence;

public sealed class EfRouteFinanceRepository(OperationsDbContext dbContext) : IRouteFinanceRepository
{
    public async Task<RouteBalanceTotals> GetTotalsAsync(
        Guid tenantId,
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var loans = await dbContext.Loans
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RouteId == routeId)
            .SumAsync(item => (decimal?)item.Terms.PrincipalAmount, cancellationToken) ?? 0m;
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RouteId == routeId)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var incomeInvestments = await dbContext.RouteFinancialEntries
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RouteId == routeId
                && (item.Type == RouteFinancialEntryType.Income || item.Type == RouteFinancialEntryType.Investment))
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        var expenses = await dbContext.RouteFinancialEntries
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RouteId == routeId
                && item.Type == RouteFinancialEntryType.Expense)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
        return new RouteBalanceTotals(loans, payments, incomeInvestments, expenses);
    }

    public async Task<IReadOnlyCollection<RouteFinancialEntry>> ListEntriesAsync(
        Guid tenantId,
        Guid routeId,
        IReadOnlyCollection<RouteFinancialEntryType> types,
        CancellationToken cancellationToken) =>
        await dbContext.RouteFinancialEntries
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RouteId == routeId && types.Contains(item.Type))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);

    public Task<RouteFinancialEntry?> FindEntryAsync(
        Guid tenantId,
        Guid entryId,
        CancellationToken cancellationToken) =>
        dbContext.RouteFinancialEntries.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == entryId,
            cancellationToken);

    public async Task AddEntryAsync(RouteFinancialEntry entry, CancellationToken cancellationToken) =>
        await dbContext.RouteFinancialEntries.AddAsync(entry, cancellationToken);

    public Task RemoveEntryAsync(RouteFinancialEntry entry, CancellationToken cancellationToken)
    {
        dbContext.RouteFinancialEntries.Remove(entry);
        return Task.CompletedTask;
    }

    public Task<RouteDayClosing?> FindDayClosingAsync(
        Guid tenantId,
        Guid routeId,
        DateOnly businessDate,
        CancellationToken cancellationToken) =>
        dbContext.RouteDayClosings.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.RouteId == routeId && item.BusinessDate == businessDate,
            cancellationToken);

    public async Task SaveDayClosingAsync(RouteDayClosing closing, CancellationToken cancellationToken)
    {
        var existing = await dbContext.RouteDayClosings.SingleOrDefaultAsync(
            item => item.TenantId == closing.TenantId && item.RouteId == closing.RouteId && item.BusinessDate == closing.BusinessDate,
            cancellationToken);
        if (existing is null)
        {
            await dbContext.RouteDayClosings.AddAsync(closing, cancellationToken);
        }
    }

    public async Task AddAuditLogAsync(FinancialMovementAuditLog auditLog, CancellationToken cancellationToken) =>
        await dbContext.FinancialMovementAuditLogs.AddAsync(auditLog, cancellationToken);

    public async Task<IReadOnlyCollection<FinancialMovementAuditLog>> ListAuditLogsAsync(
        Guid tenantId,
        Guid? routeId,
        DateTimeOffset? fromTimestampUtc,
        DateTimeOffset? toTimestampUtc,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FinancialMovementAuditLogs
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        if (routeId.HasValue && routeId.Value != Guid.Empty)
        {
            query = query.Where(item => item.RouteId == routeId.Value);
        }

        if (fromTimestampUtc.HasValue)
        {
            query = query.Where(item => item.TimestampUtc >= fromTimestampUtc.Value);
        }

        if (toTimestampUtc.HasValue)
        {
            query = query.Where(item => item.TimestampUtc <= toTimestampUtc.Value);
        }

        return await query
            .OrderByDescending(item => item.TimestampUtc)
            .ToArrayAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
