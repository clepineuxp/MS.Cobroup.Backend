namespace MS.Cobroup.Backend.Domain.Interfaces.Directories;

public interface IAdministrativeLoanTemplateDirectory
{
    Task<LoanTemplateDirectoryResult> ListAvailableAsync(
        string accessToken,
        CancellationToken cancellationToken);
}

public sealed record AvailableLoanTemplate(
    Guid Id,
    string Name,
    string? Description,
    string Classification,
    string Status,
    int Version,
    int MaxLoanDays,
    decimal MaxPrincipalAmount,
    IReadOnlyCollection<decimal> AllowedPercentages,
    DateTimeOffset UpdatedAtUtc);

public enum LoanTemplateDirectoryOutcome
{
    Success,
    Unauthorized,
    Forbidden,
    Unavailable
}

public sealed record LoanTemplateDirectoryResult(
    LoanTemplateDirectoryOutcome Outcome,
    IReadOnlyCollection<AvailableLoanTemplate> Templates)
{
    public static LoanTemplateDirectoryResult Success(IReadOnlyCollection<AvailableLoanTemplate> templates) =>
        new(LoanTemplateDirectoryOutcome.Success, templates);

    public static LoanTemplateDirectoryResult Unauthorized() => new(LoanTemplateDirectoryOutcome.Unauthorized, []);

    public static LoanTemplateDirectoryResult Forbidden() => new(LoanTemplateDirectoryOutcome.Forbidden, []);

    public static LoanTemplateDirectoryResult Unavailable() => new(LoanTemplateDirectoryOutcome.Unavailable, []);
}
