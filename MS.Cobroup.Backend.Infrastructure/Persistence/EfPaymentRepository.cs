using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.Payments;

namespace MS.Cobroup.Backend.Infrastructure.Persistence;

public sealed class EfPaymentRepository(OperationsDbContext dbContext) : IPaymentRepository
{
    public Task<Payment?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.Payments
            .Include(p => p.Allocations)
            .Include(p => p.Evidences)
            .SingleOrDefaultAsync(
                p => p.TenantId == tenantId && p.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<Payment?> FindAsync(
        Guid tenantId,
        Guid paymentId,
        CancellationToken cancellationToken) =>
        dbContext.Payments
            .Include(p => p.Allocations)
            .Include(p => p.Evidences)
            .SingleOrDefaultAsync(
                p => p.TenantId == tenantId && p.Id == paymentId,
                cancellationToken);

    public async Task<IReadOnlyCollection<Payment>> ListByLoanAsync(
        Guid tenantId,
        Guid loanId,
        CancellationToken cancellationToken) =>
        await dbContext.Payments
            .AsNoTracking()
            .Include(p => p.Allocations)
            .Include(p => p.Evidences.Where(e => !e.IsDeleted))
            .Where(p => p.TenantId == tenantId && p.LoanId == loanId)
            .OrderByDescending(p => p.RegisteredAtUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Payment>> ListByRouteAndDateAsync(
        Guid tenantId,
        Guid routeId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken) =>
        await dbContext.Payments
            .AsNoTracking()
            .Include(p => p.Allocations)
            .Where(p => p.TenantId == tenantId && p.RouteId == routeId && p.RegisteredAtUtc >= fromUtc && p.RegisteredAtUtc <= toUtc)
            .OrderBy(p => p.RegisteredAtUtc)
            .ToArrayAsync(cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        await dbContext.Payments.AddAsync(payment, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public Task<PaymentEvidence?> FindEvidenceAsync(
        Guid tenantId,
        Guid paymentId,
        Guid evidenceId,
        CancellationToken cancellationToken) =>
        dbContext.PaymentEvidences
            .SingleOrDefaultAsync(
                e => e.TenantId == tenantId && e.PaymentId == paymentId && e.Id == evidenceId,
                cancellationToken);

    public async Task AddEvidenceAsync(PaymentEvidence evidence, CancellationToken cancellationToken)
    {
        await dbContext.PaymentEvidences.AddAsync(evidence, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

