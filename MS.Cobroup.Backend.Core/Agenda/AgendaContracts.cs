using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Domain.Agenda;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;

namespace MS.Cobroup.Backend.Core.Agenda;

public sealed record AgendaItemDto(
    Guid CustomerId,
    string CustomerName,
    string? BusinessOrWorkName,
    string? PrimaryPhone,
    Guid RouteId,
    decimal? Latitude,
    decimal? Longitude,
    string? Address,
    string? Alias,
    Guid? ActiveLoanId,
    decimal LoanBalance,
    decimal OverdueAmount,
    int DaysOverdue,
    decimal DueTodayAmount,
    decimal CollectedTodayAmount,
    AgendaStatus Status,
    string StatusLabel,
    string MarkerColor,
    decimal? DistanceKm,
    decimal InstallmentAmount = 0m,
    int OverdueInstallmentsCount = 0,
    string? PaymentFrequency = null,
    Guid? NonPaymentReasonId = null,
    string? NonPaymentReasonName = null,
    string? NonPaymentObservations = null);

public sealed record AgendaSummaryDto(
    DateOnly TargetDate,
    Guid? RouteId,
    int TotalCustomers,
    decimal TotalDueToday,
    decimal TotalOverdue,
    decimal TotalCollectedToday,
    int OverdueCount,
    int DueTodayCount,
    int CollectedCount,
    int UpToDateCount,
    int WillNotPayCount = 0);

public sealed record AgendaResponse(
    AgendaSummaryDto Summary,
    IReadOnlyCollection<AgendaItemDto> Items);

public sealed record CustomerMapLocationDto(
    Guid CustomerId,
    string CustomerName,
    string? BusinessOrWorkName,
    string? PrimaryPhone,
    Guid RouteId,
    decimal Latitude,
    decimal Longitude,
    string? Address,
    string? Alias,
    Guid? ActiveLoanId,
    decimal LoanBalance,
    decimal OverdueAmount,
    decimal DueTodayAmount,
    AgendaStatus Status,
    string MarkerColor);

public sealed record PortfolioAnalyticsResponse(
    DateOnly TargetDate,
    int TotalActiveLoans,
    int TotalActiveCustomers,
    decimal TotalPlacedAmount,
    decimal TotalCollectedAmount,
    decimal TotalBalanceAmount,
    decimal TotalOverdueAmount,
    decimal PortfolioAtRiskRate,
    decimal DailyCollectionEfficiencyRate,
    IReadOnlyCollection<AgingBucketData> AgingBuckets,
    IReadOnlyCollection<RoutePortfolioData> RoutesPerformance);

public sealed record GetAgendaQuery(
    CustomerActor Actor,
    string AccessToken,
    Guid? RouteId,
    DateOnly? TargetDate,
    decimal? OriginLatitude,
    decimal? OriginLongitude);

public sealed record GetMapLocationsQuery(
    CustomerActor Actor,
    string AccessToken,
    Guid? RouteId,
    DateOnly? TargetDate);

public sealed record GetRouteAnalyticsQuery(
    CustomerActor Actor,
    string AccessToken,
    Guid? RouteId,
    DateOnly? TargetDate);

public sealed record OptimizeSequenceInput(
    Guid? RouteId,
    DateOnly? TargetDate,
    decimal OriginLatitude,
    decimal OriginLongitude);

public sealed record OptimizeSequenceCommand(
    CustomerActor Actor,
    string AccessToken,
    OptimizeSequenceInput Input);

public enum AgendaOutcome
{
    Success,
    Forbidden,
    Unauthorized,
    Unavailable,
    Invalid
}

public sealed class AgendaResult<T>
{
    public AgendaOutcome Outcome { get; }
    public T? Data { get; }
    public string? ErrorMessage { get; }

    private AgendaResult(AgendaOutcome outcome, T? data, string? errorMessage)
    {
        Outcome = outcome;
        Data = data;
        ErrorMessage = errorMessage;
    }

    public static AgendaResult<T> Success(T data) => new(AgendaOutcome.Success, data, null);
    public static AgendaResult<T> Forbidden(string message = "Acceso denegado a las rutas solicitadas.") => new(AgendaOutcome.Forbidden, default, message);
    public static AgendaResult<T> Unauthorized(string message = "No autenticado.") => new(AgendaOutcome.Unauthorized, default, message);
    public static AgendaResult<T> Unavailable(string message = "Servicio no disponible.") => new(AgendaOutcome.Unavailable, default, message);
    public static AgendaResult<T> Invalid(string message) => new(AgendaOutcome.Invalid, default, message);
}

public sealed record NonPaymentReasonDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int DisplayOrder);

public sealed record CreateNonPaymentReasonInput(
    string Name,
    string? Description,
    int DisplayOrder = 0);

public sealed record UpdateNonPaymentReasonInput(
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive);

public sealed record CreateNonPaymentReasonCommand(
    CustomerActor Actor,
    CreateNonPaymentReasonInput Input);

public sealed record ToggleReasonStatusInput(bool IsActive);

public sealed record ToggleNonPaymentReasonStatusCommand(
    CustomerActor Actor,
    Guid ReasonId,
    bool IsActive);

public sealed record RegisterWillNotPayInput(
    Guid RouteId,
    Guid CustomerId,
    Guid? LoanId,
    DateOnly BusinessDate,
    Guid? ReasonId,
    string? ReasonName,
    string? Observations);

public sealed record RegisterWillNotPayCommand(
    CustomerActor Actor,
    string AccessToken,
    RegisterWillNotPayInput Input);

public sealed record CancelWillNotPayInput(
    Guid RouteId,
    Guid CustomerId,
    Guid? LoanId,
    DateOnly BusinessDate);

public sealed record CancelWillNotPayCommand(
    CustomerActor Actor,
    string AccessToken,
    CancelWillNotPayInput Input);
