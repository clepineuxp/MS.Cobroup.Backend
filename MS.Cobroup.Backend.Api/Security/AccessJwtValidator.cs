using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.Backend.Api.Security;

public readonly record struct AccessJwtValidationResult(bool IsValid, string? FailureReason)
{
    public static AccessJwtValidationResult Success() => new(true, null);
    public static AccessJwtValidationResult Failure(string reason) => new(false, reason);
}

public static class AccessJwtValidator
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Supervisor",
        "Collector"
    };

    public static AccessJwtValidationResult Validate(ClaimsPrincipal? principal, SecurityToken? securityToken)
    {
        if (principal is null)
        {
            return AccessJwtValidationResult.Failure("Principal is null.");
        }

        if (securityToken is not JwtSecurityToken jwt)
        {
            return AccessJwtValidationResult.Failure("Security token is not a JwtSecurityToken.");
        }

        // 1. Algoritmo obligatorio: RS256
        if (!string.Equals(jwt.Header.Alg, SecurityAlgorithms.RsaSha256, StringComparison.OrdinalIgnoreCase))
        {
            return AccessJwtValidationResult.Failure($"Algorithm '{jwt.Header.Alg}' is not allowed. Only RS256 is accepted.");
        }

        // 2. Header kid obligatorio y no vacío
        if (string.IsNullOrWhiteSpace(jwt.Header.Kid))
        {
            return AccessJwtValidationResult.Failure("Header 'kid' is missing or empty.");
        }

        // 3. Claim typ obligatorio y debe ser 'access' (nunca refresh)
        var typ = principal.FindFirst("typ")?.Value ?? jwt.Claims.FirstOrDefault(c => c.Type == "typ")?.Value;
        if (!string.Equals(typ, "access", StringComparison.Ordinal))
        {
            return AccessJwtValidationResult.Failure($"Token 'typ' claim must be 'access'. Found: '{typ}'");
        }

        // 4. Claim sub obligatorio y GUID válido
        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value 
            ?? principal.FindFirst("sub")?.Value 
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out _))
        {
            return AccessJwtValidationResult.Failure("Claim 'sub' is missing, empty, or not a valid GUID.");
        }

        // 5. Claim organization_id obligatorio y GUID válido
        var orgId = principal.FindFirst("organization_id")?.Value
            ?? jwt.Claims.FirstOrDefault(c => c.Type == "organization_id")?.Value;
        if (string.IsNullOrWhiteSpace(orgId) || !Guid.TryParse(orgId, out _))
        {
            return AccessJwtValidationResult.Failure("Claim 'organization_id' is missing, empty, or not a valid GUID.");
        }

        // 6. Claim organization_code obligatorio y no vacío
        var orgCode = principal.FindFirst("organization_code")?.Value
            ?? jwt.Claims.FirstOrDefault(c => c.Type == "organization_code")?.Value;
        if (string.IsNullOrWhiteSpace(orgCode))
        {
            return AccessJwtValidationResult.Failure("Claim 'organization_code' is missing or empty.");
        }

        // 7. Claim jti obligatorio y no vacío
        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value 
            ?? principal.FindFirst("jti")?.Value
            ?? jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti || c.Type == "jti")?.Value;
        if (string.IsNullOrWhiteSpace(jti))
        {
            return AccessJwtValidationResult.Failure("Claim 'jti' is missing or empty.");
        }

        // 8. Claim role obligatorio y perteneciente a los roles COBROUP permitidos
        var role = principal.FindFirst("role")?.Value 
            ?? principal.FindFirst(ClaimTypes.Role)?.Value
            ?? jwt.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value;
        if (string.IsNullOrWhiteSpace(role) || !AllowedRoles.Contains(role))
        {
            return AccessJwtValidationResult.Failure($"Claim 'role' is missing or not an allowed COBROUP role. Found: '{role}'");
        }

        return AccessJwtValidationResult.Success();
    }
}
