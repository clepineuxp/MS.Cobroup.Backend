using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.Backend.Api.Security;

public interface IJwksValidationService
{
    TokenValidationParameters TokenValidationParameters { get; }
}

public sealed class JwksValidationService : IJwksValidationService
{
    private readonly HttpClient httpClient;
    private readonly string? jwksUri;
    private readonly string issuer;
    private readonly string audience;
    private readonly List<SecurityKey> staticPublicKeys = [];
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private JsonWebKeySet? cachedJwks;
    private DateTimeOffset nextRefreshAllowedAtUtc = DateTimeOffset.MinValue;

    public TokenValidationParameters TokenValidationParameters { get; }

    public JwksValidationService(
        IConfiguration configuration,
        IHostEnvironment environment,
        HttpClient httpClient)
    {
        this.httpClient = httpClient;

        issuer = configuration["Identity:Issuer"]
            ?? throw new InvalidOperationException("Identity:Issuer is required.");
        audience = configuration["Identity:Audience"] ?? "cobroup-api";

        var adminBaseUrl = configuration["AdministrativeApi:BaseUrl"] ?? "http://localhost:5001";
        jwksUri = configuration["Identity:JwksUri"]
            ?? configuration["Identity:JwksUrl"]
            ?? $"{adminBaseUrl.TrimEnd('/')}/api/v1/auth/.well-known/jwks.json";

        var publicKeyPem = configuration["Jwt:PublicKeyPem"];
        var configuredKid = configuration["Jwt:KeyId"] ?? configuration["Jwt:Kid"];

        if (!environment.IsDevelopment())
        {
            if (!issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Identity:Issuer must be a valid HTTPS URL outside Development. Current: '{issuer}'");
            }

            if (!string.Equals(audience, "cobroup-api", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Identity:Audience must be 'cobroup-api'. Current: '{audience}'");
            }
        }

        if (!string.IsNullOrWhiteSpace(publicKeyPem))
        {
            var rsa = RSA.Create();
            ImportPublicKey(rsa, publicKeyPem);
            var key = new RsaSecurityKey(rsa) { KeyId = configuredKid };
            staticPublicKeys.Add(key);
        }

        TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            IssuerSigningKeyResolver = ResolveSigningKey
        };
    }

    public IEnumerable<SecurityKey> ResolveSigningKey(
        string token,
        SecurityToken securityToken,
        string kid,
        TokenValidationParameters validationParameters)
    {
        if (staticPublicKeys.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(kid))
            {
                var match = staticPublicKeys.FirstOrDefault(k => k.KeyId == kid);
                if (match is not null) return [match];
            }
            else
            {
                return staticPublicKeys;
            }
        }

        // Try cached JWKS
        if (cachedJwks is not null && !string.IsNullOrWhiteSpace(kid))
        {
            var matching = cachedJwks.Keys.FirstOrDefault(k => k.Kid == kid);
            if (matching is not null)
            {
                return [matching];
            }
        }

        // Single retry on unknown kid
        if (DateTimeOffset.UtcNow >= nextRefreshAllowedAtUtc && !string.IsNullOrWhiteSpace(jwksUri))
        {
            try
            {
                refreshLock.Wait(TimeSpan.FromSeconds(3));
                try
                {
                    if (DateTimeOffset.UtcNow >= nextRefreshAllowedAtUtc)
                    {
                        var response = httpClient.GetStringAsync(jwksUri).GetAwaiter().GetResult();
                        cachedJwks = new JsonWebKeySet(response);
                        nextRefreshAllowedAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);

                        if (!string.IsNullOrWhiteSpace(kid))
                        {
                            var matching = cachedJwks.Keys.FirstOrDefault(k => k.Kid == kid);
                            if (matching is not null)
                            {
                                return [matching];
                            }
                        }
                    }
                }
                finally
                {
                    refreshLock.Release();
                }
            }
            catch
            {
                // Inability to fetch JWKS falls through to rejection
            }
        }

        return [];
    }

    private static void ImportPublicKey(RSA rsa, string pem)
    {
        var clean = pem
            .Replace("-----BEGIN PUBLIC KEY-----", "")
            .Replace("-----END PUBLIC KEY-----", "")
            .Replace("-----BEGIN RSA PUBLIC KEY-----", "")
            .Replace("-----END RSA PUBLIC KEY-----", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Trim();

        var bytes = Convert.FromBase64String(clean);
        try
        {
            rsa.ImportSubjectPublicKeyInfo(bytes, out _);
        }
        catch
        {
            rsa.ImportRSAPublicKey(bytes, out _);
        }
    }
}
