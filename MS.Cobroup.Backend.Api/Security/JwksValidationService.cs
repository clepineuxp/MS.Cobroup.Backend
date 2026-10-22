using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.Backend.Api.Security;

public interface IJwksValidationService
{
    TokenValidationParameters TokenValidationParameters { get; }
    Task<bool> RefreshJwksAsync(bool force = false, CancellationToken cancellationToken = default);
    bool HasKeys { get; }
}

public sealed class JwksValidationService : IJwksValidationService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<JwksValidationService> logger;
    private readonly string? jwksUri;
    private readonly string issuer;
    private readonly string audience;
    private readonly List<SecurityKey> staticPublicKeys = [];
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private JsonWebKeySet? cachedJwks;
    private DateTimeOffset nextRefreshAllowedAtUtc = DateTimeOffset.MinValue;
    private int isBackgroundRefreshing = 0;

    public TokenValidationParameters TokenValidationParameters { get; }

    public bool HasKeys => staticPublicKeys.Count > 0 || (cachedJwks is not null && cachedJwks.Keys.Count > 0);

    public JwksValidationService(
        IConfiguration configuration,
        IHostEnvironment environment,
        IHttpClientFactory httpClientFactory,
        ILogger<JwksValidationService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;

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

        var previousPublicKeyPem = configuration["Jwt:PreviousPublicKeyPem"];
        var previousKid = configuration["Jwt:PreviousKeyId"] ?? configuration["Jwt:PreviousKid"];
        if (!string.IsNullOrWhiteSpace(previousPublicKeyPem) && !string.IsNullOrWhiteSpace(previousKid))
        {
            var rsaPrev = RSA.Create();
            ImportPublicKey(rsaPrev, previousPublicKeyPem);
            var keyPrev = new RsaSecurityKey(rsaPrev) { KeyId = previousKid };
            staticPublicKeys.Add(keyPrev);
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
            RoleClaimType = "role",
            NameClaimType = "preferred_username",
            IssuerSigningKeyResolver = ResolveSigningKey
        };
    }

    public async Task<bool> RefreshJwksAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jwksUri))
        {
            return false;
        }

        if (!force && DateTimeOffset.UtcNow < nextRefreshAllowedAtUtc)
        {
            return false;
        }

        var acquired = await refreshLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        if (!acquired)
        {
            return false;
        }

        try
        {
            if (!force && DateTimeOffset.UtcNow < nextRefreshAllowedAtUtc)
            {
                return false;
            }

            var client = httpClientFactory.CreateClient("JwksClient");
            var response = await client.GetStringAsync(jwksUri, cancellationToken);
            cachedJwks = new JsonWebKeySet(response);
            nextRefreshAllowedAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);
            logger.LogInformation("Successfully refreshed JWKS from {JwksUri}. Available keys: {Count}", jwksUri, cachedJwks.Keys.Count);
            return true;
        }
        catch (Exception ex)
        {
            nextRefreshAllowedAtUtc = DateTimeOffset.UtcNow.AddSeconds(30);
            logger.LogWarning(ex, "Failed to refresh JWKS from {JwksUri}. Backoff for 30s.", jwksUri);
            return false;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public IEnumerable<SecurityKey> ResolveSigningKey(
        string token,
        SecurityToken securityToken,
        string kid,
        TokenValidationParameters validationParameters)
    {
        if (string.IsNullOrWhiteSpace(kid))
        {
            return [];
        }

        if (staticPublicKeys.Count > 0)
        {
            var match = staticPublicKeys.FirstOrDefault(k => k.KeyId == kid);
            if (match is not null) return [match];
        }

        // Try cached JWKS
        if (cachedJwks is not null)
        {
            var matching = cachedJwks.Keys.FirstOrDefault(k => k.Kid == kid);
            if (matching is not null)
            {
                return [matching];
            }
        }

        // Kid is unknown. Trigger a background single-flight asynchronous refresh if cooldown allows
        if (DateTimeOffset.UtcNow >= nextRefreshAllowedAtUtc && !string.IsNullOrWhiteSpace(jwksUri))
        {
            if (Interlocked.CompareExchange(ref isBackgroundRefreshing, 1, 0) == 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RefreshJwksAsync(force: false, CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore background refresh failure
                    }
                    finally
                    {
                        Interlocked.Exchange(ref isBackgroundRefreshing, 0);
                    }
                });
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
