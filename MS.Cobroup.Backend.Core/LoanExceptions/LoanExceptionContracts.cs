using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.LoanExceptions;

namespace MS.Cobroup.Backend.Core.LoanExceptions;

public sealed record CreateLoanExceptionInput(
    Guid CustomerId,
    decimal ProposedAmount,
    int ProposedDays,
    decimal ProposedPercentage,
    string Reason);

public sealed record ApplyLoanExceptionDecisionInput(
    LoanExceptionDecision Decision,
    string? DecisionNotes);

public sealed record CreateLoanExceptionCommand(
    CustomerActor Actor,
    string AccessToken,
    CreateLoanExceptionInput Input);

public sealed record GetLoanExceptionQuery(
    CustomerActor Actor,
    string AccessToken,
    Guid ExceptionId);

public sealed record SearchLoanExceptionsQuery(
    CustomerActor Actor,
    string AccessToken,
    Guid? RouteId,
    Guid? CustomerId,
    LoanExceptionStatus? Status,
    int Page,
    int PageSize);

public sealed record CancelLoanExceptionCommand(
    CustomerActor Actor,
    string AccessToken,
    Guid ExceptionId);

public sealed record ApplyLoanExceptionDecisionCommand(
    CustomerActor Actor,
    Guid ExceptionId,
    ApplyLoanExceptionDecisionInput Input);

public sealed record LoanExceptionDetails(
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

public enum LoanExceptionOutcome
{
    Success,
    Created,
    Invalid,
    NotFound,
    Forbidden,
    Unauthorized,
    Conflict,
    Unavailable
}

public sealed record LoanExceptionResult(
    LoanExceptionOutcome Outcome,
    LoanExceptionDetails? Exception,
    string? Error)
{
    public static LoanExceptionResult Success(LoanExceptionDetails exception) =>
        new(LoanExceptionOutcome.Success, exception, null);

    public static LoanExceptionResult Created(LoanExceptionDetails exception) =>
        new(LoanExceptionOutcome.Created, exception, null);

    public static LoanExceptionResult Invalid(string error) =>
        new(LoanExceptionOutcome.Invalid, null, error);

    public static LoanExceptionResult NotFound() =>
        new(LoanExceptionOutcome.NotFound, null, null);

    public static LoanExceptionResult Forbidden() =>
        new(LoanExceptionOutcome.Forbidden, null, null);

    public static LoanExceptionResult Unauthorized() =>
        new(LoanExceptionOutcome.Unauthorized, null, null);

    public static LoanExceptionResult Conflict(string error) =>
        new(LoanExceptionOutcome.Conflict, null, error);

    public static LoanExceptionResult Unavailable() =>
        new(LoanExceptionOutcome.Unavailable, null, null);
}

public sealed record LoanExceptionSearchResult(
    LoanExceptionOutcome Outcome,
    LoanExceptionSearchPage? Page,
    string? Error)
{
    public static LoanExceptionSearchResult Success(LoanExceptionSearchPage page) =>
        new(LoanExceptionOutcome.Success, page, null);

    public static LoanExceptionSearchResult Invalid(string error) =>
        new(LoanExceptionOutcome.Invalid, null, error);

    public static LoanExceptionSearchResult Forbidden() =>
        new(LoanExceptionOutcome.Forbidden, null, null);

    public static LoanExceptionSearchResult Unauthorized() =>
        new(LoanExceptionOutcome.Unauthorized, null, null);

    public static LoanExceptionSearchResult Unavailable() =>
        new(LoanExceptionOutcome.Unavailable, null, null);
}
