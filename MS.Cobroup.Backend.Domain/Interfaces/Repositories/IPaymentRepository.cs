using MS.Cobroup.Backend.Domain.Payments;

namespace MS.Cobroup.Backend.Domain.Interfaces.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken);

    Task<Payment?> FindAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Payment>> ListByLoanAsync(Guid tenantId, Guid loanId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Payment>> ListByRouteAndDateAsync(
        Guid tenantId,
        Guid routeId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<PaymentEvidence?> FindEvidenceAsync(Guid tenantId, Guid paymentId, Guid evidenceId, CancellationToken cancellationToken);

    Task AddEvidenceAsync(PaymentEvidence evidence, CancellationToken cancellationToken);
}
