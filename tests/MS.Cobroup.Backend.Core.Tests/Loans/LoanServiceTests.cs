using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.Loans;
using MS.Cobroup.Backend.Core.LoanTemplates;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.LoanExceptions;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Core.Tests.Loans;

public sealed class LoanServiceTests
{
    private readonly InMemoryLoanRepository loanRepository = new();
    private readonly InMemoryCustomerRepository customerRepository = new();
    private readonly InMemoryLoanExceptionRepository exceptionRepository = new();
    private readonly FakeRouteDirectory routeDirectory = new();
    private readonly FakeLoanTemplateDirectory templateDirectory = new();
    private readonly LoanService service;

    public LoanServiceTests()
    {
        service = new LoanService(
            loanRepository,
            customerRepository,
            exceptionRepository,
            routeDirectory,
            templateDirectory);
    }

    [Fact]
    public async Task OriginateAsync_creates_loan_with_published_template()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        routeDirectory.Routes = [new(routeId, "Ruta Centro", null, "Cobrador 1", 10, 10)];
        templateDirectory.Templates = [new(templateId, "Plantilla 20D", "Desc", "Standard", "Published", 1, 20, 1000m, [10m, 15m, 20m], DateTimeOffset.UtcNow, RouteIds: [routeId])];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var input = new OriginateLoanInput(
            customer.Id,
            800m,
            15m,
            PaymentFrequency.Daily,
            null,
            20,
            templateId,
            1,
            null,
            new DateOnly(2026, 11, 1));

        var result = await service.OriginateAsync(
            new OriginateLoanCommand(actor, "token", input),
            CancellationToken.None);

