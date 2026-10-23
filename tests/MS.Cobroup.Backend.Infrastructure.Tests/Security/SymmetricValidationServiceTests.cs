using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MS.Cobroup.Backend.Api;
using MS.Cobroup.Backend.Api.Security;
using Xunit;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Security;

public sealed class SymmetricValidationServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly byte[] ValidKey32Bytes = RandomNumberGenerator.GetBytes(32);
    private static readonly string ValidKey32Base64 = Convert.ToBase64String(ValidKey32Bytes);

    private static readonly byte[] ValidKey64Bytes = RandomNumberGenerator.GetBytes(64);
    private static readonly string ValidKey64Base64 = Convert.ToBase64String(ValidKey64Bytes);

    private static readonly byte[] InvalidKey31Bytes = RandomNumberGenerator.GetBytes(31);
    private static readonly string InvalidKey31Base64 = Convert.ToBase64String(InvalidKey31Bytes);

    private readonly WebApplicationFactory<Program> factory;

    public SymmetricValidationServiceTests(WebApplicationFactory<Program> baseFactory)
    {
        factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:OperationsDatabase", "Host=localhost;Database=test;Username=test;Password=test");
            builder.UseSetting("Identity:Issuer", "https://dev-auth.cobroup.com");
            builder.UseSetting("Identity:Audience", "cobroup-api");
            builder.UseSetting("Jwt:SigningKeyBase64", ValidKey32Base64);
            builder.UseSetting("Jwt:ActiveKeyId", "k1");
        });
    }

    private static IConfiguration CreateConfig(
        string? signingKey = null,
        string? activeKid = "k1",
        string? issuer = "https://dev-auth.cobroup.com",
        string? audience = "cobroup-api",
        Dictionary<string, string>? previousKeys = null)
    {
        var dict = new Dictionary<string, string?>();

        if (signingKey is not null)
        {
            dict["Jwt:SigningKeyBase64"] = signingKey;
        }

        if (activeKid is not null)
        {
            dict["Jwt:ActiveKeyId"] = activeKid;
        }

        if (issuer is not null)
        {
            dict["Identity:Issuer"] = issuer;
        }

        if (audience is not null)
        {
            dict["Identity:Audience"] = audience;
        }

        if (previousKeys is not null)
        {
            var idx = 0;
            foreach (var kvp in previousKeys)
            {
                dict[$"Jwt:PreviousValidationKeys:{idx}:KeyId"] = kvp.Key;
                dict[$"Jwt:PreviousValidationKeys:{idx}:SigningKeyBase64"] = kvp.Value;
                idx++;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void Test1_Backend_Starts_WhenSymmetricKeyValid()
    {
        var config = CreateConfig(ValidKey32Base64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        using var service = new SymmetricTokenValidationService(config, env);

        Assert.Equal("k1", service.ActiveKeyId);
        Assert.NotNull(service.TokenValidationParameters);
        Assert.Contains(SecurityAlgorithms.HmacSha256, service.TokenValidationParameters.ValidAlgorithms);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Test2_FailFast_WhenKeyMissingOrEmpty(string? emptyKey)
    {
        var config = CreateConfig(emptyKey, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricTokenValidationService(config, env));
        Assert.Contains("obligatoria", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CHANGE_ME")]
    [InlineData("CHANGE_ME_DEV_KEY")]
    [InlineData("${{Dev_Jwt__SigningKeyBase64}}")]
    [InlineData("PLACEHOLDER_KEY_HERE")]
    public void Test3_FailFast_WhenKeyContainsPlaceholder(string placeholder)
    {
        var config = CreateConfig(placeholder, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricTokenValidationService(config, env));
        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-a-valid-base64-string!")]
    [InlineData("====invalid====")]
    [InlineData("12345")]
    public void Test4_FailFast_WhenKeyInvalidBase64(string invalidBase64)
    {
        var config = CreateConfig(invalidBase64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricTokenValidationService(config, env));
        Assert.Contains("Base64 válida", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test5_FailFast_WhenKeyUnder32Bytes_31BytesRejected()
    {
        var config = CreateConfig(InvalidKey31Base64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var ex = Assert.Throws<InvalidOperationException>(() => new SymmetricTokenValidationService(config, env));
        Assert.Contains("longitud mínima de 32 bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("31", ex.Message);
    }

    [Fact]
    public void Test6_Success_WhenKeyExactly32BytesAnd64Bytes()
    {
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };

        using var service32 = new SymmetricTokenValidationService(CreateConfig(ValidKey32Base64), env);
        Assert.NotNull(service32.TokenValidationParameters);

        using var service64 = new SymmetricTokenValidationService(CreateConfig(ValidKey64Base64), env);
        Assert.NotNull(service64.TokenValidationParameters);
    }

    [Fact]
    public void Test7_ValidationService_IsSingleton_AcrossScopes()
    {
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();

        var instance1 = scope1.ServiceProvider.GetRequiredService<ISymmetricTokenValidationService>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<ISymmetricTokenValidationService>();
        var rootInstance = factory.Services.GetRequiredService<ISymmetricTokenValidationService>();

        Assert.Same(instance1, instance2);
        Assert.Same(instance1, rootInstance);
    }

    [Fact]
    public void Test8_PreviousKeys_ValidateTokensDuringOverlapWindow()
    {
        var prevKeyBytes = RandomNumberGenerator.GetBytes(32);
        var prevKeyBase64 = Convert.ToBase64String(prevKeyBytes);

        var previousDict = new Dictionary<string, string>
        {
            ["k0"] = prevKeyBase64
        };

        var config = CreateConfig(ValidKey32Base64, "k1", previousKeys: previousDict);
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        using var service = new SymmetricTokenValidationService(config, env);

        // Emitir token firmado con la clave anterior k0
        var prevKey = new SymmetricSecurityKey(prevKeyBytes) { KeyId = "k0" };
        var prevCreds = new SigningCredentials(prevKey, SecurityAlgorithms.HmacSha256);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = prevCreds
        };
        var tokenFromOldKey = handler.WriteToken(handler.CreateToken(descriptor));

        var principal = handler.ValidateToken(tokenFromOldKey, service.TokenValidationParameters, out var validatedToken);
        Assert.NotNull(principal);
        Assert.Equal("k0", ((JwtSecurityToken)validatedToken).Header.Kid);
    }

    [Fact]
    public void Test9_RetiredKey_FailsValidation()
    {
        var config = CreateConfig(ValidKey32Base64, "k1", previousKeys: null);
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        using var service = new SymmetricTokenValidationService(config, env);

        var retiredKeyBytes = RandomNumberGenerator.GetBytes(32);
        var retiredKey = new SymmetricSecurityKey(retiredKeyBytes) { KeyId = "k0" };
        var retiredCreds = new SigningCredentials(retiredKey, SecurityAlgorithms.HmacSha256);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = retiredCreds
        };
        var tokenFromRetiredKey = handler.WriteToken(handler.CreateToken(descriptor));

        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(tokenFromRetiredKey, service.TokenValidationParameters, out _));
    }

    [Fact]
    public void Test10_UnknownKid_FailsValidation()
    {
        var config = CreateConfig(ValidKey32Base64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        using var service = new SymmetricTokenValidationService(config, env);

        var keyWithUnknownKid = new SymmetricSecurityKey(ValidKey32Bytes) { KeyId = "unknown-kid" };
        var creds = new SigningCredentials(keyWithUnknownKid, SecurityAlgorithms.HmacSha256);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "https://dev-auth.cobroup.com",
            Audience = "cobroup-api",
            SigningCredentials = creds
        };
        var token = handler.WriteToken(handler.CreateToken(descriptor));

        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(token, service.TokenValidationParameters, out _));
    }

    [Fact]
    public void Test11_AlgorithmAllowlist_RejectsNoneRs256Es256Hs384Hs512()
    {
        var config = CreateConfig(ValidKey32Base64, "k1");
        var env = new TestHostEnvironment { EnvironmentName = Environments.Development };
        using var service = new SymmetricTokenValidationService(config, env);

        Assert.Single(service.TokenValidationParameters.ValidAlgorithms);
        Assert.Contains(SecurityAlgorithms.HmacSha256, service.TokenValidationParameters.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.None, service.TokenValidationParameters.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.RsaSha256, service.TokenValidationParameters.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.EcdsaSha256, service.TokenValidationParameters.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.HmacSha384, service.TokenValidationParameters.ValidAlgorithms);
        Assert.DoesNotContain(SecurityAlgorithms.HmacSha512, service.TokenValidationParameters.ValidAlgorithms);
    }

    [Fact]
    public void Test12_BackendBoundary_BackendDoesNotContainEmissionServicesOrEndpoints()
    {
        var apiAssembly = typeof(Program).Assembly;
        var types = apiAssembly.GetTypes();

        // 1. Backend no debe definir interfaces ni clases de emisión de tokens
        var tokenIssuingTypes = types.Where(t =>
            t.Name.Contains("TokenService") ||
            t.Name.Contains("TokenIssuer") ||
            t.Name.Contains("IssueTokens")).ToList();
        Assert.Empty(tokenIssuingTypes);

        // 2. ISymmetricTokenValidationService no debe exponer métodos de obtención de credenciales de firma
        var valInterface = typeof(ISymmetricTokenValidationService);
        Assert.Null(valInterface.GetMethod("GetSigningCredentials"));
        Assert.Null(valInterface.GetMethod("CreateToken"));
        Assert.Null(valInterface.GetMethod("IssueTokens"));

        // 3. Ningún controlador de Backend debe exponer rutas de login, refresh o emisión
        var controllers = types.Where(t => t.Name.EndsWith("Controller"));
        foreach (var controller in controllers)
        {
            var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var name = method.Name.ToLowerInvariant();
                Assert.DoesNotContain("login", name);
                Assert.DoesNotContain("refresh", name);
                Assert.DoesNotContain("issue", name);
                Assert.DoesNotContain("jwks", name);
            }
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "BackendTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
