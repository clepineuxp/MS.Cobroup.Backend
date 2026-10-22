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
    private const string KeyId = "pipeline-test-rsa-key-1";

    private readonly RSA rsa;
    private readonly RsaSecurityKey signingKey;
    private readonly SigningCredentials credentials;
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public JwtBearerPipelineHttpTests(WebApplicationFactory<Program> baseFactory)
    {
        rsa = RSA.Create(2048);
        signingKey = new RsaSecurityKey(rsa) { KeyId = KeyId };
        credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:OperationsDatabase", "Host=localhost;Database=test;Username=test;Password=test");
            builder.UseSetting("Identity:Issuer", Issuer);
            builder.UseSetting("Identity:Audience", Audience);
            builder.UseSetting("Identity:JwksUri", "");
            builder.UseSetting("Identity:JwksUrl", "");
            builder.UseSetting("Jwt:PublicKeyPem", publicKeyPem);
            builder.UseSetting("Jwt:KeyId", KeyId);
        });

        client = factory.CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        rsa.Dispose();
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
        var keyWithoutKid = new RsaSecurityKey(rsa) { KeyId = null };
        var credsWithoutKid = new SigningCredentials(keyWithoutKid, SecurityAlgorithms.RsaSha256);
        var token = CreateToken(signingCreds: credsWithoutKid);
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownKid_Returns401Unauthorized()
    {
        var keyUnknownKid = new RsaSecurityKey(rsa) { KeyId = "unknown-kid-xyz" };
        var credsUnknownKid = new SigningCredentials(keyUnknownKid, SecurityAlgorithms.RsaSha256);
        var token = CreateToken(signingCreds: credsUnknownKid);
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UntrustedKeySignature_Returns401Unauthorized()
    {
        using var untrustedRsa = RSA.Create(2048);
        var untrustedKey = new RsaSecurityKey(untrustedRsa) { KeyId = KeyId };
        var untrustedCreds = new SigningCredentials(untrustedKey, SecurityAlgorithms.RsaSha256);
        var token = CreateToken(signingCreds: untrustedCreds);
        var response = await SendMeAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HmacAlgorithm_Returns401Unauthorized()
    {
        var hmacKey = new SymmetricSecurityKey(new byte[32]) { KeyId = KeyId };
        var hmacCreds = new SigningCredentials(hmacKey, SecurityAlgorithms.HmacSha256);
        var token = CreateToken(signingCreds: hmacCreds);
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