        Assert.Equal(LoanOutcome.Created, result.Outcome);
        Assert.NotNull(result.Loan);
        Assert.Equal(customer.Id, result.Loan.CustomerId);
        Assert.Equal(800m, result.Loan.PrincipalAmount);
        Assert.Equal(15m, result.Loan.InterestRate);
        Assert.Equal(120m, result.Loan.InterestAmount);
        Assert.Equal(920m, result.Loan.TotalAmount);
        Assert.Equal(20, result.Loan.Installments.Count);
        Assert.Equal(920m, result.Loan.Installments.Sum(i => i.ExpectedAmount));
    }

    [Fact]
    public async Task OriginateAsync_rejects_when_principal_exceeds_template_max()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        routeDirectory.Routes = [new(routeId, "Ruta Centro", null, "Cobrador 1", 10, 10)];
        templateDirectory.Templates = [new(templateId, "Plantilla Pequeña", "Desc", "Standard", "Published", 1, 20, 500m, [15m], DateTimeOffset.UtcNow, RouteIds: [routeId])];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        var actor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Collector);
        var input = new OriginateLoanInput(
            customer.Id,
            600m, // Exceeds 500m
            15m,
            PaymentFrequency.Daily,
            null,
            20,
            templateId,
            1,
            null,
            new DateOnly(2026, 11, 1));

        var result = await service.OriginateAsync(
            new OriginateLoanCommand(actor, "token", input),
            CancellationToken.None);

        Assert.Equal(LoanOutcome.Invalid, result.Outcome);
        Assert.Contains("supera el máximo permitido por la plantilla", result.ErrorMessage);
    }

    [Fact]
    public async Task OriginateAsync_succeeds_with_approved_exception_when_over_limit()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();

        routeDirectory.Routes = [new(routeId, "Ruta Centro", null, "Cobrador 1", 10, 10)];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        var exception = LoanExceptionRequest.Create(
            tenantId,
            customer.Id,
            routeId,
            1500m,
            30,
            18m,
            "Cliente especial",
            collectorId,
            UserRole.Collector,
            DateTimeOffset.UtcNow.AddHours(-2));
        exception.ApplyDecision(LoanExceptionDecision.Approved, Guid.NewGuid(), "Aprobado por gerencia", DateTimeOffset.UtcNow);
        exceptionRepository.Add(exception);

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var input = new OriginateLoanInput(
            customer.Id,
            1500m,
            18m,
            PaymentFrequency.Daily,
            null,
            30,
            null,
            null,
            exception.Id,
            new DateOnly(2026, 11, 1));

        var result = await service.OriginateAsync(
            new OriginateLoanCommand(actor, "token", input),
            CancellationToken.None);

        Assert.Equal(LoanOutcome.Created, result.Outcome);
        Assert.NotNull(result.Loan);
        Assert.Equal(exception.Id, result.Loan.ExceptionRequestId);
        Assert.Equal(1500m, result.Loan.PrincipalAmount);
        Assert.Equal(30, result.Loan.Installments.Count);
    }

    [Fact]
    public async Task OriginateAsync_rejects_pending_exception()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();

        routeDirectory.Routes = [new(routeId, "Ruta Centro", null, "Cobrador 1", 10, 10)];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        var exception = LoanExceptionRequest.Create(
            tenantId,
            customer.Id,
            routeId,
            1500m,
            30,
            18m,
            "Cliente especial",
            collectorId,
            UserRole.Collector,
            DateTimeOffset.UtcNow);
        exceptionRepository.Add(exception);

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var input = new OriginateLoanInput(
            customer.Id,
            1500m,
            18m,
            PaymentFrequency.Daily,
            null,
            30,
            null,
            null,
            exception.Id,
            new DateOnly(2026, 11, 1));

        var result = await service.OriginateAsync(
            new OriginateLoanCommand(actor, "token", input),
            CancellationToken.None);

        Assert.Equal(LoanOutcome.Invalid, result.Outcome);
        Assert.Contains("no ha sido aprobada", result.ErrorMessage);
    }

    [Fact]
    public async Task OriginateAsync_rejects_when_route_active_loans_quota_reached()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        // MaxActiveLoans = 1
        routeDirectory.Routes = [new(routeId, "Ruta 1", null, "Cobrador 1", 10, 1)];
        templateDirectory.Templates = [new(templateId, "Plantilla", "Desc", "Standard", "Published", 1, 20, 1000m, [10m], DateTimeOffset.UtcNow, RouteIds: [routeId])];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        loanRepository.ActiveLoanCountsByRoute[(tenantId, routeId)] = 1; // Already 1 active loan

        var actor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Collector);
        var input = new OriginateLoanInput(
            customer.Id,
            500m,
            10m,
            PaymentFrequency.Daily,
            null,
            10,
            templateId,
            1,
            null,
            new DateOnly(2026, 11, 1));

        var result = await service.OriginateAsync(
            new OriginateLoanCommand(actor, "token", input),
            CancellationToken.None);

        Assert.Equal(LoanOutcome.Conflict, result.Outcome);
        Assert.Contains("cupo de préstamos activos", result.ErrorMessage);
    }

    [Fact]
    public async Task OriginateAsync_rejects_inactive_customer()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        routeDirectory.Routes = [new(routeId, "Ruta 1", null, "Cobrador 1", 10, 10)];
        templateDirectory.Templates = [new(templateId, "Plantilla", "Desc", "Standard", "Published", 1, 20, 1000m, [10m], DateTimeOffset.UtcNow, RouteIds: [routeId])];

        var customer = CreateCustomer(tenantId, routeId);
        customer.Update(
            routeId,
            "Juan",
            "Pérez",
            "Abarrotes",
            null,
            CustomerStatus.Inactive,
            [new CustomerPhoneValue("555123456", true)],
            [new CustomerLocationValue(19.4326m, -99.1332m, "Tienda", "Av. Principal 123", true, LocationSource.Manual)],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        customerRepository.Add(customer);

        var actor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Collector);
        var input = new OriginateLoanInput(
            customer.Id,
            500m,
            10m,
            PaymentFrequency.Daily,
            null,
            10,
            templateId,
            1,
            null,
            new DateOnly(2026, 11, 1));

        var result = await service.OriginateAsync(
            new OriginateLoanCommand(actor, "token", input),
            CancellationToken.None);

        Assert.Equal(LoanOutcome.Invalid, result.Outcome);
        Assert.Contains("cliente inactivo", result.ErrorMessage);
    }

    [Fact]
    public async Task GetAsync_returns_loan_with_installments()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        routeDirectory.Routes = [new(routeId, "Ruta 1", null, "Cobrador 1", 10, 10)];
        templateDirectory.Templates = [new(templateId, "Plantilla", "Desc", "Standard", "Published", 1, 10, 1000m, [10m], DateTimeOffset.UtcNow, RouteIds: [routeId])];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var originateResult = await service.OriginateAsync(
            new OriginateLoanCommand(actor, "token", new OriginateLoanInput(
                customer.Id, 500m, 10m, PaymentFrequency.Daily, null, 5, templateId, 1, null, new DateOnly(2026, 11, 1))),
            CancellationToken.None);

        var loanId = originateResult.Loan!.Id;

        var getResult = await service.GetAsync(
            new GetLoanQuery(actor, "token", loanId),
            CancellationToken.None);

        Assert.Equal(LoanOutcome.Success, getResult.Outcome);
        Assert.NotNull(getResult.Loan);
        Assert.Equal(5, getResult.Loan.Installments.Count);
    }

    [Fact]
    public async Task ListByCustomerAsync_returns_customer_loans()
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var collectorId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        routeDirectory.Routes = [new(routeId, "Ruta 1", null, "Cobrador 1", 10, 10)];
        templateDirectory.Templates = [new(templateId, "Plantilla", "Desc", "Standard", "Published", 1, 10, 1000m, [10m], DateTimeOffset.UtcNow, RouteIds: [routeId])];

        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        await service.OriginateAsync(
            new OriginateLoanCommand(actor, "token", new OriginateLoanInput(
                customer.Id, 500m, 10m, PaymentFrequency.Daily, null, 5, templateId, 1, null, new DateOnly(2026, 11, 1))),
            CancellationToken.None);

        var listResult = await service.ListByCustomerAsync(actor, "token", customer.Id, CancellationToken.None);

        Assert.Equal(LoanListOutcome.Success, listResult.Outcome);
        Assert.NotNull(listResult.Loans);
        Assert.Single(listResult.Loans);
    }

    [Theory]
    [InlineData(100, 10, true)]
    [InlineData(1000, 20, true)]
    [InlineData(500, 12.5, true)]
    [InlineData(99, 15, false)]
    [InlineData(1001, 15, false)]
    [InlineData(500, 9.99, false)]
    [InlineData(500, 20.01, false)]
    public async Task OriginationEnforcesContinuousRanges(decimal principal, decimal rate, bool accepted)
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        routeDirectory.Routes = [new(routeId, "Ruta", null, "Collector", null, null)];
        templateDirectory.Templates = [new(templateId, "Rango", null, "Low", "Published", 1, 30, 1000m,
            [10m, 20m], DateTimeOffset.UtcNow, 100m, 10m, 20m, [routeId])];
        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);
        var actor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Collector);
        var result = await service.OriginateAsync(new(actor, "token", new(customer.Id, principal, rate,
            PaymentFrequency.Daily, null, 10, templateId, 1, null, new DateOnly(2026, 11, 1))), default);
        Assert.Equal(accepted ? LoanOutcome.Created : LoanOutcome.Invalid, result.Outcome);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task RejectsTemplateFromAnotherRouteOrStaleVersion(bool assigned, int version)
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        routeDirectory.Routes = [new(routeId, "Ruta", null, "Collector", null, null)];
        templateDirectory.Templates = [new(templateId, "Rango", null, "Low", "Published", 1, 30, 1000m,
            [10m, 20m], DateTimeOffset.UtcNow, 100m, 10m, 20m, [assigned ? routeId : Guid.NewGuid()])];
        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);
        var actor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Collector);
        var result = await service.OriginateAsync(new(actor, "token", new(customer.Id, 500m, 15m,
            PaymentFrequency.Daily, null, 10, templateId, version, null, new DateOnly(2026, 11, 1))), default);
        Assert.Equal(LoanOutcome.Invalid, result.Outcome);
    }

    [Theory]
    [InlineData(59, true)]
    [InlineData(58, false)]
    public async Task MonthlyLoanChecksActualCalendarDurationAgainstTemplate(int maximumDays, bool accepted)
    {
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        routeDirectory.Routes = [new(routeId, "Ruta", null, "Collector", null, null)];
        templateDirectory.Templates = [new(templateId, "Mensual", null, "Low", "Published", 1, maximumDays, 1000m,
            [10m, 20m], DateTimeOffset.UtcNow, 100m, 10m, 20m, [routeId])];
        var customer = CreateCustomer(tenantId, routeId);
        customerRepository.Add(customer);
        var actor = new CustomerActor(Guid.NewGuid(), tenantId, UserRole.Collector);
        var result = await service.OriginateAsync(new(actor, "token", new(customer.Id, 500m, 12.5m,
            PaymentFrequency.Monthly, null, 2, templateId, 1, null, new DateOnly(2026, 1, 31))), default);
        Assert.Equal(accepted ? LoanOutcome.Created : LoanOutcome.Invalid, result.Outcome);
        if (accepted)
        {
            Assert.Equal(PaymentFrequency.Monthly, result.Loan!.PaymentFrequency);
            Assert.Equal(new DateOnly(2026, 2, 28), result.Loan.Installments.First().DueDate);
            Assert.Equal(new DateOnly(2026, 3, 31), result.Loan.Installments.Last().DueDate);
        }
    }

    private static Customer CreateCustomer(Guid tenantId, Guid routeId)
    {
        return Customer.Create(
            tenantId,
            routeId,
            "Juan",
            "Pérez",
            "Abarrotes Pérez",
            "Cliente recurrente",
            [new CustomerPhoneValue("555123456", true)],
            [new CustomerLocationValue(19.4326m, -99.1332m, "Tienda", "Av. Principal 123", true, LocationSource.Manual)],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
    }

    private sealed class InMemoryLoanRepository : ILoanRepository
    {
        private readonly List<Loan> loans = [];
        public Dictionary<(Guid TenantId, Guid RouteId), int> ActiveLoanCountsByRoute { get; } = [];

        public Task<Loan?> FindAsync(Guid tenantId, Guid loanId, CancellationToken cancellationToken) =>
            Task.FromResult(loans.SingleOrDefault(l => l.TenantId == tenantId && l.Id == loanId));

        public Task AddAsync(Loan loan, CancellationToken cancellationToken)
        {
            loans.Add(loan);
            return Task.CompletedTask;
        }

        public Task<LoanSaveOutcome> TryAddAsync(
            Loan loan,
            int? maxActiveLoans,
            Guid? consumedExceptionId,
            CancellationToken cancellationToken)
        {
            if (maxActiveLoans.HasValue)
            {
                var active = ActiveLoanCountsByRoute.TryGetValue((loan.TenantId, loan.RouteId), out var count)
                    ? count
                    : loans.Count(l => l.TenantId == loan.TenantId && l.RouteId == loan.RouteId && l.Status == LoanStatus.Active);

                if (active >= maxActiveLoans.Value)
                {
                    return Task.FromResult(LoanSaveOutcome.CapacityReached);
                }
            }

            loans.Add(loan);
            return Task.FromResult(LoanSaveOutcome.Saved);
        }

        public Task<int> CountActiveLoansByRouteAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken)
        {
            if (ActiveLoanCountsByRoute.TryGetValue((tenantId, routeId), out var count))
            {
                return Task.FromResult(count);
            }

            var active = loans.Count(l => l.TenantId == tenantId && l.RouteId == routeId && l.Status == LoanStatus.Active);
            return Task.FromResult(active);
        }

        public Task<IReadOnlyCollection<Loan>> ListByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<Loan> result = loans.Where(l => l.TenantId == tenantId && l.CustomerId == customerId).ToList();
            return Task.FromResult(result);
        }

        public Task<LoanSearchPage> SearchAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> accessibleRouteIds,
            Guid? routeId,
            Guid? customerId,
            LoanStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var query = loans.Where(l => l.TenantId == tenantId && accessibleRouteIds.Contains(l.RouteId));
            if (routeId.HasValue) query = query.Where(l => l.RouteId == routeId.Value);
            if (customerId.HasValue) query = query.Where(l => l.CustomerId == customerId.Value);
            if (status.HasValue) query = query.Where(l => l.Status == status.Value);

            var items = query.Skip((page - 1) * pageSize).Take(pageSize).Select(l => new LoanSearchItem(
                l.Id,
                l.CustomerId,
                "Cliente",
                l.RouteId,
                l.Terms.PrincipalAmount,
                l.Terms.InterestRate,
                l.Terms.TotalAmount,
                l.Terms.BalanceAmount,
                l.Terms.PaymentFrequency,
                l.Terms.InstallmentCount,
                l.Status,
                l.Terms.StartsAtUtc,
                l.Terms.DueAtUtc,
                l.CreatedAtUtc)).ToList();

            return Task.FromResult(new LoanSearchPage(items, page, pageSize, query.Count()));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
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

    private sealed class InMemoryLoanExceptionRepository : ILoanExceptionRepository
    {
        private readonly List<LoanExceptionRequest> exceptions = [];

        public void Add(LoanExceptionRequest exception) => exceptions.Add(exception);

        public Task<LoanExceptionRequest?> FindAsync(Guid tenantId, Guid exceptionId, CancellationToken cancellationToken) =>
            Task.FromResult(exceptions.SingleOrDefault(e => e.TenantId == tenantId && e.Id == exceptionId));

        public Task AddAsync(LoanExceptionRequest request, CancellationToken cancellationToken)
        {
            exceptions.Add(request);
            return Task.CompletedTask;
        }

        public Task<LoanExceptionSearchPage> SearchAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> accessibleRouteIds,
            Guid? routeId,
            Guid? customerId,
            LoanExceptionStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new LoanExceptionSearchPage([], page, pageSize, 0));

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
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

    private sealed class FakeLoanTemplateDirectory : IAdministrativeLoanTemplateDirectory
    {
        public IReadOnlyCollection<AvailableLoanTemplate> Templates { get; set; } = [];
        public LoanTemplateDirectoryOutcome Outcome { get; set; } = LoanTemplateDirectoryOutcome.Success;

        public Task<LoanTemplateDirectoryResult> ListAvailableAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome is LoanTemplateDirectoryOutcome.Success
                ? LoanTemplateDirectoryResult.Success(Templates)
                : new LoanTemplateDirectoryResult(Outcome, []));
    }
}
