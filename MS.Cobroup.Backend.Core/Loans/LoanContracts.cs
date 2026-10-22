using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Core.Loans;

public sealed record OriginateLoanInput(
    Guid CustomerId,
    decimal PrincipalAmount,
    decimal InterestRate,
    PaymentFrequency PaymentFrequency,
    int? FrequencyNDays,
    int InstallmentCount,
    Guid? LoanTemplateId,
    int? LoanTemplateVersion,
    Guid? ExceptionRequestId,
    DateOnly StartDate);

public sealed record InstallmentDetails(
    Guid Id,
    int Number,
    DateOnly DueDate,
    decimal ExpectedAmount,
    decimal PaidAmount,
    InstallmentStatus Status);

public sealed record LoanDetails(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid RouteId,
    string RouteName,
    Guid? LoanTemplateId,
    int? LoanTemplateVersion,
    Guid? ExceptionRequestId,
    LoanStatus Status,
    decimal PrincipalAmount,
    decimal InterestRate,
    decimal InterestAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    PaymentFrequency PaymentFrequency,
    int? FrequencyNDays,
    int InstallmentCount,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset DueAtUtc,
    Guid CreatedByUserId,
    UserRole CreatedByRole,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyCollection<InstallmentDetails> Installments);

public sealed record OriginateLoanCommand(
    CustomerActor Actor,
    string AccessToken,
    OriginateLoanInput Input);

public sealed record GetLoanQuery(
    CustomerActor Actor,
    string AccessToken,
    Guid LoanId);

public sealed record SearchLoansQuery(
    CustomerActor Actor,
    string AccessToken,
    Guid? RouteId,
    Guid? CustomerId,
    LoanStatus? Status,
    int Page,
    int PageSize);

public enum LoanOutcome
{
    Success,
    Created,
    NotFound,
    Forbidden,
    Conflict,
    Invalid,
    Unauthorized,
    Unavailable
}

public enum LoanSearchOutcome
{
    Success,
    Forbidden,
    Unauthorized,
    Unavailable,
    Invalid
}

public sealed class LoanResult
{
    public LoanOutcome Outcome { get; }
    public LoanDetails? Loan { get; }
    public string? ErrorMessage { get; }

    private LoanResult(LoanOutcome outcome, LoanDetails? loan, string? errorMessage)
    {
        Outcome = outcome;
        Loan = loan;
        ErrorMessage = errorMessage;
    }

    public static LoanResult Success(LoanDetails loan) => new(LoanOutcome.Success, loan, null);
    public static LoanResult Created(LoanDetails loan) => new(LoanOutcome.Created, loan, null);
    public static LoanResult NotFound(string message = "Préstamo no encontrado.") => new(LoanOutcome.NotFound, null, message);
    public static LoanResult Forbidden(string message = "Acceso no autorizado.") => new(LoanOutcome.Forbidden, null, message);
    public static LoanResult Conflict(string message) => new(LoanOutcome.Conflict, null, message);
    public static LoanResult Invalid(string message) => new(LoanOutcome.Invalid, null, message);
    public static LoanResult Unauthorized() => new(LoanOutcome.Unauthorized, null, "No autenticado.");
    public static LoanResult Unavailable() => new(LoanOutcome.Unavailable, null, "Servicio no disponible.");
}

public sealed class LoanSearchResult
{
    public LoanSearchOutcome Outcome { get; }
    public LoanSearchPage? Page { get; }
    public string? ErrorMessage { get; }

    private LoanSearchResult(LoanSearchOutcome outcome, LoanSearchPage? page, string? errorMessage)
    {
        Outcome = outcome;
        Page = page;
        ErrorMessage = errorMessage;
    }

    public static LoanSearchResult Success(LoanSearchPage page) => new(LoanSearchOutcome.Success, page, null);
    public static LoanSearchResult Forbidden() => new(LoanSearchOutcome.Forbidden, null, "Acceso denegado a rutas fuera de alcance.");
    public static LoanSearchResult Unauthorized() => new(LoanSearchOutcome.Unauthorized, null, "No autenticado.");
    public static LoanSearchResult Unavailable() => new(LoanSearchOutcome.Unavailable, null, "Servicio no disponible.");
    public static LoanSearchResult Invalid(string message) => new(LoanSearchOutcome.Invalid, null, message);
}

public enum LoanListOutcome
{
    Success,
    NotFound,
    Forbidden,
    Unauthorized,
    Unavailable
}

public sealed class LoanListResult
{
    public LoanListOutcome Outcome { get; }
    public IReadOnlyCollection<LoanDetails>? Loans { get; }
    public string? ErrorMessage { get; }

    private LoanListResult(LoanListOutcome outcome, IReadOnlyCollection<LoanDetails>? loans, string? errorMessage)
    {
        Outcome = outcome;
        Loans = loans;
        ErrorMessage = errorMessage;
    }

    public static LoanListResult Success(IReadOnlyCollection<LoanDetails> loans) => new(LoanListOutcome.Success, loans, null);
    public static LoanListResult NotFound(string message = "Cliente no encontrado.") => new(LoanListOutcome.NotFound, null, message);
    public static LoanListResult Forbidden() => new(LoanListOutcome.Forbidden, null, "Acceso denegado a rutas fuera de alcance.");
    public static LoanListResult Unauthorized() => new(LoanListOutcome.Unauthorized, null, "No autenticado.");
    public static LoanListResult Unavailable() => new(LoanListOutcome.Unavailable, null, "Servicio no disponible.");
}

