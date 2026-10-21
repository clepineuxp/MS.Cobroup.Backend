namespace MS.Cobroup.Backend.Domain.Customers;

public sealed class CustomerLocation
{
    private CustomerLocation()
    {
    }

    private CustomerLocation(
        Guid customerId,
        decimal latitude,
        decimal longitude,
        string? alias,
        string? address,
        bool isPrimary,
        LocationSource source)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
        }

        Id = Guid.NewGuid();
        CustomerId = customerId;
        Latitude = latitude;
        Longitude = longitude;
        Alias = NormalizeOptional(alias, nameof(alias), 120);
        Address = NormalizeOptional(address, nameof(address), 300);
        IsPrimary = isPrimary;
        Source = source;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    public string? Alias { get; private set; }

    public string? Address { get; private set; }

    public bool IsPrimary { get; private set; }

    public LocationSource Source { get; private set; }

    internal static CustomerLocation Create(
        Guid customerId,
        decimal latitude,
        decimal longitude,
        string? alias,
        string? address,
        bool isPrimary,
        LocationSource source) =>
        new(customerId, latitude, longitude, alias, address, isPrimary, source);

    private static string? NormalizeOptional(string? value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }
}
