using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.LoanExceptions;
using MS.Cobroup.Backend.Infrastructure.Persistence;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Persistence;

public sealed class EfLoanExceptionRepositoryTests
{
    [Fact]
    public async Task Find_does_not_return_an_exception_from_another_tenant()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var customer = CreateCustomer(tenantId, routeId);
        context.Customers.Add(customer);

        var exception = LoanExceptionRequest.Create(
            tenantId,
            customer.Id,
            routeId,
            25000m,
            30,
            15m,
            "Solicitud especial",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow);

        context.LoanExceptionRequests.Add(exception);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new EfLoanExceptionRepository(context);
        var otherTenantId = Guid.NewGuid();

        var result = await repository.FindAsync(otherTenantId, exception.Id, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task Search_filters_by_tenant_and_accessible_routes()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var routeA = Guid.NewGuid();
        var routeB = Guid.NewGuid();

        var customerA = CreateCustomer(tenantId, routeA);
        var customerB = CreateCustomer(tenantId, routeB);
        var customerOther = CreateCustomer(Guid.NewGuid(), routeA);
        context.Customers.AddRange(customerA, customerB, customerOther);

        var exceptionRouteA = LoanExceptionRequest.Create(
            tenantId,
            customerA.Id,
            routeA,
            10000m,
            15,
            10m,
            "Ruta A",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow);

        var exceptionRouteB = LoanExceptionRequest.Create(
            tenantId,
            customerB.Id,
            routeB,
            20000m,
            20,
            12m,
            "Ruta B",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow);

        var otherTenantException = LoanExceptionRequest.Create(
            customerOther.TenantId,
            customerOther.Id,
            routeA,
            30000m,
            25,
            15m,
            "Otro tenant",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow);

        context.LoanExceptionRequests.AddRange(exceptionRouteA, exceptionRouteB, otherTenantException);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new EfLoanExceptionRepository(context);

        // Collector only has access to routeA
        var searchResult = await repository.SearchAsync(
            tenantId,
            [routeA],
            null,
            null,
            null,
            1,
            10,
            default);

        var item = Assert.Single(searchResult.Items);
        Assert.Equal(exceptionRouteA.Id, item.Id);
        Assert.Equal("Juan Pérez", item.CustomerName);
        Assert.Equal(1, searchResult.TotalCount);
    }

    [Fact]
    public async Task Search_filters_by_status()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();

        var customer = CreateCustomer(tenantId, routeId);
        context.Customers.Add(customer);

        var pending = LoanExceptionRequest.Create(
            tenantId,
            customer.Id,
            routeId,
            10000m,
            15,
            10m,
            "Pendiente",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow);

        var approved = LoanExceptionRequest.Create(
            tenantId,
            customer.Id,
            routeId,
            20000m,
            20,
            12m,
            "Aprobada",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow);
        approved.ApplyDecision(LoanExceptionDecision.Approved, Guid.NewGuid(), "Aprobado por Admin", DateTimeOffset.UtcNow);

        context.LoanExceptionRequests.AddRange(pending, approved);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new EfLoanExceptionRepository(context);

        var searchPending = await repository.SearchAsync(
            tenantId,
            [routeId],
            null,
            null,
            LoanExceptionStatus.Pending,
            1,
            10,
            default);

        var item = Assert.Single(searchPending.Items);
        Assert.Equal(pending.Id, item.Id);
        Assert.Equal(LoanExceptionStatus.Pending, item.Status);
    }

    private static Customer CreateCustomer(Guid tenantId, Guid routeId) =>
        Customer.Create(
            tenantId,
            routeId,
            "Juan",
            "Pérez",
            "Comercio Juan",
            null,
            [new("3001112233", true)],
            [new(4.711m, -74.072m, "Local Principal", null, true, LocationSource.Manual)],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow, "001234567");

    private static OperationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OperationsDbContext(options);
    }
}
