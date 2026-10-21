using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Infrastructure.Persistence;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Persistence;

public sealed class EfCustomerRepositoryTests
{
    [Fact]
    public async Task Find_does_not_return_a_customer_from_another_tenant()
    {
        await using var context = CreateContext();
        var customer = CreateCustomer(Guid.NewGuid(), Guid.NewGuid());
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new EfCustomerRepository(context);
        var result = await repository.FindAsync(Guid.NewGuid(), customer.Id, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task Search_filters_by_tenant_even_when_the_route_is_shared()
    {
        await using var context = CreateContext();
        var routeId = Guid.NewGuid();
        var expected = CreateCustomer(Guid.NewGuid(), routeId);
        var otherTenant = CreateCustomer(Guid.NewGuid(), routeId);
        context.Customers.AddRange(expected, otherTenant);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new EfCustomerRepository(context);
        var result = await repository.SearchAsync(
            expected.TenantId,
            [routeId],
            null,
            null,
            1,
            25,
            default);

        var item = Assert.Single(result.Items);
        Assert.Equal(expected.Id, item.Id);
        Assert.Equal(1, result.TotalCount);
    }

    private static OperationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OperationsDbContext(options);
    }

    private static Customer CreateCustomer(Guid tenantId, Guid routeId) => Customer.Create(
        tenantId,
        routeId,
        "Ana",
        "Pérez",
        null,
        null,
        [new("3001112233", true)],
        [new(4.711m, -74.072m, "Casa", null, true, LocationSource.Manual)],
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}
