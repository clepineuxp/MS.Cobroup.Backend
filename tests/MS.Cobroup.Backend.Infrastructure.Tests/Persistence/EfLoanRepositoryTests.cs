using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Loans;
using MS.Cobroup.Backend.Infrastructure.Persistence;
using Xunit.Sdk;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Persistence;

public sealed class EfLoanRepositoryTests
{
    private const string UserSecretsId = "894d43b4-c41d-4fa3-b335-55b09dd87560";

    [Fact]
    public async Task FindAsync_retrieves_loan_with_installments_and_respects_tenant()
    {
        var connectionString = ReadOperationsConnectionStringOrSkip();
        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();

        try
        {
            await using var context = CreateContext(connectionString);
            var customer = CreateCustomer(tenantId, routeId);
            context.Customers.Add(customer);

            var now = DateTimeOffset.UtcNow;
            var terms = LoanTermsSnapshot.Create(
                1000m,
                10m,
                100m,
                1100m,
                PaymentFrequency.Daily,
                null,
                2,
                now,
                now.AddDays(2));

            var loanId = Guid.NewGuid();
            var installments = new List<Installment>
            {
                Installment.Create(tenantId, loanId, 1, DateOnly.FromDateTime(DateTime.UtcNow), 550m),
                Installment.Create(tenantId, loanId, 2, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), 550m)
            };

            var loan = Loan.Originate(
                tenantId,
                customer.Id,
                routeId,
                null,
                null,
                null,
                terms,
                Guid.NewGuid(),
                UserRole.Collector,
                now,
                installments);

            var repository = new EfLoanRepository(context);
            await repository.AddAsync(loan, default);
            await repository.SaveChangesAsync(default);
            context.ChangeTracker.Clear();

            var retrieved = await repository.FindAsync(tenantId, loan.Id, default);
            Assert.NotNull(retrieved);
            Assert.Equal(loan.Id, retrieved.Id);
            Assert.Equal(2, retrieved.Installments.Count);
            Assert.Equal(1000m, retrieved.Terms.PrincipalAmount);
            Assert.Equal(1100m, retrieved.Terms.TotalAmount);

            var otherTenantResult = await repository.FindAsync(Guid.NewGuid(), loan.Id, default);
            Assert.Null(otherTenantResult);
        }
        finally
        {
            await CleanupAsync(connectionString, tenantId);
        }
    }

    [Fact]
    public async Task CountActiveLoansByRouteAsync_counts_only_active_loans_for_given_route_and_tenant()
    {
        var connectionString = ReadOperationsConnectionStringOrSkip();
        var tenantId = Guid.NewGuid();
        var routeA = Guid.NewGuid();
        var routeB = Guid.NewGuid();

        try
        {
            await using var context = CreateContext(connectionString);
            var customerA = CreateCustomer(tenantId, routeA);
            var customerB = CreateCustomer(tenantId, routeB);
            context.Customers.AddRange(customerA, customerB);

            var now = DateTimeOffset.UtcNow;
            var terms = LoanTermsSnapshot.Create(500m, 10m, 50m, 550m, PaymentFrequency.Daily, null, 1, now, now.AddDays(1));

            var loan1 = Loan.Originate(tenantId, customerA.Id, routeA, null, null, null, terms, Guid.NewGuid(), UserRole.Collector, now,
                [Installment.Create(tenantId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow), 550m)]);

            var loan2 = Loan.Originate(tenantId, customerB.Id, routeB, null, null, null, terms, Guid.NewGuid(), UserRole.Collector, now,
                [Installment.Create(tenantId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow), 550m)]);

            var repository = new EfLoanRepository(context);
            await repository.AddAsync(loan1, default);
            await repository.AddAsync(loan2, default);
            await repository.SaveChangesAsync(default);
            context.ChangeTracker.Clear();

            var countRouteA = await repository.CountActiveLoansByRouteAsync(tenantId, routeA, default);
            var countRouteB = await repository.CountActiveLoansByRouteAsync(tenantId, routeB, default);

            Assert.Equal(1, countRouteA);
            Assert.Equal(1, countRouteB);
        }
        finally
        {
            await CleanupAsync(connectionString, tenantId);
        }
    }

    [Fact]
    public async Task SearchAsync_filters_by_accessible_routes_and_customer()
    {
        var connectionString = ReadOperationsConnectionStringOrSkip();
        var tenantId = Guid.NewGuid();
        var route1 = Guid.NewGuid();
        var route2 = Guid.NewGuid();

        try
        {
            await using var context = CreateContext(connectionString);
            var customer1 = CreateCustomer(tenantId, route1);
            var customer2 = CreateCustomer(tenantId, route2);
            context.Customers.AddRange(customer1, customer2);

            var now = DateTimeOffset.UtcNow;
            var terms = LoanTermsSnapshot.Create(500m, 10m, 50m, 550m, PaymentFrequency.Daily, null, 1, now, now.AddDays(1));

            var loan1 = Loan.Originate(tenantId, customer1.Id, route1, null, null, null, terms, Guid.NewGuid(), UserRole.Collector, now,
                [Installment.Create(tenantId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow), 550m)]);
            var loan2 = Loan.Originate(tenantId, customer2.Id, route2, null, null, null, terms, Guid.NewGuid(), UserRole.Collector, now,
                [Installment.Create(tenantId, Guid.NewGuid(), 1, DateOnly.FromDateTime(DateTime.UtcNow), 550m)]);

            var repository = new EfLoanRepository(context);
            await repository.AddAsync(loan1, default);
            await repository.AddAsync(loan2, default);
            await repository.SaveChangesAsync(default);
            context.ChangeTracker.Clear();

            // Search with access only to route1
            var searchResult = await repository.SearchAsync(tenantId, [route1], null, null, null, 1, 10, default);
            Assert.Equal(1, searchResult.TotalCount);
            var item = Assert.Single(searchResult.Items);
            Assert.Equal(loan1.Id, item.Id);
            Assert.Equal("Carlos Mendoza", item.CustomerName);
            Assert.Equal(500m, item.PrincipalAmount);
        }
        finally
        {
            await CleanupAsync(connectionString, tenantId);
        }
    }

    private static Customer CreateCustomer(Guid tenantId, Guid routeId) =>
        Customer.Create(
            tenantId,
            routeId,
            "Carlos",
            "Mendoza",
            "Comercio Carlos",
            null,
            [new("3009998877", true)],
            [new(4.711m, -74.072m, "Local Principal", null, true, LocationSource.Manual)],
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

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
        var configuredConnection = Environment.GetEnvironmentVariable("ConnectionStrings__OperationsDatabase");
        if (!string.IsNullOrWhiteSpace(configuredConnection))
        {
            return configuredConnection;
        }

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
