using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.LoanExceptions;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.LoanExceptions;

namespace MS.Cobroup.Backend.Core.Tests.LoanExceptions;

public sealed class LoanExceptionServiceTests
{
    private readonly InMemoryLoanExceptionRepository exceptionRepository = new();
    private readonly InMemoryCustomerRepository customerRepository = new();
    private readonly FakeRouteDirectory routeDirectory = new();
    private readonly LoanExceptionService service;

    public LoanExceptionServiceTests()
    {
        service = new LoanExceptionService(exceptionRepository, customerRepository, routeDirectory);
    }

    [Fact]
    public async Task CreateAsync_creates_exception_for_active_customer_in_accessible_route()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();
        routeDirectory.Routes = [new(routeId, "Ruta 1", null, "Collector", 10, 10)];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var input = new CreateLoanExceptionInput(customer.Id, 50000m, 20, 15m, "Solicitud para cliente VIP");

        var result = await service.CreateAsync(
            new CreateLoanExceptionCommand(actor, "token", input),
            CancellationToken.None);

        Assert.Equal(LoanExceptionOutcome.Created, result.Outcome);
        Assert.NotNull(result.Exception);
        Assert.Equal(customer.Id, result.Exception.CustomerId);
        Assert.Equal(50000m, result.Exception.ProposedAmount);
        Assert.Equal(LoanExceptionStatus.Pending, result.Exception.Status);
    }

    [Fact]
    public async Task CreateAsync_rejects_customer_in_route_outside_scope()
    {
        var tenantId = Guid.NewGuid();
        var allowedRouteId = Guid.NewGuid();
        var otherRouteId = Guid.NewGuid();
        routeDirectory.Routes = [new(allowedRouteId, "Ruta 1", null, "Collector", 10, 10)];

        var customerInOtherRoute = CreateCustomer(tenantId, otherRouteId);
        customerRepository.Add(customerInOtherRoute);

        var actor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Collector);
        var input = new CreateLoanExceptionInput(customerInOtherRoute.Id, 50000m, 20, 15m, "Solicitud");

        var result = await service.CreateAsync(
            new CreateLoanExceptionCommand(actor, "token", input),
            CancellationToken.None);

        Assert.Equal(LoanExceptionOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task ApplyDecisionAsync_forbids_non_admin_actors_satisfying_AC08()
    {
        var tenantId = Guid.NewGuid();
        var exception = LoanExceptionRequest.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            20000m,
            15,
            10m,
            "Motivo",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow);
        await exceptionRepository.AddAsync(exception, CancellationToken.None);

        var collectorActor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Collector);
        var supervisorActor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Supervisor);

        var collectorResult = await service.ApplyDecisionAsync(
            new ApplyLoanExceptionDecisionCommand(collectorActor, exception.Id, new(LoanExceptionDecision.Approved, "Aprobado")),
            CancellationToken.None);

        var supervisorResult = await service.ApplyDecisionAsync(
            new ApplyLoanExceptionDecisionCommand(supervisorActor, exception.Id, new(LoanExceptionDecision.Approved, "Aprobado")),
            CancellationToken.None);

        Assert.Equal(LoanExceptionOutcome.Forbidden, collectorResult.Outcome);
        Assert.Equal(LoanExceptionOutcome.Forbidden, supervisorResult.Outcome);
        Assert.Equal(LoanExceptionStatus.Pending, exception.Status);
    }

    [Fact]
    public async Task ApplyDecisionAsync_allows_admin_to_approve_and_is_idempotent()
    {
        var tenantId = Guid.NewGuid();
        var customer = CreateCustomer(tenantId, Guid.NewGuid());
        customerRepository.Add(customer);

        var exception = LoanExceptionRequest.Create(
            tenantId,
            customer.Id,
            customer.RouteId,
            30000m,
            25,
            12m,
            "Excepción solicitada",
            Guid.NewGuid(),
            UserRole.Collector,
            DateTimeOffset.UtcNow);
        await exceptionRepository.AddAsync(exception, CancellationToken.None);

        var adminId = Guid.NewGuid();
        var adminActor = new CustomerActor(adminId, tenantId, UserRole.Admin);

        var result = await service.ApplyDecisionAsync(
            new ApplyLoanExceptionDecisionCommand(adminActor, exception.Id, new(LoanExceptionDecision.Approved, "Aprobado por comité")),
            CancellationToken.None);

        Assert.Equal(LoanExceptionOutcome.Success, result.Outcome);
        Assert.NotNull(result.Exception);
        Assert.Equal(LoanExceptionStatus.Approved, result.Exception.Status);
        Assert.Equal(adminId, result.Exception.DecidedByUserId);

        // Idempotent re-application
        var idempotentResult = await service.ApplyDecisionAsync(
            new ApplyLoanExceptionDecisionCommand(adminActor, exception.Id, new(LoanExceptionDecision.Approved, "Aprobado por comité")),
            CancellationToken.None);

        Assert.Equal(LoanExceptionOutcome.Success, idempotentResult.Outcome);
        Assert.Equal(LoanExceptionStatus.Approved, idempotentResult.Exception!.Status);
    }

    [Fact]
    public async Task CancelAsync_allows_creator_to_cancel_pending_request()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        routeDirectory.Routes = [new(routeId, "Ruta 1", null, "Collector", 10, 10)];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        var creatorId = Guid.NewGuid();
        var exception = LoanExceptionRequest.Create(
            tenantId,
            customer.Id,
            routeId,
            15000m,
            10,
            10m,
            "Motivo",
            creatorId,
            UserRole.Collector,
            DateTimeOffset.UtcNow);
        await exceptionRepository.AddAsync(exception, CancellationToken.None);

        var creatorActor = new CustomerActor(creatorId, tenantId, UserRole.Collector);

        var result = await service.CancelAsync(
            new CancelLoanExceptionCommand(creatorActor, "token", exception.Id),
            CancellationToken.None);

        Assert.Equal(LoanExceptionOutcome.Success, result.Outcome);
        Assert.Equal(LoanExceptionStatus.Cancelled, result.Exception!.Status);
    }

    [Fact]
    public async Task CancelAsync_forbids_cancellation_by_different_collector()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        routeDirectory.Routes = [new(routeId, "Ruta 1", null, "Collector", 10, 10)];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        var creatorId = Guid.NewGuid();
        var otherCollectorId = Guid.NewGuid();

        var exception = LoanExceptionRequest.Create(
            tenantId,
            customer.Id,
            routeId,
            15000m,
            10,
            10m,
            "Motivo",
            creatorId,
            UserRole.Collector,
            DateTimeOffset.UtcNow);
        await exceptionRepository.AddAsync(exception, CancellationToken.None);

        var otherActor = new CustomerActor(otherCollectorId, tenantId, UserRole.Collector);

        var result = await service.CancelAsync(
            new CancelLoanExceptionCommand(otherActor, "token", exception.Id),
            CancellationToken.None);

        Assert.Equal(LoanExceptionOutcome.Forbidden, result.Outcome);
    }

    private static Customer CreateCustomer(Guid tenantId, Guid routeId) =>
        Customer.Create(
            tenantId,
            routeId,
            "Carlos",
            "Gómez",
            "Comercio ABC",
            null,
            [new("3001234567", true)],
            [new(4.60m, -74.08m, "Local", null, true, LocationSource.Manual)],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

    private sealed class InMemoryLoanExceptionRepository : ILoanExceptionRepository
    {
        private readonly List<LoanExceptionRequest> items = [];

        public Task<LoanExceptionRequest?> FindAsync(Guid tenantId, Guid exceptionId, CancellationToken cancellationToken) =>
            Task.FromResult(items.SingleOrDefault(e => e.TenantId == tenantId && e.Id == exceptionId));

        public Task AddAsync(LoanExceptionRequest request, CancellationToken cancellationToken)
        {
            items.Add(request);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<LoanExceptionSearchPage> SearchAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> accessibleRouteIds,
            Guid? routeId,
            Guid? customerId,
            LoanExceptionStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var query = items.Where(e => e.TenantId == tenantId && accessibleRouteIds.Contains(e.RouteId));
            if (routeId.HasValue) query = query.Where(e => e.RouteId == routeId.Value);
            if (customerId.HasValue) query = query.Where(e => e.CustomerId == customerId.Value);
            if (status.HasValue) query = query.Where(e => e.Status == status.Value);

            var total = query.Count();
            var list = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new LoanExceptionSearchItem(
                    e.Id,
                    e.CustomerId,
                    "Customer Name",
                    e.RouteId,
                    e.ProposedAmount,
                    e.ProposedDays,
                    e.ProposedPercentage,
                    e.Reason,
                    e.Status,
                    e.CreatedByUserId,
                    e.CreatedByRole,
                    e.CreatedAtUtc,
                    e.DecidedByUserId,
                    e.DecidedAtUtc,
                    e.DecisionNotes,
                    e.CancelledByUserId,
                    e.CancelledAtUtc))
                .ToArray();

            return Task.FromResult(new LoanExceptionSearchPage(list, page, pageSize, total));
        }
    }

    private sealed class InMemoryCustomerRepository : ICustomerRepository
    {
        private readonly List<Customer> customers = [];

        public void Add(Customer customer) => customers.Add(customer);

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
            Task.FromResult(customers.SingleOrDefault(c => c.TenantId == tenantId && c.Id == customerId));

        public Task<CustomerSaveOutcome> TryAddAsync(
            Customer customer,
            CustomerAuditRecord auditRecord,
            int? maxActiveCustomers,
            CancellationToken cancellationToken) =>
            Task.FromResult(CustomerSaveOutcome.Saved);

        public Task<CustomerSaveOutcome> TryUpdateAsync(
            Customer customer,
            CustomerAuditRecord auditRecord,
            Guid previousRouteId,
            CustomerStatus previousStatus,
            int? maxActiveCustomers,
            CancellationToken cancellationToken) =>
            Task.FromResult(CustomerSaveOutcome.Saved);
    }

    private sealed class FakeRouteDirectory : IAdministrativeRouteDirectory
    {
        public IReadOnlyCollection<AccessibleRoute> Routes { get; set; } = [];
        public RouteDirectoryOutcome Outcome { get; set; } = RouteDirectoryOutcome.Success;

        public Task<RouteDirectoryResult> ListAccessibleAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome is RouteDirectoryOutcome.Success
                ? RouteDirectoryResult.Success(Routes)
                : new RouteDirectoryResult(Outcome, []));
    }
}
