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
    public async Task GetAgendaAsync_produces_distinct_items_per_active_loan_for_same_customer()
    {
        var custId = Guid.NewGuid();
        var loan1Id = Guid.NewGuid();
        var loan2Id = Guid.NewGuid();

        var loan1 = new AgendaLoanData(
            loan1Id, 100m, 0m, 100m, 1,
            [new(Guid.NewGuid(), 1, today, 100m, 0m, InstallmentStatus.Pending)]);
        var loan2 = new AgendaLoanData(
            loan2Id, 200m, 0m, 200m, 2,
            [new(Guid.NewGuid(), 1, today.AddDays(-2), 100m, 0m, InstallmentStatus.Pending)]);

        var customer = new AgendaCustomerData(custId, "Multi Prestamo", "Comercio", "3009998877", routeId, null, [loan1, loan2], []);
        agendaRepository.Customers = [customer];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", routeId, today, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var items = result.Data!.Items.ToList();
        Assert.Equal(2, items.Count);

        var itemLoan1 = items.Single(i => i.ActiveLoanId == loan1Id);
        Assert.Equal(AgendaStatus.DueToday, itemLoan1.Status);
        Assert.Equal(100m, itemLoan1.InstallmentAmount);

        var itemLoan2 = items.Single(i => i.ActiveLoanId == loan2Id);
        Assert.Equal(AgendaStatus.Overdue, itemLoan2.Status);
        Assert.Equal(1, itemLoan2.OverdueInstallmentsCount);
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

    [Fact]
    public async Task GetAgendaAsync_customer_marked_as_will_not_pay_has_status_WillNotPayToday()
    {
        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        var installments = new List<AgendaInstallmentData>
        {
            new(Guid.NewGuid(), 1, today, 100m, 0m, InstallmentStatus.Pending)
        };
        var loan = new AgendaLoanData(loanId, 100m, 0m, 100m, 1, installments);
        var nonPayment = new CustomerNonPaymentRecord(
            Guid.NewGuid(),
            tenantId,
            routeId,
            customerId,
            loanId,
            today,
            Guid.NewGuid(),
            "Cliente de viaje",
            "Vuelve el lunes",
            collectorId,
            DateTimeOffset.UtcNow);

        var customer = new AgendaCustomerData(
            customerId,
            "Maria Gomez",
            null,
            "3009998877",
            routeId,
            null,
            [loan],
            [],
            [nonPayment]);

        agendaRepository.Customers = [customer];
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", routeId, today, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(AgendaStatus.WillNotPayToday, item.Status);
        Assert.Equal("No pagará hoy", item.StatusLabel);
        Assert.Equal("Cliente de viaje", item.NonPaymentReasonName);
        Assert.Equal("Vuelve el lunes", item.NonPaymentObservations);
        Assert.Equal(1, result.Data.Summary.WillNotPayCount);
    }

    [Fact]
    public async Task GetAgendaAsync_partial_payment_today_moves_customer_to_Collected_even_with_overdue()
    {
        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        var installments = new List<AgendaInstallmentData>
        {
            new(Guid.NewGuid(), 1, today.AddDays(-2), 100m, 0m, InstallmentStatus.Pending)
        };
        var loan = new AgendaLoanData(loanId, 100m, 0m, 100m, 1, installments);
        var payment = new AgendaPaymentData(Guid.NewGuid(), loanId, 30m, DateTimeOffset.UtcNow, "Efectivo");

        var customer = new AgendaCustomerData(
            customerId,
            "Juan Perez",
            null,
            "3001112233",
            routeId,
            null,
            [loan],
            [payment]);

        agendaRepository.Customers = [customer];
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", routeId, today, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(AgendaStatus.Collected, item.Status);
        Assert.Equal(30m, item.CollectedTodayAmount);
        Assert.Equal(1, result.Data.Summary.CollectedCount);
    }

    [Fact]
    public async Task RegisterWillNotPayAsync_and_CancelWillNotPayAsync_succeed()
    {
        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);

        var registerInput = new RegisterWillNotPayInput(
            routeId,
            customerId,
            loanId,
            today,
            null,
            "Sin fondos",
            "Pasa mañana");

        var regResult = await service.RegisterWillNotPayAsync(
            new RegisterWillNotPayCommand(actor, "token", registerInput),
            CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, regResult.Outcome);
        Assert.Single(agendaRepository.NonPaymentRecords);
        Assert.Equal("Sin fondos", agendaRepository.NonPaymentRecords[0].ReasonName);

        var cancelInput = new CancelWillNotPayInput(routeId, customerId, loanId, today);
        var cancelResult = await service.CancelWillNotPayAsync(
            new CancelWillNotPayCommand(actor, "token", cancelInput),
            CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, cancelResult.Outcome);
        Assert.Empty(agendaRepository.NonPaymentRecords);
    }

    [Fact]
    public async Task GetAgendaAsync_daily_loan_with_today_installment_already_paid_still_appears_as_due_today()
    {
        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();

        var installments = new List<AgendaInstallmentData>
        {
            new(Guid.NewGuid(), 1, today, 50m, 50m, InstallmentStatus.Paid),
            new(Guid.NewGuid(), 2, today.AddDays(1), 50m, 0m, InstallmentStatus.Pending),
            new(Guid.NewGuid(), 3, today.AddDays(2), 50m, 0m, InstallmentStatus.Pending),
        };
        var loan = new AgendaLoanData(loanId, 150m, 50m, 100m, 3, installments, PaymentFrequency.Daily);
        var customer = new AgendaCustomerData(customerId, "Cliente Diario", null, "3001234567", routeId, null, [loan], []);
        agendaRepository.Customers = [customer];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", routeId, today, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(AgendaStatus.DueToday, item.Status);
        Assert.Equal(50m, item.DueTodayAmount);
        Assert.Equal(50m, item.InstallmentAmount);
    }

    [Fact]
    public async Task GetAgendaAsync_weekly_uptodate_loan_appears_as_uptodate_with_zero_due_today()
    {
        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();

        var installments = new List<AgendaInstallmentData>
        {
            new(Guid.NewGuid(), 1, today.AddDays(4), 100m, 0m, InstallmentStatus.Pending),
        };
        var loan = new AgendaLoanData(loanId, 100m, 0m, 100m, 1, installments, PaymentFrequency.Weekly);
        var customer = new AgendaCustomerData(customerId, "Cliente Semanal Al Dia", null, "3001234567", routeId, null, [loan], []);
        agendaRepository.Customers = [customer];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", routeId, today, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(AgendaStatus.UpToDate, item.Status);
        Assert.Equal(0m, item.DueTodayAmount);
        Assert.Equal(0m, item.OverdueAmount);
    }

    [Fact]
    public async Task GetAgendaAsync_newly_created_daily_loan_with_first_installment_tomorrow_appears_as_uptodate()
    {
        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();

        var installments = new List<AgendaInstallmentData>
        {
            new(Guid.NewGuid(), 1, today.AddDays(1), 50m, 0m, InstallmentStatus.Pending),
            new(Guid.NewGuid(), 2, today.AddDays(2), 50m, 0m, InstallmentStatus.Pending),
        };
        var loan = new AgendaLoanData(loanId, 100m, 0m, 100m, 2, installments, PaymentFrequency.Daily);
        var customer = new AgendaCustomerData(customerId, "Cliente Nuevo", null, "3001234567", routeId, null, [loan], []);
        agendaRepository.Customers = [customer];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", routeId, today, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(AgendaStatus.UpToDate, item.Status);
        Assert.Equal(0m, item.DueTodayAmount);
        Assert.Equal(0m, item.OverdueAmount);
    }

    [Fact]
    public async Task GetAgendaAsync_WhenActiveOpenBoxExistsFromYesterday_UsesBoxDateAndSetsIsBoxPastDue()
    {
        var yesterday = today.AddDays(-1);
        agendaRepository.ActiveCashBoxDate = yesterday;

        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        var installments = new List<AgendaInstallmentData>
        {
            new(Guid.NewGuid(), 1, yesterday, 50m, 0m, InstallmentStatus.Pending),
        };
        var loan = new AgendaLoanData(loanId, 50m, 0m, 50m, 1, installments, PaymentFrequency.Daily);
        var customer = new AgendaCustomerData(customerId, "Cliente Ayer", null, "3001234567", routeId, null, [loan], []);
        agendaRepository.Customers = [customer];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        // query with null TargetDate to use open box date
        var query = new GetAgendaQuery(actor, "token", routeId, null, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        Assert.True(result.Data!.Summary.IsBoxPastDue);
        Assert.Equal(yesterday, result.Data.Summary.TargetDate);
    }

    [Fact]
    public async Task GetAgendaAsync_WhenActiveOpenBoxExists_InstallmentDueOnBoxDate_IsDueTodayNotOverdue_EvenIfClientSentNextDay()
    {
        // Arrange
        // Active open cash box is for today (e.g. 2026-10-21)
        agendaRepository.ActiveCashBoxDate = today;

        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        var installments = new List<AgendaInstallmentData>
        {
            new(Guid.NewGuid(), 1, today, 51429m, 0m, InstallmentStatus.Pending),
            new(Guid.NewGuid(), 2, today.AddDays(1), 51429m, 0m, InstallmentStatus.Pending),
        };
        var loan = new AgendaLoanData(loanId, 102858m, 0m, 102858m, 2, installments, PaymentFrequency.Daily);
        var customer = new AgendaCustomerData(customerId, "Jhonny Mecanico", "negocio", "3001234567", routeId, null, [loan], []);
        agendaRepository.Customers = [customer];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        // Even if client sent tomorrow (e.g. UTC date skew at night 11pm), the active open cash box governs
        var query = new GetAgendaQuery(actor, "token", routeId, today.AddDays(1), null, null);

        // Act
        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(AgendaStatus.DueToday, item.Status);
        Assert.Equal("Cobro hoy", item.StatusLabel);
        Assert.Equal(0m, item.OverdueAmount);
        Assert.Equal(0, item.DaysOverdue);
        Assert.Equal(51429m, item.DueTodayAmount);
        Assert.Equal(today, result.Data.Summary.TargetDate);
    }

    [Fact]
    public async Task GetAgendaAsync_weekly_loan_in_mora_appears_in_daily_matrix_until_completely_uptodate()
    {
        var customerId = Guid.NewGuid();
        var loanId = Guid.NewGuid();

        var installments = new List<AgendaInstallmentData>
        {
            new(Guid.NewGuid(), 1, today.AddDays(-2), 100m, 0m, InstallmentStatus.Pending),
        };
        var loan = new AgendaLoanData(loanId, 100m, 0m, 100m, 1, installments, PaymentFrequency.Weekly);
        var customer = new AgendaCustomerData(customerId, "Cliente Semanal En Mora", null, "3001234567", routeId, null, [loan], []);
        agendaRepository.Customers = [customer];

        var actor = new CustomerActor(collectorId, tenantId, UserRole.Collector);
        var query = new GetAgendaQuery(actor, "token", routeId, today, null, null);

        var result = await service.GetAgendaAsync(query, CancellationToken.None);

        Assert.Equal(AgendaOutcome.Success, result.Outcome);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(AgendaStatus.Overdue, item.Status);
        Assert.Equal(100m, item.OverdueAmount);
    }
}

