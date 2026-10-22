using System.Security.Claims;
using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Domain.Authorization;

namespace MS.Cobroup.Backend.Api.Security;

public static class CustomerActorReader
{
    public static bool TryRead(ClaimsPrincipal principal, out CustomerActor actor)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? principal.FindFirstValue("user_id");
        var tenant = principal.FindFirstValue("organization_id");
        var role = principal.FindFirstValue(ClaimTypes.Role)
            ?? principal.FindFirstValue("role");

        if (!Guid.TryParse(subject, out var userId)
            || !Guid.TryParse(tenant, out var tenantId)
            || !Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            actor = null!;
            return false;
        }

        actor = new CustomerActor(userId, tenantId, parsedRole);
        return true;
    }

    public static string ReadBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : string.Empty;
    }
}
