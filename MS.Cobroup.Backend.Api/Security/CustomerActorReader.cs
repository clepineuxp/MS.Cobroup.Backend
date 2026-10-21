using System.Security.Claims;
using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Domain.Authorization;
using OpenIddict.Abstractions;

namespace MS.Cobroup.Backend.Api.Security;

public static class CustomerActorReader
{
    public static bool TryRead(ClaimsPrincipal principal, out CustomerActor actor)
    {
        var subject = principal.FindFirstValue(OpenIddictConstants.Claims.Subject);
        var tenant = principal.FindFirstValue("organization_id");
        var role = principal.FindFirstValue(OpenIddictConstants.Claims.Role);
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
