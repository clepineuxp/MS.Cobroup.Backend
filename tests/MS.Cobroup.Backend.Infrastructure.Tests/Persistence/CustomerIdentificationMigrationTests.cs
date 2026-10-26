using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Infrastructure.Persistence;
using MS.Cobroup.Backend.Infrastructure.Persistence.Migrations;
using Npgsql;
using Xunit.Sdk;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Persistence;

public sealed class CustomerIdentificationMigrationTests
{
    [Fact]
    public void Identification_is_required_without_a_database_default()
    {
        using var context = new OperationsDbContext(new DbContextOptionsBuilder<OperationsDbContext>()
            .UseNpgsql("Host=localhost;Database=design;Username=design").Options);
        var property = context.Model.FindEntityType(typeof(Customer))!.FindProperty(nameof(Customer.Identification))!;
        Assert.False(property.IsNullable);
        Assert.Equal(64, property.GetMaxLength());
        Assert.Null(property.GetDefaultValue());
    }

    [Fact]
    public async Task Migration_backfills_only_existing_rows_and_does_not_default_new_rows()
    {
        var secretsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "UserSecrets", "894d43b4-c41d-4fa3-b335-55b09dd87560", "secrets.json");
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OperationsDatabase");
        if (string.IsNullOrWhiteSpace(connectionString) && File.Exists(secretsPath))
        {
            using var secrets = JsonDocument.Parse(await File.ReadAllTextAsync(secretsPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            connectionString = secrets.RootElement.GetProperty("ConnectionStrings:OperationsDatabase").GetString();
        }
        if (string.IsNullOrWhiteSpace(connectionString)) throw SkipException.ForSkip("The operations integration database is unavailable.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(builder.Database, new[] { "dev_cobroup_operations", "cobroup_operations_tests" });
        using var context = new OperationsDbContext(new DbContextOptionsBuilder<OperationsDbContext>().UseNpgsql(connectionString).Options);
        var migration = new AddCustomerIdentification();
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, context.Model);
        var sql = string.Join("\n", commands.Select(command => command.CommandText));
        Assert.Contains("operations.customers", sql);
        sql = sql.Replace("operations.customers", "pg_temp.customer_identification_qa", StringComparison.Ordinal);
        Assert.DoesNotContain("operations.customers", sql);
        Assert.DoesNotContain("\"operations\"", sql);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        // All writes are confined to a temporary table and the transaction is rolled back.
        await new NpgsqlCommand("CREATE TEMP TABLE customer_identification_qa (\"Id\" integer PRIMARY KEY); INSERT INTO customer_identification_qa VALUES (1);", connection, transaction).ExecuteNonQueryAsync();
        await new NpgsqlCommand(sql, connection, transaction).ExecuteNonQueryAsync();
        var legacy = await new NpgsqlCommand("SELECT \"Identification\" FROM customer_identification_qa WHERE \"Id\" = 1;", connection, transaction).ExecuteScalarAsync();
        Assert.Equal("0", legacy);
        await transaction.SaveAsync("missing_id");
        var error = await Assert.ThrowsAsync<PostgresException>(() => new NpgsqlCommand("INSERT INTO customer_identification_qa (\"Id\") VALUES (2);", connection, transaction).ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.NotNullViolation, error.SqlState);
        await transaction.RollbackAsync("missing_id");
        await new NpgsqlCommand("INSERT INTO customer_identification_qa (\"Id\", \"Identification\") VALUES (2, '001234');", connection, transaction).ExecuteNonQueryAsync();
        Assert.Equal("001234", await new NpgsqlCommand("SELECT \"Identification\" FROM customer_identification_qa WHERE \"Id\" = 2;", connection, transaction).ExecuteScalarAsync());
        await transaction.RollbackAsync();
    }
}
