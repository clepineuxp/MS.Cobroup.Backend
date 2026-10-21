using MS.Cobroup.Backend.Core.Services;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Interfaces.Services;
using MS.Cobroup.Backend.Infrastructure.Routes;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddSingleton<IAccessScopeAuthorizer, AccessScopeAuthorizer>();
builder.Services.AddScoped<ListAccessibleRoutesService>();
builder.Services.AddHttpClient<IAdministrativeRouteDirectory, AdminRouteDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["AdministrativeApi:BaseUrl"]
        ?? throw new InvalidOperationException("AdministrativeApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("tenant-user", policy =>
    {
        policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(OpenIddictConstants.Claims.Subject);
        policy.RequireClaim("organization_id");
    });
});
builder.Services.AddOpenIddict().AddValidation(options =>
{
    var issuer = builder.Configuration["Identity:Issuer"]
        ?? throw new InvalidOperationException("Identity:Issuer is required.");
    options.SetIssuer(new Uri(issuer, UriKind.Absolute));
    options.AddAudiences("cobroup-api");
    options.UseSystemNetHttp();
    options.UseAspNetCore();
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
