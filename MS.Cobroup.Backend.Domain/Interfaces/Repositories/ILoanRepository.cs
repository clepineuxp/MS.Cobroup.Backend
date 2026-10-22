using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Domain.Interfaces.Repositories;

public interface ILoanRepository
{
    Task<Loan?> FindAsync(Guid tenantId, Guid loanId, CancellationToken cancellationToken);

    Task AddAsync(Loan loan, CancellationToken cancellationToken);

    Task<int> CountActiveLoansByRouteAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Loan>> ListByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken);

    Task<LoanSearchPage> SearchAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        Guid? customerId,
        LoanStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record LoanSearchPage(
    IReadOnlyCollection<LoanSearchItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record LoanSearchItem(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid RouteId,
    decimal PrincipalAmount,
    decimal InterestRate,
    decimal TotalAmount,
    decimal BalanceAmount,
    PaymentFrequency PaymentFrequency,
    int InstallmentCount,
    LoanStatus Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset DueAtUtc,
    DateTimeOffset CreatedAtUtc);
