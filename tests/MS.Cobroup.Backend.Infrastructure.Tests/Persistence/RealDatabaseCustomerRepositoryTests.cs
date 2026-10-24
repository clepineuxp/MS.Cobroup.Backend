using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Infrastructure.Persistence;
using Xunit.Sdk;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Persistence;

public sealed class RealDatabaseCustomerRepositoryTests
{
    private const string UserSecretsId = "894d43b4-c41d-4fa3-b335-55b09dd87560";

    [Fact]
    public async Task Concurrent_creates_cannot_exceed_route_capacity()
    {
        var connectionString = ReadOperationsConnectionStringOrSkip();

        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var first = CreateCustomer(tenantId, routeId, "Ana");
        var second = CreateCustomer(tenantId, routeId, "Beatriz");

        try
        {
            await using var firstContext = CreateContext(connectionString);
            await using var secondContext = CreateContext(connectionString);
            var firstRepository = new EfCustomerRepository(firstContext);
            var secondRepository = new EfCustomerRepository(secondContext);

            var outcomes = await Task.WhenAll(
                firstRepository.TryAddAsync(first, CreateAudit(first), 1, default),
                secondRepository.TryAddAsync(second, CreateAudit(second), 1, default));

            Assert.Single(outcomes, outcome => outcome == CustomerSaveOutcome.Saved);
            Assert.Single(outcomes, outcome => outcome == CustomerSaveOutcome.CapacityReached);
        }
        finally
        {
            await CleanupAsync(connectionString, tenantId);
        }
    }

    [Fact]
    public async Task Add_and_update_persist_audit_without_contact_conflicts()
    {
        var connectionString = ReadOperationsConnectionStringOrSkip();

        var tenantId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var customer = CreateCustomer(tenantId, routeId, "Ana");

        try
        {
            await using (var createContext = CreateContext(connectionString))
            {
                var createRepository = new EfCustomerRepository(createContext);
                Assert.Equal(
                    CustomerSaveOutcome.Saved,
                    await createRepository.TryAddAsync(customer, CreateAudit(customer), null, default));
            }

            await using var updateContext = CreateContext(connectionString);
            var repository = new EfCustomerRepository(updateContext);
            var persistedCustomer = await repository.FindAsync(tenantId, customer.Id, default);
            Assert.NotNull(persistedCustomer);
            var previousStatus = persistedCustomer.Status;
            persistedCustomer.Update(
                routeId,
                "Ana María",
                "Pérez",
                null,
                null,
                CustomerStatus.Active,
                [new("300 111 2233", true)],
                [new(4.711m, -74.072m, "Casa", null, true, LocationSource.Manual)],
                Guid.NewGuid(),
                DateTimeOffset.UtcNow, "001234567");
            var updateAudit = CustomerAuditRecord.Create(
                tenantId,
                persistedCustomer.Id,
                persistedCustomer.UpdatedByUserId,
                CustomerAuditAction.Updated,
                "{}",
                persistedCustomer.UpdatedAtUtc);

            Assert.Equal(
                CustomerSaveOutcome.Saved,
                await repository.TryUpdateAsync(persistedCustomer, updateAudit, routeId, previousStatus, null, default));

            updateContext.ChangeTracker.Clear();
            var persisted = await repository.FindAsync(tenantId, customer.Id, default);
            Assert.Equal("Ana María", persisted?.FirstNames);
            Assert.Equal(2, await updateContext.CustomerAuditRecords.CountAsync(record => record.CustomerId == customer.Id));
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

    private static Customer CreateCustomer(Guid tenantId, Guid routeId, string firstNames) => Customer.Create(
        tenantId,
        routeId,
        firstNames,
        "Pérez",
        null,
        null,
        [new("3001112233", true)],
        [new(4.711m, -74.072m, "Casa", null, true, LocationSource.Manual)],
        Guid.NewGuid(),
        DateTimeOffset.UtcNow, "001234567");

    private static CustomerAuditRecord CreateAudit(Customer customer) => CustomerAuditRecord.Create(
        customer.TenantId,
        customer.Id,
        customer.CreatedByUserId,
        CustomerAuditAction.Created,
        "{}",
        customer.CreatedAtUtc);

    private static async Task CleanupAsync(string connectionString, Guid tenantId)
    {
        await using var context = CreateContext(connectionString);
        await context.CustomerAuditRecords.Where(record => record.TenantId == tenantId).ExecuteDeleteAsync();
        await context.Customers.Where(customer => customer.TenantId == tenantId).ExecuteDeleteAsync();
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
