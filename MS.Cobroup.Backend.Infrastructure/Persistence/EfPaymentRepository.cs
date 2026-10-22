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

    public async Task<PaymentSaveOutcome> TryRegisterPaymentAsync(
        Payment payment,
        Domain.Loans.Loan loan,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var loanKey = BitConverter.ToInt64(loan.Id.ToByteArray(), 0);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({loanKey})",
                cancellationToken);

            var existing = await dbContext.Payments
                .Include(p => p.Allocations)
                .Include(p => p.Evidences)
                .SingleOrDefaultAsync(
                    p => p.TenantId == payment.TenantId && p.IdempotencyKey == payment.IdempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PaymentSaveOutcome.IdempotentReplay;
            }

            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PaymentSaveOutcome.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return PaymentSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pe && pe.SqlState == "23505")
        {
            await transaction.RollbackAsync(cancellationToken);
            return PaymentSaveOutcome.IdempotentReplay;
        }
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

