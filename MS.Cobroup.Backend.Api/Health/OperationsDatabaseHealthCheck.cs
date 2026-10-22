using Microsoft.Extensions.Diagnostics.HealthChecks;
using MS.Cobroup.Backend.Infrastructure.Persistence;

namespace MS.Cobroup.Backend.Api.Health;

public sealed class OperationsDatabaseHealthCheck(OperationsDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Operations database connection is healthy.")
                : HealthCheckResult.Unhealthy("Cannot connect to operations database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Operations database error: {ex.Message}");
        }
    }
}
