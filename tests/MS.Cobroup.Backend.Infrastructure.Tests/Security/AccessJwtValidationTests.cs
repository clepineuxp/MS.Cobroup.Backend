using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.Backend.Api.Security;
using Xunit;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Security;

public sealed class AccessJwtValidationTests
{
    private const string ExpectedIssuer = "https://identity.cobroup.internal";
    private const string ExpectedAudience = "cobroup-api";
    private const string KeyId = "backend-test-key-1";

    private static readonly byte[] KeyBytes = RandomNumberGenerator.GetBytes(32);
    private static readonly string KeyBase64 = Convert.ToBase64String(KeyBytes);

    private readonly SymmetricSecurityKey signingKey;
    private readonly SigningCredentials signingCredentials;
    private readonly SymmetricTokenValidationService validationService;
    private readonly JwtSecurityTokenHandler tokenHandler;

    public AccessJwtValidationTests()
    {
        signingKey = new SymmetricSecurityKey(KeyBytes) { KeyId = KeyId };
        signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var inMemory = new Dictionary<string, string?>
        {
            ["Identity:Issuer"] = ExpectedIssuer,
            ["Identity:Audience"] = ExpectedAudience,
            ["Jwt:SigningKeyBase64"] = KeyBase64,
            ["Jwt:ActiveKeyId"] = KeyId
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        validationService = new SymmetricTokenValidationService(config, env);
        tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
    }

    [Fact]
    public void Case1_WrongSignature_FailsValidation()
    {
        var attackerKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)) { KeyId = KeyId };
        var attackerCredentials = new SigningCredentials(attackerKey, SecurityAlgorithms.HmacSha256);

