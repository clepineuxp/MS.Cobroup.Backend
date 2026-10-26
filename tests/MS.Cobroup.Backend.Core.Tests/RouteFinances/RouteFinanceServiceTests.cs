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
        Assert.Equal(1_000m, result.Balance.InitialBalance);
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
        public RouteBalanceTotals Totals { get; set; } = new(0m, 0m, 0m, 0m);
        public RouteFinancialEntry? Entry { get; set; }
        public Task<RouteBalanceTotals> GetTotalsAsync(Guid tenantId, Guid routeId, CancellationToken cancellationToken) => Task.FromResult(Totals);
        public Task<IReadOnlyCollection<RouteFinancialEntry>> ListEntriesAsync(Guid tenantId, Guid routeId, IReadOnlyCollection<RouteFinancialEntryType> types, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<RouteFinancialEntry>>(Entry is null ? [] : [Entry]);
        public Task<RouteFinancialEntry?> FindEntryAsync(Guid tenantId, Guid entryId, CancellationToken cancellationToken) => Task.FromResult(Entry?.Id == entryId ? Entry : null);
        public Task AddEntryAsync(RouteFinancialEntry entry, CancellationToken cancellationToken) { Entry = entry; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
