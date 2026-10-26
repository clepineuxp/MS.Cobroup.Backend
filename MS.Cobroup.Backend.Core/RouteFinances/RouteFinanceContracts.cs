using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Domain.RouteFinances;

namespace MS.Cobroup.Backend.Core.RouteFinances;

public enum RouteFinanceOutcome
{
    Success,
    Created,
    Invalid,
    NotFound,
    Forbidden,
    Unauthorized,
    Unavailable
}

public sealed record RouteBalanceDetails(
    Guid RouteId,
    string RouteName,
    decimal InitialBalance,
    decimal LoanDisbursements,
    decimal PaymentCollections,
    decimal IncomeInvestments,
    decimal Expenses,
    decimal NetBalance);

public sealed record RouteFinancialEntryDetails(
    Guid Id,
    Guid RouteId,
    RouteFinancialEntryType Type,
    string Concept,
    string? Description,
    decimal Amount,
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    DateTimeOffset CreatedAtUtc,
    Guid UpdatedByUserId,
    string UpdatedByDisplayName,
    DateTimeOffset UpdatedAtUtc,
    bool CanEdit);

public sealed record SaveRouteFinancialEntryInput(
    Guid RouteId,
    RouteFinancialEntryType Type,
    string Concept,
    string? Description,
    decimal Amount);

public sealed record DayClosingStatusDetails(
    Guid RouteId,
    string RouteName,
    DateOnly BusinessDate,
    bool IsClosed,
    DateTimeOffset? ClosedAtUtc,
    string? ClosedByDisplayName,
    DateTimeOffset? ReopenedAtUtc,
    string? ReopenedByDisplayName,
    bool CanReopen);

public sealed record FinancialMovementAuditLogDetails(
    Guid Id,
    Guid RouteId,
    FinancialMovementEntityType EntityType,
    Guid EntityId,
    FinancialMovementAuditAction Action,
    string Concept,
    decimal Amount,
    string Details,
    Guid PerformedByUserId,
    string PerformedByDisplayName,
    string PerformedByUserRole,
    DateTimeOffset TimestampUtc,
    string TimestampFormatted);

public sealed record RouteFinanceResult(
    RouteFinanceOutcome Outcome,
    RouteBalanceDetails? Balance = null,
    IReadOnlyCollection<RouteFinancialEntryDetails>? Entries = null,
    RouteFinancialEntryDetails? Entry = null,
    DayClosingStatusDetails? ClosingStatus = null,
    IReadOnlyCollection<FinancialMovementAuditLogDetails>? AuditLogs = null,
    string? Error = null)
{
    public static RouteFinanceResult Invalid(string error) => new(RouteFinanceOutcome.Invalid, Error: error);
    public static RouteFinanceResult NotFound() => new(RouteFinanceOutcome.NotFound);
    public static RouteFinanceResult Forbidden(string? error = null) => new(RouteFinanceOutcome.Forbidden, Error: error);
    public static RouteFinanceResult Unauthorized() => new(RouteFinanceOutcome.Unauthorized);
    public static RouteFinanceResult Unavailable() => new(RouteFinanceOutcome.Unavailable);
}

public sealed record RouteFinanceCommand(
    CustomerActor Actor,
    string ActorDisplayName,
    string AccessToken);
