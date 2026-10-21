using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.LoanExceptions;

namespace MS.Cobroup.Backend.Infrastructure.Persistence;

public sealed class EfLoanExceptionRepository(OperationsDbContext dbContext) : ILoanExceptionRepository
{
    public Task<LoanExceptionRequest?> FindAsync(Guid tenantId, Guid exceptionId, CancellationToken cancellationToken) =>
        dbContext.LoanExceptionRequests
            .SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == exceptionId, cancellationToken);

    public async Task AddAsync(LoanExceptionRequest request, CancellationToken cancellationToken) =>
        await dbContext.LoanExceptionRequests.AddAsync(request, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<LoanExceptionSearchPage> SearchAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        Guid? customerId,
        LoanExceptionStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (accessibleRouteIds.Count is 0)
        {
            return new LoanExceptionSearchPage([], page, pageSize, 0);
        }

        var query = from e in dbContext.LoanExceptionRequests.AsNoTracking()
                    join c in dbContext.Customers.AsNoTracking() on e.CustomerId equals c.Id into customerGroup
                    from c in customerGroup.DefaultIfEmpty()
                    where e.TenantId == tenantId && accessibleRouteIds.Contains(e.RouteId)
                    select new { Exception = e, Customer = c };

        if (routeId.HasValue)
        {
            query = query.Where(x => x.Exception.RouteId == routeId.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(x => x.Exception.CustomerId == customerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Exception.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Exception.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LoanExceptionSearchItem(
                x.Exception.Id,
                x.Exception.CustomerId,
                x.Customer != null ? (x.Customer.FirstNames + " " + x.Customer.LastNames).Trim() : string.Empty,
                x.Exception.RouteId,
                x.Exception.ProposedAmount,
                x.Exception.ProposedDays,
                x.Exception.ProposedPercentage,
                x.Exception.Reason,
                x.Exception.Status,
                x.Exception.CreatedByUserId,
                x.Exception.CreatedByRole,
                x.Exception.CreatedAtUtc,
                x.Exception.DecidedByUserId,
                x.Exception.DecidedAtUtc,
                x.Exception.DecisionNotes,
                x.Exception.CancelledByUserId,
                x.Exception.CancelledAtUtc))
            .ToArrayAsync(cancellationToken);

        return new LoanExceptionSearchPage(items, page, pageSize, totalCount);
    }
}
