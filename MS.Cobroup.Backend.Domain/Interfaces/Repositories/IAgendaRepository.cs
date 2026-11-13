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

    Task<IReadOnlyCollection<NonPaymentReason>> GetNonPaymentReasonsAsync(
        Guid tenantId,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<NonPaymentReason?> GetNonPaymentReasonByIdAsync(
        Guid tenantId,
        Guid reasonId,
        CancellationToken cancellationToken);

    Task SaveNonPaymentReasonAsync(
        NonPaymentReason reason,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CustomerNonPaymentRecord>> GetNonPaymentRecordsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        DateOnly businessDate,
        CancellationToken cancellationToken);

    Task SaveNonPaymentRecordAsync(
        CustomerNonPaymentRecord record,
        CancellationToken cancellationToken);

    Task DeleteNonPaymentRecordAsync(
        Guid tenantId,
        Guid routeId,
        Guid customerId,
        Guid? loanId,
        DateOnly businessDate,
        CancellationToken cancellationToken);

    Task<DateOnly?> GetActiveCashBoxBusinessDateAsync(
        Guid tenantId,
        Guid routeId,
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
    IReadOnlyCollection<AgendaInstallmentData> Installments,
    PaymentFrequency PaymentFrequency = PaymentFrequency.Daily);

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
    IReadOnlyCollection<AgendaPaymentData> PaymentsOnDate,
    IReadOnlyCollection<CustomerNonPaymentRecord>? NonPaymentRecords = null);

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
