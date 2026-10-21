using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/v1/session")]
[Authorize(Policy = "tenant-user")]
public sealed class SessionController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentSession() => Ok(new
    {
        userId = User.FindFirstValue(OpenIddictConstants.Claims.Subject),
        organizationId = User.FindFirstValue("organization_id"),
        organizationCode = User.FindFirstValue("organization_code"),
        username = User.FindFirstValue(OpenIddictConstants.Claims.PreferredUsername),
        displayName = User.FindFirstValue(OpenIddictConstants.Claims.Name),
        role = User.FindFirstValue(OpenIddictConstants.Claims.Role)
    });
}