internal sealed class InMemoryAgendaRepository : IAgendaRepository
{
    public List<AgendaCustomerData> Customers { get; set; } = [];
    public List<RoutePortfolioData> RouteData { get; set; } = [];
    public List<NonPaymentReason> NonPaymentReasons { get; set; } = [];
    public List<CustomerNonPaymentRecord> NonPaymentRecords { get; set; } = [];

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

    public Task<IReadOnlyCollection<NonPaymentReason>> GetNonPaymentReasonsAsync(
        Guid tenantId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = NonPaymentReasons.Where(r => r.TenantId == tenantId);
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }
        return Task.FromResult<IReadOnlyCollection<NonPaymentReason>>(query.OrderBy(r => r.DisplayOrder).ToList());
    }

    public Task<NonPaymentReason?> GetNonPaymentReasonByIdAsync(
        Guid tenantId,
        Guid reasonId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(NonPaymentReasons.FirstOrDefault(r => r.TenantId == tenantId && r.Id == reasonId));
    }

    public Task SaveNonPaymentReasonAsync(
        NonPaymentReason reason,
        CancellationToken cancellationToken)
    {
        var idx = NonPaymentReasons.FindIndex(r => r.TenantId == reason.TenantId && r.Id == reason.Id);
        if (idx >= 0)
        {
            NonPaymentReasons[idx] = reason;
        }
        else
        {
            NonPaymentReasons.Add(reason);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<CustomerNonPaymentRecord>> GetNonPaymentRecordsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var targetRoutes = accessibleRouteIds;
        if (routeId.HasValue)
        {
            targetRoutes = [routeId.Value];
        }
        var filtered = NonPaymentRecords
            .Where(r => r.TenantId == tenantId && targetRoutes.Contains(r.RouteId) && r.BusinessDate == businessDate)
            .ToList();
        return Task.FromResult<IReadOnlyCollection<CustomerNonPaymentRecord>>(filtered);
    }

    public Task SaveNonPaymentRecordAsync(
        CustomerNonPaymentRecord record,
        CancellationToken cancellationToken)
    {
        NonPaymentRecords.RemoveAll(r => r.TenantId == record.TenantId && r.RouteId == record.RouteId && r.CustomerId == record.CustomerId && r.BusinessDate == record.BusinessDate && (record.LoanId == null || r.LoanId == record.LoanId || r.LoanId == null));
        NonPaymentRecords.Add(record);
        return Task.CompletedTask;
    }

    public Task DeleteNonPaymentRecordAsync(
        Guid tenantId,
        Guid routeId,
        Guid customerId,
        Guid? loanId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        NonPaymentRecords.RemoveAll(r => r.TenantId == tenantId && r.RouteId == routeId && r.CustomerId == customerId && r.BusinessDate == businessDate && (loanId == null || r.LoanId == loanId || r.LoanId == null));
        return Task.CompletedTask;
    }

    public DateOnly? ActiveCashBoxDate { get; set; }

    public Task<DateOnly?> GetActiveCashBoxBusinessDateAsync(
        Guid tenantId,
        Guid routeId,
        CancellationToken cancellationToken) =>
        Task.FromResult(ActiveCashBoxDate);
}

internal sealed class FakeRouteDirectory : IAdministrativeRouteDirectory
{
    public IReadOnlyCollection<AccessibleRoute> Routes { get; set; } = [];
    public Task<RouteDirectoryResult> ListAccessibleAsync(string accessToken, CancellationToken cancellationToken) =>
        Task.FromResult(RouteDirectoryResult.Success(Routes));
}

