using MS.Cobroup.Backend.Domain.Agenda;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Domain.Interfaces.Repositories;

public interface IAgendaRepository
{
    Task<IReadOnlyCollection<AgendaCustomerData>> GetAgendaCustomersAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        DateOnly targetDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RoutePortfolioData>> GetRoutePortfolioDataAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        DateOnly targetDate,
        CancellationToken cancellationToken);
}

public sealed record AgendaLocationData(
    decimal Latitude,
    decimal Longitude,
    string? Address,
    string? Alias);

public sealed record AgendaInstallmentData(
    Guid Id,
    int Number,
    DateOnly DueDate,
    decimal ExpectedAmount,
    decimal PaidAmount,
    InstallmentStatus Status);

public sealed record AgendaLoanData(
    Guid LoanId,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    int InstallmentCount,
    IReadOnlyCollection<AgendaInstallmentData> Installments);

public sealed record AgendaPaymentData(
    Guid PaymentId,
    Guid LoanId,
    decimal Amount,
    DateTimeOffset RegisteredAtUtc,
    string? PaymentMethodName);

public sealed record AgendaCustomerData(
    Guid CustomerId,
    string CustomerName,
    string? BusinessOrWorkName,
    string? PrimaryPhone,
    Guid RouteId,
    AgendaLocationData? Location,
    IReadOnlyCollection<AgendaLoanData> ActiveLoans,
    IReadOnlyCollection<AgendaPaymentData> PaymentsOnDate);

public sealed record AgingBucketData(
    string BucketKey,
    string Label,
    int MinDays,
    int? MaxDays,
    decimal TotalAmount,
    int LoanCount);

public sealed record RoutePortfolioData(
    Guid RouteId,
    int ActiveLoansCount,
    int ActiveCustomersCount,
    decimal TotalPlacedAmount,
    decimal TotalCollectedAmount,
    decimal TotalBalanceAmount,
    decimal OverdueBalanceAmount,
    decimal PortfolioAtRiskRate,
    IReadOnlyCollection<AgingBucketData> AgingBuckets);
