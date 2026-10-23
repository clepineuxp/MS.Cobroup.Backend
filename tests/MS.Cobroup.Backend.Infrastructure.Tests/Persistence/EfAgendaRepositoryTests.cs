using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Loans;
using MS.Cobroup.Backend.Infrastructure.Persistence;
using Xunit;
using Xunit.Sdk;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Persistence;

public sealed class EfAgendaRepositoryTests
{
    private const string UserSecretsId = "894d43b4-c41d-4fa3-b335-55b09dd87560";

    [Fact]
    public async Task GetAgendaCustomersAsync_filters_by_tenant_and_accessible_routes()
    {
        var connectionString = ReadOperationsConnectionStringOrSkip();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var route1 = Guid.NewGuid();
        var route2 = Guid.NewGuid();

        try
        {
            await using var context = CreateContext(connectionString);
            var cust1 = Customer.Create(
                tenantId,
                route1,
                "Juan",
                "Perez",
                "Ferreteria Central",
                null,
                [new CustomerPhoneValue("3001112233", true)],
                [new CustomerLocationValue(4.60m, -74.08m, "Sede Principal", "Av 10", true, LocationSource.Gps)],
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);

            var custOtherTenant = Customer.Create(
                otherTenantId,
                route1,
                "Pedro",
                "Gomez",
                null,
                null,
                [new CustomerPhoneValue("3009998877", true)],
                [new CustomerLocationValue(4.61m, -74.09m, null, null, true, LocationSource.Manual)],
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);

            await context.Customers.AddRangeAsync(cust1, custOtherTenant);
            await context.SaveChangesAsync();

            var repo = new EfAgendaRepository(context);
            var targetDate = DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await repo.GetAgendaCustomersAsync(tenantId, [route1, route2], null, targetDate, default);

            Assert.Single(result);
            Assert.Equal(cust1.Id, result.First().CustomerId);
            Assert.Equal("Juan Perez", result.First().CustomerName);
            Assert.NotNull(result.First().Location);
            Assert.Equal(4.60m, result.First().Location!.Latitude);
        }
        finally
        {
            await CleanupAsync(connectionString, tenantId);
            await CleanupAsync(connectionString, otherTenantId);
        }
    }

    [Fact]
    public async Task GetRoutePortfolioDataAsync_calculates_portfolio_metrics_and_aging()
    {
        var connectionString = ReadOperationsConnectionStringOrSkip();
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();

        try
        {
            await using var context = CreateContext(connectionString);
            var cust = Customer.Create(
                tenantId,
                routeId,
                "Maria",
                "Suarez",
                null,
                null,
                [new CustomerPhoneValue("3001234567", true)],
                [new CustomerLocationValue(4.70m, -74.10m, null, null, true, LocationSource.Manual)],
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);

            await context.Customers.AddAsync(cust);

            var targetDate = new DateOnly(2026, 10, 21);
            var installments = new List<Installment>
            {
                new(Guid.NewGuid(), tenantId, Guid.Empty, 1, targetDate.AddDays(-10), 500m, 0m, InstallmentStatus.Pending),
                new(Guid.NewGuid(), tenantId, Guid.Empty, 2, targetDate, 500m, 0m, InstallmentStatus.Pending)
            };

            var terms = new LoanTermsSnapshot(
                800m,
                25m,
                200m,
                1000m,
                0m,
                1000m,
                PaymentFrequency.Daily,
                null,
                2,
                DateTimeOffset.UtcNow.AddDays(-10),
                DateTimeOffset.UtcNow);

            var loan = Loan.Originate(
                tenantId,
                cust.Id,
                routeId,
                null,
                null,
                null,
                terms,
                Guid.NewGuid(),
                UserRole.Collector,
                DateTimeOffset.UtcNow,
                installments);

            await context.Loans.AddAsync(loan);
            await context.SaveChangesAsync();

            var repo = new EfAgendaRepository(context);
            var result = await repo.GetRoutePortfolioDataAsync(tenantId, [routeId], routeId, targetDate, default);

            Assert.Single(result);
            var data = result.First();
            Assert.Equal(1, data.ActiveLoansCount);
            Assert.Equal(1, data.ActiveCustomersCount);
            Assert.Equal(1000m, data.TotalPlacedAmount);
            Assert.Equal(1000m, data.TotalBalanceAmount);
            Assert.Equal(500m, data.OverdueBalanceAmount);
            Assert.Equal(50.00m, data.PortfolioAtRiskRate); // 500 / 1000 = 50%
            Assert.Equal(5, data.AgingBuckets.Count);
        }
        finally
        {
            await CleanupAsync(connectionString, tenantId);
        }
    }

    private static OperationsDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new OperationsDbContext(options);
    }

    private static async Task CleanupAsync(string connectionString, Guid tenantId)
    {
        await using var context = CreateContext(connectionString);
        await context.Installments.Where(i => i.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Loans.Where(l => l.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Customers.Where(c => c.TenantId == tenantId).ExecuteDeleteAsync();
    }

    private static string ReadOperationsConnectionStringOrSkip()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var secretsPath = Path.Combine(appData, "Microsoft", "UserSecrets", UserSecretsId, "secrets.json");
        if (!File.Exists(secretsPath))
        {
            throw SkipException.ForSkip("The local OperationsDatabase user secret is unavailable.");
        }

        var text = File.ReadAllText(secretsPath);
        var match = System.Text.RegularExpressions.Regex.Match(text, @"""ConnectionStrings:OperationsDatabase""\s*:\s*""([^""]+)""");
        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
        {
            throw SkipException.ForSkip("The local OperationsDatabase connection string is unavailable.");
        }

        return match.Groups[1].Value;
    }
}
