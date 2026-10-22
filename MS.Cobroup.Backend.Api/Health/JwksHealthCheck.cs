using Microsoft.Extensions.Diagnostics.HealthChecks;
using MS.Cobroup.Backend.Api.Security;

namespace MS.Cobroup.Backend.Api.Health;

public sealed class JwksHealthCheck(IJwksValidationService jwksService, IHostEnvironment environment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (environment.IsDevelopment() || jwksService.HasKeys)
        {
            return Task.FromResult(HealthCheckResult.Healthy("JWKS validation keys are available."));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("JWKS validation keys are not available."));
    }
}
