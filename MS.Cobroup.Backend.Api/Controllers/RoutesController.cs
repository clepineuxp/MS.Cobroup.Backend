using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Core.Routes;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/routes")]
[Authorize(Policy = "tenant-user")]
public sealed class RoutesController(ListAccessibleRoutesService routes) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        var accessToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : string.Empty;
        var result = await routes.ExecuteAsync(accessToken, cancellationToken);
        return result.Outcome switch
        {
            RouteDirectoryOutcome.Success => Ok(result.Routes),
            RouteDirectoryOutcome.Unauthorized => Unauthorized(),
            RouteDirectoryOutcome.Forbidden => Forbid(),
            RouteDirectoryOutcome.Unavailable => Problem(
                "The administrative route directory is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem("Unexpected route directory result.")
        };
    }
}
