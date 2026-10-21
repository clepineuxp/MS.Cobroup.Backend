using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;

namespace MS.Cobroup.Backend.Core.Tests.Customers;

public sealed class CustomerServiceTests
{
    [Fact]
    public async Task Create_rejects_a_route_outside_the_actor_scope()
    {
        var fixture = new Fixture(UserRole.Collector);

        var result = await fixture.Service.CreateAsync(fixture.CreateCommand(Guid.NewGuid()), default);

        Assert.Equal(CustomerOutcome.Forbidden, result.Outcome);
        Assert.Null(fixture.Repository.SavedCustomer);
    }

    [Fact]
    public async Task Create_propagates_route_capacity()
    {
        var fixture = new Fixture(UserRole.Collector);
        fixture.Repository.NextSaveOutcome = CustomerSaveOutcome.CapacityReached;

        var result = await fixture.Service.CreateAsync(fixture.CreateCommand(fixture.RouteId), default);

        Assert.Equal(CustomerOutcome.CapacityReached, result.Outcome);
        Assert.Equal(3, fixture.Repository.LastMaximum);
    }

    [Fact]
    public async Task Create_returns_the_persisted_customer()
    {
        var fixture = new Fixture(UserRole.Collector);

        var result = await fixture.Service.CreateAsync(fixture.CreateCommand(fixture.RouteId), default);

        Assert.Equal(CustomerOutcome.Created, result.Outcome);
        Assert.Equal(fixture.RouteId, result.Customer!.RouteId);
        Assert.NotNull(fixture.Repository.SavedCustomer);
    }

    [Fact]
    public async Task Get_hides_a_customer_from_an_unassigned_route()
    {
        var fixture = new Fixture(UserRole.Collector);
        fixture.Repository.Customer = fixture.CreateCustomer(Guid.NewGuid());

        var result = await fixture.Service.GetAsync(
            new GetCustomerQuery(fixture.Actor, "token", fixture.Repository.Customer.Id),
            default);

        Assert.Equal(CustomerOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_prevents_non_admin_route_transfer()
    {
        var fixture = new Fixture(UserRole.Supervisor, includeSecondRoute: true);
        fixture.Repository.Customer = fixture.CreateCustomer(fixture.RouteId);

        var result = await fixture.Service.UpdateAsync(
            fixture.UpdateCommand(fixture.Repository.Customer.Id, fixture.SecondRouteId),
            default);

        Assert.Equal(CustomerOutcome.Forbidden, result.Outcome);
    }

    [Fact]
    public async Task Search_rejects_an_explicit_inaccessible_route()
    {
        var fixture = new Fixture(UserRole.Collector);

        var result = await fixture.Service.SearchAsync(
            new SearchCustomersQuery(fixture.Actor, "token", Guid.NewGuid(), null, 1, 25),
            default);

        Assert.Equal(CustomerOutcome.Forbidden, result.Outcome);
    }

    private sealed class Fixture
    {
        public Fixture(UserRole role, bool includeSecondRoute = false)
        {
            Actor = new CustomerActor(Guid.NewGuid(), Guid.NewGuid(), role);
            var routes = new List<AccessibleRoute>
            {
                new(RouteId, "Centro", null, role.ToString(), 3, null)
            };
            if (includeSecondRoute)
            {
                routes.Add(new AccessibleRoute(SecondRouteId, "Norte", null, role.ToString(), 2, null));
            }

            Service = new CustomerService(Repository, new FakeRouteDirectory(RouteDirectoryResult.Success(routes)));
        }

        public Guid RouteId { get; } = Guid.NewGuid();

        public Guid SecondRouteId { get; } = Guid.NewGuid();

        public CustomerActor Actor { get; }

        public FakeCustomerRepository Repository { get; } = new();

        public CustomerService Service { get; }

        public CreateCustomerCommand CreateCommand(Guid routeId) => new(
            Actor,
            "token",
            routeId,
            "Ana",
            "Pérez",
            null,
            null,
            [new("3001112233", true)],
            [new(4.711m, -74.072m, "Casa", null, true, LocationSource.Manual)]);

        public UpdateCustomerCommand UpdateCommand(Guid customerId, Guid routeId) => new(
            Actor,
            "token",
            customerId,
            routeId,
            "Ana",
            "Pérez",
            null,
            null,
            CustomerStatus.Active,
            [new("3001112233", true)],
            [new(4.711m, -74.072m, "Casa", null, true, LocationSource.Manual)]);

        public Customer CreateCustomer(Guid routeId) => Customer.Create(
            Actor.TenantId,
            routeId,
            "Ana",
            "Pérez",
            null,
            null,
            [new("3001112233", true)],
            [new(4.711m, -74.072m, "Casa", null, true, LocationSource.Manual)],
            Actor.UserId,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeRouteDirectory(RouteDirectoryResult result) : IAdministrativeRouteDirectory
    {
        public Task<RouteDirectoryResult> ListAccessibleAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public Customer? Customer { get; set; }

        public Customer? SavedCustomer { get; private set; }

        public CustomerSaveOutcome NextSaveOutcome { get; set; } = CustomerSaveOutcome.Saved;

        public int? LastMaximum { get; private set; }

        public Task<CustomerSearchPage> SearchAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> accessibleRouteIds,
            Guid? routeId,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CustomerSearchPage([], page, pageSize, 0));

        public Task<Customer?> FindAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult(Customer?.Id == customerId ? Customer : null);

        public Task<CustomerSaveOutcome> TryAddAsync(
            Customer customer,
            int? maxActiveCustomers,
            CancellationToken cancellationToken)
        {
            SavedCustomer = customer;
            LastMaximum = maxActiveCustomers;
            return Task.FromResult(NextSaveOutcome);
        }

        public Task<CustomerSaveOutcome> TryUpdateAsync(
            Customer customer,
            Guid previousRouteId,
            CustomerStatus previousStatus,
            int? maxActiveCustomers,
            CancellationToken cancellationToken)
        {
            SavedCustomer = customer;
            LastMaximum = maxActiveCustomers;
            return Task.FromResult(NextSaveOutcome);
        }
    }
}
