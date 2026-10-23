using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MS.Cobroup.Backend.Infrastructure.Persistence;

public sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    public OperationsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OperationsDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OperationsConnection")
            ?? "Host=localhost;Database=operations_design;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);
        return new OperationsDbContext(optionsBuilder.Options);
    }
}
