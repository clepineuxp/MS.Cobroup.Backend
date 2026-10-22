using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MS.Cobroup.Backend.Api.Health;
using MS.Cobroup.Backend.Api.Security;
using Xunit;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Security;

public sealed class JwksSingletonIdentityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public JwksSingletonIdentityTests(WebApplicationFactory<Program> baseFactory)
    {
        factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:OperationsDatabase", "Host=localhost;Database=test;Username=test;Password=test");
            builder.UseSetting("Identity:Issuer", "https://identity.cobroup.internal");
            builder.UseSetting("Identity:Audience", "cobroup-api");
        });
    }

    [Fact]
    public void JwksValidationService_IsSingleton_AcrossScopes()
    {
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();

        var instance1 = scope1.ServiceProvider.GetRequiredService<IJwksValidationService>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<IJwksValidationService>();
        var rootInstance = factory.Services.GetRequiredService<IJwksValidationService>();

        Assert.Same(instance1, instance2);
        Assert.Same(rootInstance, instance1);
    }

    [Fact]
    public void JwksHealthCheck_And_PreloadHostedService_ShareSameJwksValidationService()
    {
        var jwksService = factory.Services.GetRequiredService<IJwksValidationService>();

        // Health check resolution
        var healthCheckOptions = factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>();
        var jwksRegistration = healthCheckOptions.Value.Registrations.FirstOrDefault(r => r.Name == "jwks");
        Assert.NotNull(jwksRegistration);

        using var scope = factory.Services.CreateScope();
        var healthCheck = jwksRegistration.Factory(scope.ServiceProvider);
        Assert.IsType<JwksHealthCheck>(healthCheck);

        // Hosted services resolution
        var hostedServices = factory.Services.GetServices<IHostedService>();
        var preloadService = hostedServices.OfType<JwksPreloadHostedService>().FirstOrDefault();
        Assert.NotNull(preloadService);

        // Options resolution
        var optionsMonitor = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.NotNull(jwtOptions.TokenValidationParameters);
        Assert.Same(jwksService.TokenValidationParameters, jwtOptions.TokenValidationParameters);
    }
}
