using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Infrastructure.Persistence;

public sealed class EfLoanRepository(OperationsDbContext dbContext) : ILoanRepository
{
    public Task<Loan?> FindAsync(Guid tenantId, Guid loanId, CancellationToken cancellationToken) =>
        dbContext.Loans
            .Include(l => l.Installments)
            .SingleOrDefaultAsync(l => l.TenantId == tenantId && l.Id == loanId, cancellationToken);

    public async Task AddAsync(Loan loan, CancellationToken cancellationToken) =>
        await dbContext.Loans.AddAsync(loan, cancellationToken);

    public async Task<LoanSaveOutcome> TryAddAsync(
        Loan loan,
        int? maxActiveLoans,
        Guid? consumedExceptionId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var routeKey = BitConverter.ToInt64(loan.RouteId.ToByteArray(), 0);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({routeKey})",
                cancellationToken);

            if (maxActiveLoans.HasValue)
            {
                var activeCount = await dbContext.Loans.CountAsync(
                    l => l.TenantId == loan.TenantId && l.RouteId == loan.RouteId && l.Status == LoanStatus.Active,
                    cancellationToken);

                if (activeCount >= maxActiveLoans.Value)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return LoanSaveOutcome.CapacityReached;
                }
            }

            if (consumedExceptionId.HasValue)
            {
                var exception = await dbContext.LoanExceptionRequests
                    .SingleOrDefaultAsync(
                        e => e.TenantId == loan.TenantId && e.Id == consumedExceptionId.Value,
                        cancellationToken);

                if (exception is null || exception.IsConsumed || exception.Status != Domain.LoanExceptions.LoanExceptionStatus.Approved)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return LoanSaveOutcome.ExceptionAlreadyConsumed;
                }

                exception.Consume(loan.Id, loan.CreatedAtUtc);
            }

            dbContext.Loans.Add(loan);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LoanSaveOutcome.Saved;
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return LoanSaveOutcome.Conflict;
        }
    }

    public Task<int> CountActiveLoansByRouteAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken) =>
        dbContext.Loans
            .CountAsync(l => l.TenantId == tenantId && l.RouteId == routeId && l.Status == LoanStatus.Active, cancellationToken);

    public async Task<IReadOnlyCollection<Loan>> ListByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken)
    {
        var loans = await dbContext.Loans
            .Include(l => l.Installments)
            .Where(l => l.TenantId == tenantId && l.CustomerId == customerId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return loans;
    }

    public async Task<LoanSearchPage> SearchAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        Guid? customerId,
        LoanStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (accessibleRouteIds.Count is 0)
        {
            return new LoanSearchPage([], page, pageSize, 0);
        }

        var query = dbContext.Loans.AsNoTracking()
            .Where(l => l.TenantId == tenantId && accessibleRouteIds.Contains(l.RouteId));

        if (routeId.HasValue)
        {
            query = query.Where(l => l.RouteId == routeId.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(l => l.CustomerId == customerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pagedLoans = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var customerIds = pagedLoans.Select(l => l.CustomerId).Distinct().ToList();
        var customerMap = await dbContext.Customers.AsNoTracking()
            .Where(c => c.TenantId == tenantId && customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => $"{c.FirstNames} {c.LastNames}".Trim(), cancellationToken);

        var items = pagedLoans.Select(l => new LoanSearchItem(
            l.Id,
            l.CustomerId,
            customerMap.GetValueOrDefault(l.CustomerId, string.Empty),
            l.RouteId,
            l.Terms.PrincipalAmount,
            l.Terms.InterestRate,
            l.Terms.TotalAmount,
            l.Terms.BalanceAmount,
            l.Terms.PaymentFrequency,
            l.Terms.InstallmentCount,
            l.Status,
            l.Terms.StartsAtUtc,
            l.Terms.DueAtUtc,
            l.CreatedAtUtc)).ToArray();

        return new LoanSearchPage(items, page, pageSize, totalCount);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
