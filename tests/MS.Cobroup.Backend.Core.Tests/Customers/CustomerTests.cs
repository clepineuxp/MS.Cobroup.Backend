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
            DateTimeOffset.UtcNow, "001234567"));

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
            DateTimeOffset.UtcNow, "001234567"));
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
            DateTimeOffset.UtcNow, "001234567"));
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
            DateTimeOffset.UtcNow, "001234567"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    public void Create_rejects_missing_or_reserved_identification(string? identification)
    {
        Assert.Throws<ArgumentException>(() => CreateValid(identification!));
    }

    [Fact]
    public void Identification_preserves_leading_zeros_and_trims_spaces()
    {
        var customer = CreateValid("  001234567  ");
        Assert.Equal("001234567", customer.Identification);
        Assert.Throws<ArgumentException>(() => CreateValid(new string('A', 65)));
    }

    [Fact]
    public void Legacy_zero_can_be_retained_or_corrected_but_not_assigned_to_identified_customers()
    {
        var customer = CreateValid();
        typeof(Customer).GetProperty(nameof(Customer.Identification))!.SetValue(customer, "0");
        UpdateIdentification(customer, "0");
        Assert.Equal("0", customer.Identification);
        UpdateIdentification(customer, "001001");
        Assert.Equal("001001", customer.Identification);
        Assert.Throws<ArgumentException>(() => UpdateIdentification(customer, "0"));
        Assert.Throws<ArgumentException>(() => UpdateIdentification(customer, ""));
    }

    private static void UpdateIdentification(Customer customer, string identification) => customer.Update(
        customer.RouteId, customer.FirstNames, customer.LastNames, customer.BusinessOrWorkName, null,
        customer.Status, [new("3001112233", true)],
        [new(4.711m, -74.072m, null, null, true, LocationSource.Manual)],
        Guid.NewGuid(), DateTimeOffset.UtcNow, identification);

    private static Customer CreateValid(string identification = "001234567") => Customer.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        " Ana ",
        " Pérez ",
        " Taller ",
        null,
        [new("+57 300-111-2233", true)],
        [new(4.711m, -74.072m, " Casa ", "Bogotá", true, LocationSource.Gps)],
        Guid.NewGuid(),
        DateTimeOffset.UtcNow, identification);
}
