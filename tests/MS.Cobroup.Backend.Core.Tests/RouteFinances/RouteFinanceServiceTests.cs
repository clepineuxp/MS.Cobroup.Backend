using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.RouteFinances;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Directories;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.RouteFinances;

namespace MS.Cobroup.Backend.Core.Tests.RouteFinances;

public sealed class RouteFinanceServiceTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid routeId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();

    [Fact]
    public async Task BalanceUsesInitialLoansPaymentsIncomeAndExpensesWithoutClamping()
    {
        var repository = new FakeRepository
        {
            Totals = new RouteBalanceTotals(2_000m, 250m, 100m, 75m)
        };
        var service = CreateService(repository);

        var result = await service.GetBalanceAsync(Command(UserRole.Collector), routeId, CancellationToken.None);

        Assert.Equal(RouteFinanceOutcome.Success, result.Outcome);
        Assert.Equal(-725m, result.Balance!.NetBalance);
        // Cuando la caja está cerrada, balance inicial y actual siempre deben ser iguales
        // y los movimientos de la caja son 0
        Assert.Equal(-725m, result.Balance.InitialBalance);
        Assert.Equal(0m, result.Balance.PaymentCollections);
        Assert.Equal(0m, result.Balance.LoanDisbursements);
        Assert.Equal(0m, result.Balance.IncomeInvestments);
        Assert.Equal(0m, result.Balance.Expenses);
    }

    [Fact]
    public async Task OpenBoxCalculatesInitialBalanceAsNetMinusDeltaInCurrentBox()
    {
        var repository = new FakeRepository
        {
            Totals = new RouteBalanceTotals(2_000m, 250m, 100m, 75m),
            TotalsSince = new RouteBalanceTotals(10m, 50m, 20m, 5m),
            Closing = RouteDayClosing.CreateOpen(tenantId, routeId, DateOnly.FromDateTime(DateTime.UtcNow), userId, "Cobrador", DateTimeOffset.UtcNow.AddHours(-1))
        };
        var service = CreateService(repository);

        var result = await service.GetBalanceAsync(Command(UserRole.Collector), routeId, CancellationToken.None);

        Assert.Equal(RouteFinanceOutcome.Success, result.Outcome);
        Assert.Equal(-725m, result.Balance!.NetBalance);
        // Net (-725) - Delta (+50 + 20 - 10 - 5 = +55) = -780m
        Assert.Equal(-780m, result.Balance.InitialBalance);
        Assert.Equal(50m, result.Balance.PaymentCollections);
        Assert.Equal(10m, result.Balance.LoanDisbursements);
        Assert.Equal(20m, result.Balance.IncomeInvestments);
        Assert.Equal(5m, result.Balance.Expenses);
    }

    [Fact]
    public async Task CollectorCannotModifyAnEntryCreatedOnAPreviousBusinessDay()
    {
        var repository = new FakeRepository();
        repository.Entry = RouteFinancialEntry.Create(
            tenantId, routeId, RouteFinancialEntryType.Expense, "Combustible", null, 30m,
            userId, "Cobrador", DateTimeOffset.UtcNow.AddDays(-1));
        var service = CreateService(repository);

        var result = await service.UpdateEntryAsync(
            Command(UserRole.Collector), repository.Entry.Id,
            new SaveRouteFinancialEntryInput(routeId, RouteFinancialEntryType.Expense, "Gasolina", null, 35m),
            CancellationToken.None);

        Assert.Equal(RouteFinanceOutcome.Forbidden, result.Outcome);
        Assert.Equal("Combustible", repository.Entry.Concept);
    }

    [Fact]
    public async Task AdministratorCanModifyAnEntryCreatedOnAPreviousDay()
    {
        var repository = new FakeRepository();
        repository.Entry = RouteFinancialEntry.Create(
            tenantId, routeId, RouteFinancialEntryType.Income, "Aporte", null, 80m,
            Guid.NewGuid(), "Supervisor", DateTimeOffset.UtcNow.AddDays(-7));
        var service = CreateService(repository);

        var result = await service.UpdateEntryAsync(
            Command(UserRole.Admin), repository.Entry.Id,
            new SaveRouteFinancialEntryInput(routeId, RouteFinancialEntryType.Investment, "Capital adicional", "Caja", 90m),
            CancellationToken.None);

        Assert.Equal(RouteFinanceOutcome.Success, result.Outcome);
        Assert.Equal(RouteFinancialEntryType.Investment, repository.Entry.Type);
        Assert.Equal(90m, repository.Entry.Amount);
        Assert.Equal("Administrador", repository.Entry.UpdatedByDisplayName);
    }

    [Fact]
    public async Task ClosedDayIsReopenedWhenCollectorRegistersNewMovement()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);
        var closed = await service.CloseDayAsync(Command(UserRole.Collector), routeId, CancellationToken.None);
        Assert.True(closed.ClosingStatus!.IsClosed);
        var input = new SaveRouteFinancialEntryInput(routeId, RouteFinancialEntryType.Income, "Capital", null, 100m);
        // Al ingresar un nuevo movimiento el cobrador puede abrir la caja automáticamente
        var createResult = await service.CreateEntryAsync(Command(UserRole.Collector), input, CancellationToken.None);
        Assert.Equal(RouteFinanceOutcome.Created, createResult.Outcome);
        Assert.False(repository.Closing!.IsClosed);
    }

    [Fact]
    public async Task AutomaticBoxOpeningOnFirstMovement_WhenNoBoxExists()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        var input = new SaveRouteFinancialEntryInput(routeId, RouteFinancialEntryType.Income, "Venta inicial", null, 150m);
        var result = await service.CreateEntryAsync(Command(UserRole.Collector), input, CancellationToken.None);

        Assert.Equal(RouteFinanceOutcome.Created, result.Outcome);
        Assert.NotNull(repository.Closing);
        Assert.False(repository.Closing.IsClosed);
        Assert.NotNull(repository.Closing.OpenedAtUtc);
    }

    [Fact]
    public async Task ReopenBox_FailsWhenAnotherBoxIsOpen()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        // Active open box
        repository.Closing = RouteDayClosing.CreateOpen(tenantId, routeId, new DateOnly(2026, 11, 12), userId, "Cobrador", DateTimeOffset.UtcNow);

        var result = await service.ReopenBoxAsync(Command(UserRole.Admin), routeId, new DateOnly(2026, 11, 11), CancellationToken.None);

        Assert.Equal(RouteFinanceOutcome.Invalid, result.Outcome);
        Assert.Contains("existe una caja abierta", result.Error);
    }

    [Fact]
    public async Task ReopenBox_SucceedsWhenNoBoxIsOpen()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        repository.Closing = RouteDayClosing.CreateClosed(tenantId, routeId, new DateOnly(2026, 11, 11), userId, "Cobrador", DateTimeOffset.UtcNow);

        var result = await service.ReopenBoxAsync(Command(UserRole.Admin), routeId, new DateOnly(2026, 11, 11), CancellationToken.None);

        Assert.Equal(RouteFinanceOutcome.Success, result.Outcome);
        Assert.False(result.ClosingStatus!.IsClosed);
        Assert.NotNull(result.ClosingStatus.ReopenedAtUtc);
    }

    [Fact]
    public async Task ExpiredCutoff_BlocksMovements_WithDebesCerrarCaja()
    {
        var repository = new FakeRepository();
        // Route with 4 AM local cutoff (09:00 UTC) and enforce enabled
        var customDirectory = new FakeCustomDirectory(new AccessibleRoute(
            routeId, "Centro", null, "Collector", null, null, 1_000m, "America/Bogota",
            new TimeOnly(9, 0), true));
        var service = new RouteFinanceService(repository, customDirectory);

        // Open box from yesterday
        repository.Closing = RouteDayClosing.CreateOpen(
            tenantId, routeId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), userId, "Cobrador", DateTimeOffset.UtcNow.AddDays(-2));

        var input = new SaveRouteFinancialEntryInput(routeId, RouteFinancialEntryType.Income, "Cobro tardio", null, 100m);
        var result = await service.CreateEntryAsync(Command(UserRole.Collector), input, CancellationToken.None);

        Assert.Equal(RouteFinanceOutcome.Invalid, result.Outcome);
        Assert.Contains("Debes cerrar caja para continuar", result.Error);
    }

    private sealed class FakeCustomDirectory(AccessibleRoute route) : IAdministrativeRouteDirectory
    {
        public Task<RouteDirectoryResult> ListAccessibleAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(RouteDirectoryResult.Success([route]));
    }

    [Fact]
    public async Task AuditTraceRejectsInaccessibleRouteAndFiltersUnassignedRoutes()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);
        var hiddenRoute = Guid.NewGuid();
        foreach (var id in new[] { routeId, hiddenRoute })
            repository.Audits.Add(FinancialMovementAuditLog.Create(tenantId, id, FinancialMovementEntityType.Payment,
                Guid.NewGuid(), FinancialMovementAuditAction.Created, "Cobro", 20m, "Cobro registrado", userId, "Supervisor", "Supervisor", DateTimeOffset.UtcNow));
        Assert.Equal(RouteFinanceOutcome.Forbidden, (await service.ListAuditTraceAsync(Command(UserRole.Supervisor), hiddenRoute, null, null, CancellationToken.None)).Outcome);
        var result = await service.ListAuditTraceAsync(Command(UserRole.Supervisor), null, null, null, CancellationToken.None);
        Assert.Equal(routeId, Assert.Single(result.AuditLogs!).RouteId);
    }

    [Fact]
    public async Task DayClosingStatusReturnsCorrectBoxNameWhenOpenAndClosed()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        // When closed / no open box
        var statusClosed = await service.GetDayClosingStatusAsync(Command(UserRole.Collector), routeId, CancellationToken.None);
        Assert.Equal("Centro | Caja Cerrada", statusClosed.ClosingStatus!.BoxName);
        Assert.True(statusClosed.ClosingStatus.IsClosed);

        // When open box exists
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        repository.Closing = RouteDayClosing.CreateOpen(tenantId, routeId, today, userId, "Cobrador", DateTimeOffset.UtcNow);
        var statusOpen = await service.GetDayClosingStatusAsync(Command(UserRole.Collector), routeId, CancellationToken.None);
        Assert.Equal($"Centro | {today:yyyy-MM-dd}", statusOpen.ClosingStatus!.BoxName);
        Assert.False(statusOpen.ClosingStatus.IsClosed);
    }

    private RouteFinanceService CreateService(FakeRepository repository) =>
        new(repository, new FakeDirectory(routeId));

    private RouteFinanceCommand Command(UserRole role) =>
        new(new CustomerActor(userId, tenantId, role), role is UserRole.Admin ? "Administrador" : "Cobrador", "token");

    private sealed class FakeDirectory(Guid routeId) : IAdministrativeRouteDirectory
    {
        public Task<RouteDirectoryResult> ListAccessibleAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult(RouteDirectoryResult.Success([
                new AccessibleRoute(routeId, "Centro", null, "Collector", null, null, 1_000m)
            ]));
    }

    private sealed class FakeRepository : IRouteFinanceRepository
    {
        public RouteDayClosing? Closing { get; set; }
        public List<FinancialMovementAuditLog> Audits { get; } = [];
        public RouteBalanceTotals Totals { get; set; } = new(0m, 0m, 0m, 0m);
        public RouteBalanceTotals? TotalsSince { get; set; }
        public RouteFinancialEntry? Entry { get; set; }
        public Task<RouteBalanceTotals> GetTotalsAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken) => Task.FromResult(Totals);
        public Task<RouteBalanceTotals> GetTotalsSinceAsync(Guid tenantId, Guid routeId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) => Task.FromResult(TotalsSince ?? new RouteBalanceTotals(0m, 0m, 0m, 0m));
        public Task<IReadOnlyCollection<RouteFinancialEntry>> ListEntriesAsync(Guid tenantId, Guid routeId, IReadOnlyCollection<RouteFinancialEntryType> types, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<RouteFinancialEntry>>(Entry is null ? [] : [Entry]);
        public Task<RouteFinancialEntry?> FindEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken) => Task.FromResult(Entry?.Id == entryId ? Entry : null);
        public Task AddEntryAsync(RouteFinancialEntry entry, CancellationToken cancellationToken) { Entry = entry; return Task.CompletedTask; }
        public Task RemoveEntryAsync(RouteFinancialEntry entry, CancellationToken cancellationToken) { Entry = null; return Task.CompletedTask; }
        public Task<RouteDayClosing?> FindDayClosingAsync(Guid tenantId, Guid routeId, DateOnly businessDate, CancellationToken cancellationToken) => Task.FromResult(Closing);
        public Task<RouteDayClosing?> FindActiveOpenClosingAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken) => Task.FromResult(Closing?.IsClosed == false ? Closing : null);
        public Task<RouteDayClosing?> FindLatestDayClosingAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken) => Task.FromResult(Closing);
        public Task SaveDayClosingAsync(RouteDayClosing closing, CancellationToken cancellationToken) { Closing = closing; return Task.CompletedTask; }
        public Task AddAuditLogAsync(FinancialMovementAuditLog auditLog, CancellationToken cancellationToken) { Audits.Add(auditLog); return Task.CompletedTask; }
        public Task<IReadOnlyCollection<FinancialMovementAuditLog>> ListAuditLogsAsync(Guid tenantId, Guid? routeId, DateTimeOffset? fromTimestampUtc, DateTimeOffset? toTimestampUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<FinancialMovementAuditLog>>(Audits);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
