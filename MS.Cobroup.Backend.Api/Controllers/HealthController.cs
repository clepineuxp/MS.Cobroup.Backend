using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    [HttpGet("/health")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        return report.Status is HealthStatus.Healthy
            ? Ok("Healthy")
            : StatusCode(StatusCodes.Status503ServiceUnavailable, report.Status.ToString());
    }
}
