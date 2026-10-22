using MS.Cobroup.Backend.Core.Agenda;
using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Agenda;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.Loans;
using Xunit;

namespace MS.Cobroup.Backend.Core.Tests.Agenda;

public sealed class AgendaServiceTests
{
    private readonly InMemoryAgendaRepository agendaRepository = new();
    private readonly FakeRouteDirectory routeDirectory = new();
    private readonly AgendaService service;

    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid routeId = Guid.NewGuid();
    private readonly Guid collectorId = Guid.NewGuid();
    private readonly DateOnly today = new(2026, 10, 21);

    public AgendaServiceTests()
    {
        service = new AgendaService(agendaRepository, routeDirectory);
        routeDirectory.Routes = [new(routeId, "Ruta 1", null, "Cobrador 1", 10, 10)];
    }

    [Fact]
    public async Task GetAgendaAsync_computes_overdue_days_and_amounts_accurately_AC16()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();

        // 3 installments:
        // 1: due 5 days ago (2026-10-16), expected 100, paid 0 => Overdue by 5 days, 100
        // 2: due today (2026-10-21), expected 100, paid 0 => Due today 100
        // 3: due tomorrow (2026-10-22), expected 100, paid 0 => Future
        var installments = new List<AgendaInstallmentData>
        {
            new(Guid.NewGuid(), 1, today.AddDays(-5), 100m, 0m, InstallmentStatus.Pending),
            new(Guid.NewGuid(), 2, today, 100m, 0m, InstallmentStatus.Pending),
            new(Guid.NewGuid(), 3, today.AddDays(1), 100m, 0m, InstallmentStatus.Pending),
        };

        var loan = new AgendaLoanData(loanId, 300m, 0m, 300m, 3, installments);
        var customer = new AgendaCustomerData(
            customerId,
            "Carlos Perez",
            "Tienda Don Carlos",
            "3001234567",
            routeId,
            new AgendaLocationData(4.60m, -74.08m, "Calle 10 # 5-20", "Local"),
            [loan],
            []);

        agendaRepository.Customers = [customer];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", routeId, today, null, null);

        // Act
        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var data = result.Data!;
        Assert.Single(data.Items);

        var item = data.Items.First();
        Assert.Equal(customerId, item.CustomerId);
        Assert.Equal(AgendaStatus.Overdue, item.Status);
        Assert.Equal(100m, item.OverdueAmount);
        Assert.Equal(5, item.DaysOverdue);
        Assert.Equal(100m, item.DueTodayAmount);
        Assert.Equal("#DC2626", item.MarkerColor);
        Assert.Equal("En mora (5d)", item.StatusLabel);

