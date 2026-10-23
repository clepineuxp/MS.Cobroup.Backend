using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.Backend.Api;
using Xunit;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Security;

public sealed class JwtBearerPipelineHttpTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string Issuer = "https://identity.cobroup.internal";
    private const string Audience = "cobroup-api";
    private const string KeyId = "pipeline-test-hs256-key-1";
    private static readonly byte[] RawKeyBytes = new byte[32]
    {
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
        0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
        0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27,
        0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F
    };
    private static readonly string KeyBase64 = Convert.ToBase64String(RawKeyBytes);

    private readonly SymmetricSecurityKey signingKey;
    private readonly SigningCredentials credentials;
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public JwtBearerPipelineHttpTests(WebApplicationFactory<Program> baseFactory)
    {
        signingKey = new SymmetricSecurityKey(RawKeyBytes) { KeyId = KeyId };
        credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:OperationsDatabase", "Host=localhost;Database=test;Username=test;Password=test");
            builder.UseSetting("Identity:Issuer", Issuer);
            builder.UseSetting("Identity:Audience", Audience);
            builder.UseSetting("Jwt:SigningKeyBase64", KeyBase64);
            builder.UseSetting("Jwt:ActiveKeyId", KeyId);
        });

        client = factory.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
    }

    [Fact]
    public async Task ValidAccessJwt_Returns200Ok_AndExpectedClaims()
    {
        var sub = Guid.NewGuid().ToString();
        var orgId = Guid.NewGuid().ToString();
        var orgCode = "ORG-CORP-1";
        var role = "Admin";

        var token = CreateToken(claimsModifier: claims =>
        {
            claims["sub"] = sub;
            claims["organization_id"] = orgId;
            claims["organization_code"] = orgCode;
            claims["role"] = role;
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations/v1/session/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(sub, root.GetProperty("userId").GetString());
        Assert.Equal(orgId, root.GetProperty("organizationId").GetString());
        Assert.Equal(orgCode, root.GetProperty("organizationCode").GetString());
        Assert.Equal(role, root.GetProperty("role").GetString());
    }

    [Fact]
    public async Task MissingTypClaim_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims.Remove("typ"));
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_UsedAsBearer_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims["typ"] = "refresh");
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingSubClaim_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims.Remove("sub"));
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidSub_NotGuid_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims["sub"] = "not-a-valid-guid");
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingOrganizationIdClaim_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims.Remove("organization_id"));
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidOrganizationId_NotGuid_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims["organization_id"] = "invalid-guid");
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingOrganizationCodeClaim_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims.Remove("organization_code"));
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingRoleClaim_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims.Remove("role"));
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidRoleClaim_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims["role"] = "SuperHacker");
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingJtiClaim_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims.Remove("jti"));
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingIatClaim_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims.Remove("iat"));
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingNbfClaim_Returns401Unauthorized()
    {
        var token = CreateToken(claimsModifier: claims => claims.Remove("nbf"));
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FutureNbf_Returns401Unauthorized()
    {
        var future = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
        var token = CreateToken(claimsModifier: claims => claims["nbf"] = future);
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401Unauthorized()
    {
        var past = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds();
        var token = CreateToken(
            expires: DateTimeOffset.UtcNow.AddMinutes(-10),
            claimsModifier: claims =>
            {
                claims["iat"] = past;
                claims["nbf"] = past;
            });
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongIssuer_Returns401Unauthorized()
    {
        var token = CreateToken(issuer: "https://evil-issuer.com");
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongAudience_Returns401Unauthorized()
    {
        var token = CreateToken(audience: "wrong-audience");
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingKid_Returns401Unauthorized()
    {
        var keyWithoutKid = new SymmetricSecurityKey(RawKeyBytes) { KeyId = null };
        var credsWithoutKid = new SigningCredentials(keyWithoutKid, SecurityAlgorithms.HmacSha256);
        var token = CreateToken(signingCreds: credsWithoutKid);
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownKid_Returns401Unauthorized()
    {
        var keyUnknownKid = new SymmetricSecurityKey(RawKeyBytes) { KeyId = "unknown-kid-xyz" };
        var credsUnknownKid = new SigningCredentials(keyUnknownKid, SecurityAlgorithms.HmacSha256);
        var token = CreateToken(signingCreds: credsUnknownKid);
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UntrustedKeySignature_Returns401Unauthorized()
    {
        var untrustedBytes = new byte[32];
        Array.Fill(untrustedBytes, (byte)0x77);
        var untrustedKey = new SymmetricSecurityKey(untrustedBytes) { KeyId = KeyId };
        var untrustedCreds = new SigningCredentials(untrustedKey, SecurityAlgorithms.HmacSha256);
        var token = CreateToken(signingCreds: untrustedCreds);
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RsaAlgorithm_Returns401Unauthorized()
    {
        using var rsa = RSA.Create(2048);
        var rsaKey = new RsaSecurityKey(rsa) { KeyId = KeyId };
        var rsaCreds = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        var token = CreateToken(signingCreds: rsaCreds);
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendMeAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations/v1/session/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private string CreateToken(
        SigningCredentials? signingCreds = null,
        string? issuer = null,
        string? audience = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null,
        Action<Dictionary<string, object>>? claimsModifier = null)
    {
        var now = DateTimeOffset.UtcNow;
        var nbf = notBefore ?? now.AddMinutes(-1);
        var exp = expires ?? now.AddMinutes(15);

        var claimsDict = new Dictionary<string, object>
        {
            ["typ"] = "access",
            ["sub"] = Guid.NewGuid().ToString(),
            ["organization_id"] = Guid.NewGuid().ToString(),
            ["organization_code"] = "ORG-TEST",
            ["role"] = "Admin",
            ["preferred_username"] = "testuser",
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = nbf.ToUnixTimeSeconds(),
            ["nbf"] = nbf.ToUnixTimeSeconds()
        };

        claimsModifier?.Invoke(claimsDict);

        var claims = claimsDict.Select(kv => new Claim(kv.Key, kv.Value.ToString()!)).ToList();

        var header = new JwtHeader(signingCreds ?? credentials);
        var payload = new JwtPayload(
            issuer ?? Issuer,
            audience ?? Audience,
            claims,
            null,
            exp.UtcDateTime);

        var secToken = new JwtSecurityToken(header, payload);
        return new JwtSecurityTokenHandler().WriteToken(secToken);
    }
}
