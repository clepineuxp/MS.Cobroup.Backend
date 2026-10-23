using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

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

        string? alg = null;
        string? kid = null;
        Func<string, string?> getClaimFromToken;

        if (securityToken is JsonWebToken jsonWebToken)
        {
            alg = jsonWebToken.Alg;
            kid = jsonWebToken.Kid;
            getClaimFromToken = claimType => jsonWebToken.TryGetClaim(claimType, out var claim) ? claim.Value : null;
        }
        else if (securityToken is JwtSecurityToken jwt)
        {
            alg = jwt.Header.Alg;
            kid = jwt.Header.Kid;
            getClaimFromToken = claimType => jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
        }
        else
        {
            return AccessJwtValidationResult.Failure("Security token is not a recognized JWT type (expected JsonWebToken or JwtSecurityToken).");
        }

        // 1. Algoritmo obligatorio: HS256
        if (!string.Equals(alg, SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
        {
            return AccessJwtValidationResult.Failure($"Algorithm '{alg}' is not allowed. Only HS256 is accepted.");
        }

        // 2. Header kid obligatorio y no vacío
        if (string.IsNullOrWhiteSpace(kid))
        {
            return AccessJwtValidationResult.Failure("Header 'kid' is missing or empty.");
        }

        // 3. Claim typ obligatorio y debe ser 'access' (nunca refresh)
        var typ = principal.FindFirst("typ")?.Value ?? getClaimFromToken("typ");
        if (!string.Equals(typ, "access", StringComparison.Ordinal))
        {
            return AccessJwtValidationResult.Failure($"Token 'typ' claim must be 'access'. Found: '{typ}'");
        }

        // 4. Claim sub obligatorio y GUID válido
        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value 
            ?? principal.FindFirst("sub")?.Value 
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? getClaimFromToken(JwtRegisteredClaimNames.Sub)
            ?? getClaimFromToken("sub");
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out _))
        {
            return AccessJwtValidationResult.Failure("Claim 'sub' is missing, empty, or not a valid GUID.");
        }

        // 5. Claim organization_id obligatorio y GUID válido
        var orgId = principal.FindFirst("organization_id")?.Value
            ?? getClaimFromToken("organization_id");
        if (string.IsNullOrWhiteSpace(orgId) || !Guid.TryParse(orgId, out _))
        {
            return AccessJwtValidationResult.Failure("Claim 'organization_id' is missing, empty, or not a valid GUID.");
        }

        // 6. Claim organization_code obligatorio y no vacío
        var orgCode = principal.FindFirst("organization_code")?.Value
            ?? getClaimFromToken("organization_code");
        if (string.IsNullOrWhiteSpace(orgCode))
        {
            return AccessJwtValidationResult.Failure("Claim 'organization_code' is missing or empty.");
        }

        // 7. Claim jti obligatorio y no vacío
        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value 
            ?? principal.FindFirst("jti")?.Value
            ?? getClaimFromToken(JwtRegisteredClaimNames.Jti)
            ?? getClaimFromToken("jti");
        if (string.IsNullOrWhiteSpace(jti))
        {
            return AccessJwtValidationResult.Failure("Claim 'jti' is missing or empty.");
        }

        // 8. Claim role obligatorio y perteneciente a los roles COBROUP permitidos
        var role = principal.FindFirst("role")?.Value 
            ?? principal.FindFirst(ClaimTypes.Role)?.Value
            ?? getClaimFromToken("role")
            ?? getClaimFromToken(ClaimTypes.Role);
        if (string.IsNullOrWhiteSpace(role) || !AllowedRoles.Contains(role))
        {
            return AccessJwtValidationResult.Failure($"Claim 'role' is missing or not an allowed COBROUP role. Found: '{role}'");
        }

        // 9. Claim iat obligatorio y entero/timestamp válido (I-18)
        var iat = principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value
            ?? principal.FindFirst("iat")?.Value
            ?? getClaimFromToken(JwtRegisteredClaimNames.Iat)
            ?? getClaimFromToken("iat");
        if (string.IsNullOrWhiteSpace(iat) || !long.TryParse(iat, out _))
        {
            return AccessJwtValidationResult.Failure("Claim 'iat' is missing, empty, or not a valid timestamp.");
        }

        // 10. Claim nbf obligatorio y entero/timestamp válido (I-18)
        var nbf = principal.FindFirst(JwtRegisteredClaimNames.Nbf)?.Value
            ?? principal.FindFirst("nbf")?.Value
            ?? getClaimFromToken(JwtRegisteredClaimNames.Nbf)
            ?? getClaimFromToken("nbf");
        if (string.IsNullOrWhiteSpace(nbf) || !long.TryParse(nbf, out _))
        {
            return AccessJwtValidationResult.Failure("Claim 'nbf' is missing, empty, or not a valid timestamp.");
        }

        return AccessJwtValidationResult.Success();
    }
}
