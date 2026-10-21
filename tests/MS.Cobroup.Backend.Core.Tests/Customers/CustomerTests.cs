using MS.Cobroup.Backend.Domain.Customers;

namespace MS.Cobroup.Backend.Core.Tests.Customers;

public sealed class CustomerTests
{
    [Fact]
    public void Create_normalizes_contact_data_and_requires_complete_customer()
    {
        var customer = CreateValid();

        Assert.Equal("Ana", customer.FirstNames);
        Assert.Equal("Pérez", customer.LastNames);
        Assert.Equal("+573001112233", customer.Phones.Single().Number);
        Assert.Single(customer.Locations);
        Assert.Equal(CustomerStatus.Active, customer.Status);
    }

    [Fact]
    public void Create_rejects_duplicated_normalized_phones()
    {
        var exception = Assert.Throws<ArgumentException>(() => Customer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ana",
            "Pérez",
            null,
            null,
            [new("300 111 2233", true), new("300-111-2233", false)],
            [new(4.711m, -74.072m, "Casa", null, true, LocationSource.Manual)],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.Contains("duplicated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_rejects_missing_primary_location()
    {
        Assert.Throws<ArgumentException>(() => Customer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ana",
            "Pérez",
            null,
            null,
            [new("3001112233", true)],
            [new(4.711m, -74.072m, "Casa", null, false, LocationSource.Manual)],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_rejects_out_of_range_coordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Customer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ana",
            "Pérez",
            null,
            null,
            [new("3001112233", true)],
            [new(91m, -74.072m, "Casa", null, true, LocationSource.Gps)],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_rejects_letters_in_phone_numbers()
    {
        Assert.Throws<ArgumentException>(() => Customer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ana",
            "Pérez",
            null,
            null,
            [new("abc3001112233", true)],
            [new(4.711m, -74.072m, "Casa", null, true, LocationSource.Manual)],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
    }

    private static Customer CreateValid() => Customer.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        " Ana ",
        " Pérez ",
        " Taller ",
        null,
        [new("+57 300-111-2233", true)],
        [new(4.711m, -74.072m, " Casa ", "Bogotá", true, LocationSource.Gps)],
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}