        Assert.Equal(1, data.Summary.OverdueCount);
        Assert.Equal(100m, data.Summary.TotalOverdue);
        Assert.Equal(100m, data.Summary.TotalDueToday);
    }

    [Fact]
    public async Task GetAgendaAsync_assigns_due_today_and_collected_statuses()
    {
        // Customer 1: due today, no overdue => DueToday
        var cust1Id = Guid.NewGuid();
        var loan1 = new AgendaLoanData(
            Guid.NewGuid(), 100m, 0m, 100m, 1,
            [new(Guid.NewGuid(), 1, today, 100m, 0m, InstallmentStatus.Pending)]);
        var cust1 = new AgendaCustomerData(cust1Id, "Ana Lopez", null, "3001112233", routeId, null, [loan1], []);

        // Customer 2: paid today, no overdue, no due today => Collected
        var cust2Id = Guid.NewGuid();
        var loan2 = new AgendaLoanData(
            Guid.NewGuid(), 100m, 100m, 0m, 1,
            [new(Guid.NewGuid(), 1, today, 100m, 100m, InstallmentStatus.Paid)]);
        var payment = new AgendaPaymentData(Guid.NewGuid(), loan2.LoanId, 100m, DateTimeOffset.UtcNow, "Efectivo");
        var cust2 = new AgendaCustomerData(cust2Id, "Beatriz Gomez", null, "3004445566", routeId, null, [loan2], [payment]);

        agendaRepository.Customers = [cust1, cust2];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", routeId, today, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var items = result.Data!.Items.ToList();
        Assert.Equal(2, items.Count);

        var item1 = items.Single(i => i.CustomerId == cust1Id);
        Assert.Equal(AgendaStatus.DueToday, item1.Status);
        Assert.Equal("#F59E0B", item1.MarkerColor);

        var item2 = items.Single(i => i.CustomerId == cust2Id);
        Assert.Equal(AgendaStatus.Collected, item2.Status);
        Assert.Equal("#10B981", item2.MarkerColor);
    }

    [Fact]
    public async Task GetAgendaAsync_forbids_access_to_unauthorized_route()
    {
        var unauthorizedRouteId = Guid.NewGuid();
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", unauthorizedRouteId, today, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Forbidden, result.Outcome);
    }

    [Fact]
    public async Task GetMapLocationsAsync_returns_only_customers_with_coordinates()
    {
        var custWithLoc = new AgendaCustomerData(
            Guid.NewGuid(), "Con Ubicacion", null, null, routeId,
            new AgendaLocationData(4.65m, -74.05m, "Calle 72", "Oficina"),
            [], []);

        var custWithoutLoc = new AgendaCustomerData(
            Guid.NewGuid(), "Sin Ubicacion", null, null, routeId,
            null,
            [], []);

        agendaRepository.Customers = [custWithLoc, custWithoutLoc];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetMapLocationsQuery(actor, "token", routeId, today);

        var result = await service.GetMapLocationsAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        Assert.Single(result.Data!);
        Assert.Equal(custWithLoc.CustomerId, result.Data!.First().CustomerId);
    }

    [Fact]
    public async Task OptimizeSequenceAsync_orders_stops_by_distance_nearest_neighbor()
    {
        // Origin: (0, 0)
        // Stop A: (0, 10) -> distance ~1111 km
        // Stop B: (0, 1)  -> distance ~111 km (closer to origin)
        // Stop C: (0, 2)  -> closer to Stop B than to Stop A
        var stopA = new AgendaCustomerData(
            Guid.NewGuid(), "Stop A", null, null, routeId,
            new AgendaLocationData(0m, 10m, "Far", null), [], []);

        var stopB = new AgendaCustomerData(
            Guid.NewGuid(), "Stop B", null, null, routeId,
            new AgendaLocationData(0m, 1m, "Near Origin", null), [], []);

        var stopC = new AgendaCustomerData(
            Guid.NewGuid(), "Stop C", null, null, routeId,
            new AgendaLocationData(0m, 2m, "Near B", null), [], []);

        agendaRepository.Customers = [stopA, stopB, stopC];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var input = new OptimizeSequenceInput(routeId, today, 0m, 0m);
        var command = new OptimizeSequenceCommand(actor, "token", input);

        var result = await service.OptimizeSequenceAsync(command, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var ordered = result.Data!.Items.ToList();

        // Sequence should start with nearest to origin (Stop B), then Stop C, then Stop A
        Assert.Equal("Stop B", ordered[0].CustomerName);
        Assert.Equal("Stop C", ordered[1].CustomerName);
        Assert.Equal("Stop A", ordered[2].CustomerName);
    }

    [Fact]
    public async Task GetRouteAnalyticsAsync_calculates_PAR_and_aging_buckets()
    {
        var buckets = new List<AgingBucketData>
        {
            new("current", "Al día", 0, 0, 8000m, 8),
            new("1_7_days", "Mora temprana", 1, 7, 2000m, 2),
            new("8_15_days", "Mora media", 8, 15, 0m, 0),
            new("16_30_days", "Mora alta", 16, 30, 0m, 0),
            new("over_30_days", "Mora crítica", 31, null, 0m, 0)
        };

        agendaRepository.RouteData = [
            new RoutePortfolioData(
                routeId,
                10,
                10,
                12000m,
                2000m,
                10000m,
                2000m,
                20.00m, // PAR% = 2000 / 10000 = 20%
                buckets)
        ];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetRouteAnalyticsQuery(actor, "token", routeId, today);

        var result = await service.GetRouteAnalyticsAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var analytics = result.Data!;
        Assert.Equal(10000m, analytics.TotalBalanceAmount);
        Assert.Equal(2000m, analytics.TotalOverdueAmount);
        Assert.Equal(20.00m, analytics.PortfolioAtRiskRate);
        Assert.Equal(5, analytics.AgingBuckets.Count);
    }
}

internal sealed class InMemoryAgendaRepository : IAgendaRepository
{
    public List<AgendaCustomerData> Customers { get; set; } = [];
    public List<RoutePortfolioData> RouteData { get; set; } = [];

    public Task<IReadOnlyCollection<AgendaCustomerData>> GetAgendaCustomersAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        DateOnly targetDate,
        CancellationToken cancellationToken)
    {
        var filtered = Customers.Where(c => accessibleRouteIds.Contains(c.RouteId));
        if (routeId.HasValue)
        {
            filtered = filtered.Where(c => c.RouteId == routeId.Value);
        }
        return Task.FromResult<IReadOnlyCollection<AgendaCustomerData>>(filtered.ToList());
    }

    public Task<IReadOnlyCollection<RoutePortfolioData>> GetRoutePortfolioDataAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        DateOnly targetDate,
        CancellationToken cancellationToken)
    {
        var filtered = RouteData.Where(r => accessibleRouteIds.Contains(r.RouteId));
        if (routeId.HasValue)
        {
            filtered = filtered.Where(r => r.RouteId == routeId.Value);
        }
        return Task.FromResult<IReadOnlyCollection<RoutePortfolioData>>(filtered.ToList());
    }
}

internal sealed class FakeRouteDirectory : IAdministrativeRouteDirectory
{
    public IReadOnlyCollection<AccessibleRoute> Routes { get; set; } = [];
    public Task<RouteDirectoryResult> ListAccessibleAsync(string accessToken, CancellationToken cancellationToken) =>
        Task.FromResult(RouteDirectoryResult.Success(Routes));
}