        var token = CreateToken(attackerCredentials);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out _));
    }

    [Fact]
    public void Case2_WrongAlgorithmOrNone_FailsValidation()
    {
        // Token firmado con RSA debe ser rechazado por la allowlist estricta de HS256
        using var rsa = RSA.Create(2048);
        var rsaCreds = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256);
        var tokenWithRsa = CreateToken(rsaCreds);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            tokenHandler.ValidateToken(tokenWithRsa, validationService.TokenValidationParameters, out _));

        // Token firmado con HMAC-SHA384 debe ser rechazado
        var hs384Key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(48)) { KeyId = KeyId };
        var hs384Creds = new SigningCredentials(hs384Key, SecurityAlgorithms.HmacSha384);
        var tokenWithHs384 = CreateToken(hs384Creds);
        Assert.ThrowsAny<SecurityTokenException>(() =>
            tokenHandler.ValidateToken(tokenWithHs384, validationService.TokenValidationParameters, out _));
    }

    [Fact]
    public void Case3_MissingKid_FailsValidation()
    {
        var credsNoKid = new SigningCredentials(new SymmetricSecurityKey(KeyBytes) { KeyId = null }, SecurityAlgorithms.HmacSha256);
        var token = CreateToken(credsNoKid);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out _));
    }

    [Fact]
    public void Case4_UnknownKid_FailsValidation()
    {
        var credsUnknownKid = new SigningCredentials(new SymmetricSecurityKey(KeyBytes) { KeyId = "unknown-kid-999" }, SecurityAlgorithms.HmacSha256);
        var token = CreateToken(credsUnknownKid);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out _));
    }

    [Fact]
    public void Case5_WrongIssuer_FailsValidation()
    {
        var token = CreateToken(signingCredentials, issuer: "https://evil.issuer.com");

        Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
            tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out _));
    }

    [Fact]
    public void Case6_WrongAudience_FailsValidation()
    {
        var token = CreateToken(signingCredentials, audience: "wrong-audience");

        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out _));
    }

    [Fact]
    public void Case7_ExpiredToken_FailsValidation()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-30);
        var token = CreateToken(signingCredentials, notBefore: now.AddMinutes(-5), expires: now);

        Assert.Throws<SecurityTokenExpiredException>(() =>
            tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out _));
    }

    [Fact]
    public void Case8_FutureNbf_FailsValidation()
    {
        var future = DateTimeOffset.UtcNow.AddMinutes(10);
        var token = CreateToken(signingCredentials, notBefore: future, expires: future.AddMinutes(15));

        Assert.Throws<SecurityTokenNotYetValidException>(() =>
            tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("refresh")]
    [InlineData("id_token")]
    public void Case9_TypMissingOrNotAccess_FailsValidation(string? typ)
    {
        var claims = CreateValidClaims();
        claims.RemoveAll(c => c.Type == "typ");
        if (typ is not null)
        {
            claims.Add(new Claim("typ", typ));
        }

        var token = CreateTokenWithClaims(signingCredentials, claims);
        var principal = tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out var validatedToken);

        var result = AccessJwtValidator.Validate(principal, validatedToken);
        Assert.False(result.IsValid);
        Assert.Contains("typ", result.FailureReason);
    }

    [Fact]
    public void Case10_MandatoryClaimsMissingOrMalformed_FailsValidation()
    {
        // 10a. Malformed sub (not a GUID)
        var claims = CreateValidClaims();
        claims.RemoveAll(c => c.Type == "sub");
        claims.Add(new Claim("sub", "not-a-guid"));
        var token = CreateTokenWithClaims(signingCredentials, claims);
        var principal = tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out var validated);
        var res = AccessJwtValidator.Validate(principal, validated);
        Assert.False(res.IsValid);
        Assert.Contains("sub", res.FailureReason);

        // 10b. Missing organization_id
        claims = CreateValidClaims();
        claims.RemoveAll(c => c.Type == "organization_id");
        token = CreateTokenWithClaims(signingCredentials, claims);
        principal = tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out validated);
        res = AccessJwtValidator.Validate(principal, validated);
        Assert.False(res.IsValid);
        Assert.Contains("organization_id", res.FailureReason);

        // 10c. Empty organization_code
        claims = CreateValidClaims();
        claims.RemoveAll(c => c.Type == "organization_code");
        claims.Add(new Claim("organization_code", ""));
        token = CreateTokenWithClaims(signingCredentials, claims);
        principal = tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out validated);
        res = AccessJwtValidator.Validate(principal, validated);
        Assert.False(res.IsValid);
        Assert.Contains("organization_code", res.FailureReason);

        // 10d. Missing jti
        claims = CreateValidClaims();
        claims.RemoveAll(c => c.Type == "jti");
        token = CreateTokenWithClaims(signingCredentials, claims);
        principal = tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out validated);
        res = AccessJwtValidator.Validate(principal, validated);
        Assert.False(res.IsValid);
        Assert.Contains("jti", res.FailureReason);

        // 10e. Invalid role
        claims = CreateValidClaims();
        claims.RemoveAll(c => c.Type == "role");
        claims.Add(new Claim("role", "SuperHacker"));
        token = CreateTokenWithClaims(signingCredentials, claims);
        principal = tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out validated);
        res = AccessJwtValidator.Validate(principal, validated);
        Assert.False(res.IsValid);
        Assert.Contains("role", res.FailureReason);
    }

    [Fact]
    public void Case11_RefreshJwtPresentedAsBearer_FailsValidation()
    {
        var refreshClaims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("family_id", Guid.NewGuid().ToString()),
            new("organization_id", Guid.NewGuid().ToString()),
            new("typ", "refresh"),
            new("jti", Guid.NewGuid().ToString("N"))
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(refreshClaims),
            Issuer = ExpectedIssuer,
            Audience = "cobroup-refresh",
            Expires = DateTime.UtcNow.AddDays(30),
            SigningCredentials = signingCredentials
        };

        var rawToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

        // Audience mismatch alone will reject it
        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            tokenHandler.ValidateToken(rawToken, validationService.TokenValidationParameters, out _));

        // And even if audience were coerced, typ=refresh is rejected by AccessJwtValidator
        var paramsWithCoercedAud = validationService.TokenValidationParameters.Clone();
        paramsWithCoercedAud.ValidAudience = "cobroup-refresh";
        var principal = tokenHandler.ValidateToken(rawToken, paramsWithCoercedAud, out var validated);
        var result = AccessJwtValidator.Validate(principal, validated);
        Assert.False(result.IsValid);
        Assert.Contains("access", result.FailureReason);
    }

    [Fact]
    public void Case12_ValidToken_SuccessfullyValidated()
    {
        var token = CreateToken(signingCredentials);

        var principal = tokenHandler.ValidateToken(token, validationService.TokenValidationParameters, out var validatedToken);
        var result = AccessJwtValidator.Validate(principal, validatedToken);

        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
        Assert.NotNull(principal.FindFirst("organization_id")?.Value);
        Assert.NotNull(principal.FindFirst("role")?.Value);
    }

    private string CreateToken(
        SigningCredentials credentials,
        string issuer = ExpectedIssuer,
        string audience = ExpectedAudience,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null)
    {
        var claims = CreateValidClaims();
        return CreateTokenWithClaims(credentials, claims, issuer, audience, notBefore, expires);
    }

    private string CreateTokenWithClaims(
        SigningCredentials credentials,
        List<Claim> claims,
        string issuer = ExpectedIssuer,
        string audience = ExpectedAudience,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null)
    {
        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer,
            Audience = audience,
            NotBefore = (notBefore ?? now).UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Expires = (expires ?? now.AddMinutes(15)).UtcDateTime,
            SigningCredentials = credentials
        };

        var token = tokenHandler.CreateToken(descriptor);
        return tokenHandler.WriteToken(token);
    }

    private static List<Claim> CreateValidClaims()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("jti", Guid.NewGuid().ToString("N")),
            new Claim("iat", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("nbf", now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("typ", "access"),
            new Claim("organization_id", Guid.NewGuid().ToString()),
            new Claim("organization_code", "DEMO-ADMIN"),
            new Claim("role", "Admin"),
            new Claim("preferred_username", "admin_user"),
            new Claim("user_id", Guid.NewGuid().ToString())
        ];
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "BackendTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
