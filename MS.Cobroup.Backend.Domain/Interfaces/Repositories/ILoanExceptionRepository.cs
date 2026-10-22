using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.LoanExceptions;

namespace MS.Cobroup.Backend.Domain.Interfaces.Repositories;

public interface ILoanExceptionRepository
{
    Task<LoanExceptionRequest?> FindAsync(Guid tenantId, Guid exceptionId, CancellationToken cancellationToken);

    Task AddAsync(LoanExceptionRequest request, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<LoanExceptionSearchPage> SearchAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        Guid? customerId,
        LoanExceptionStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record LoanExceptionSearchPage(
    IReadOnlyCollection<LoanExceptionSearchItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record LoanExceptionSearchItem(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid RouteId,
    decimal ProposedAmount,
    int ProposedDays,
    decimal ProposedPercentage,
    string Reason,
    LoanExceptionStatus Status,
    Guid CreatedByUserId,
    UserRole CreatedByRole,
    DateTimeOffset CreatedAtUtc,
    Guid? DecidedByUserId,
    DateTimeOffset? DecidedAtUtc,
    string? DecisionNotes,
    Guid? CancelledByUserId,
    DateTimeOffset? CancelledAtUtc);
