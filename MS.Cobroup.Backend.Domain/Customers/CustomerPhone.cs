namespace MS.Cobroup.Backend.Domain.Customers;

public sealed class CustomerPhone
{
    private CustomerPhone()
    {
    }

    private CustomerPhone(Guid customerId, string number, bool isPrimary)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Number = Normalize(number);
        IsPrimary = isPrimary;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public bool IsPrimary { get; private set; }

    internal static CustomerPhone Create(Guid customerId, string number, bool isPrimary) =>
        new(customerId, number, isPrimary);

    public static string Normalize(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        var normalized = string.Concat(trimmed.Where((character, index) =>
            char.IsDigit(character) || (character == '+' && index == 0)));
        var digitCount = normalized.Count(char.IsDigit);
        if (digitCount is < 7 or > 20 || normalized.Length > 21)
        {
            throw new ArgumentException("A phone must contain between 7 and 20 digits.", nameof(value));
        }

        return normalized;
    }
}
