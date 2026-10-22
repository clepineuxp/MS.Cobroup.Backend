namespace MS.Cobroup.Backend.Domain.Interfaces.Directories;

public interface IOperationalPaymentMethodDirectory
{
    Task<PaymentMethodDirectoryResult> ListAvailableAsync(
        string accessToken,
        CancellationToken cancellationToken);
}

public sealed record OperationalPaymentMethodItem(
    Guid Id,
    string Name,
    int DisplayOrder);

public sealed record PaymentMethodDirectoryResult(
    PaymentMethodDirectoryOutcome Outcome,
    IReadOnlyCollection<OperationalPaymentMethodItem> Items)
{
    public static PaymentMethodDirectoryResult Success(IReadOnlyCollection<OperationalPaymentMethodItem> items) =>
        new(PaymentMethodDirectoryOutcome.Success, items);

    public static PaymentMethodDirectoryResult Unauthorized() =>
        new(PaymentMethodDirectoryOutcome.Unauthorized, []);

    public static PaymentMethodDirectoryResult Forbidden() =>
        new(PaymentMethodDirectoryOutcome.Forbidden, []);

    public static PaymentMethodDirectoryResult Unavailable() =>
        new(PaymentMethodDirectoryOutcome.Unavailable, []);
}

public enum PaymentMethodDirectoryOutcome
{
    Success,
    Unauthorized,
    Forbidden,
    Unavailable
}
