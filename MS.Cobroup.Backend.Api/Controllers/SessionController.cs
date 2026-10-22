using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/session")]
[Route("api/v1/session")]
[Authorize(Policy = "tenant-user")]
public sealed class SessionController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentSession() => Ok(new
    {
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("user_id"),
        organizationId = User.FindFirstValue("organization_id"),
        organizationCode = User.FindFirstValue("organization_code"),
        username = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("preferred_username") ?? User.FindFirstValue("unique_name"),
        displayName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name"),
        role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role")
    });
}
