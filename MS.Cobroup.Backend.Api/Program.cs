using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MS.Cobroup.Backend.Api.Health;
using MS.Cobroup.Backend.Core.Agenda;
using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.LoanExceptions;
using MS.Cobroup.Backend.Core.Loans;
using MS.Cobroup.Backend.Core.LoanTemplates;
using MS.Cobroup.Backend.Core.Payments;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Core.Services;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.Interfaces.Services;
using MS.Cobroup.Backend.Infrastructure.LoanTemplates;
using MS.Cobroup.Backend.Infrastructure.Payments;
using MS.Cobroup.Backend.Infrastructure.Persistence;
using MS.Cobroup.Backend.Infrastructure.Routes;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var operationsConnectionString = builder.Configuration.GetConnectionString("OperationsDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:OperationsDatabase is required.");

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Service is live"), tags: ["live"])
    .AddCheck<OperationsDatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<IAccessScopeAuthorizer, AccessScopeAuthorizer>();
builder.Services.AddScoped<ListAccessibleRoutesService>();
builder.Services.AddScoped<ListAvailableLoanTemplatesService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<LoanExceptionService>();
builder.Services.AddScoped<LoanService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<AgendaService>();
builder.Services.AddScoped<ICustomerRepository, EfCustomerRepository>();
builder.Services.AddScoped<ILoanExceptionRepository, EfLoanExceptionRepository>();
builder.Services.AddScoped<ILoanRepository, EfLoanRepository>();
builder.Services.AddScoped<IPaymentRepository, EfPaymentRepository>();
builder.Services.AddScoped<IAgendaRepository, EfAgendaRepository>();

builder.Services.AddSingleton<MS.Cobroup.Backend.Domain.Interfaces.Services.IFileStorageService, MS.Cobroup.Backend.Infrastructure.Storage.LocalFileStorageService>();

builder.Services.AddDbContext<OperationsDbContext>(options =>
    options.UseNpgsql(operationsConnectionString, postgres =>
    {
        postgres.MigrationsAssembly(typeof(OperationsDbContext).Assembly.FullName);
        postgres.MigrationsHistoryTable("__EFMigrationsHistory", "operations");
    }));

builder.Services.AddHttpClient<IAdministrativeRouteDirectory, AdminRouteDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["AdministrativeApi:BaseUrl"]
        ?? throw new InvalidOperationException("AdministrativeApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IAdministrativeLoanTemplateDirectory, AdminLoanTemplateDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["AdministrativeApi:BaseUrl"]
        ?? throw new InvalidOperationException("AdministrativeApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IOperationalPaymentMethodDirectory, AdminPaymentMethodDirectoryClient>((services, client) =>
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
    options.AddPolicy("tenant-admin", policy =>
    {
        policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(OpenIddictConstants.Claims.Subject);
        policy.RequireClaim("organization_id");
        policy.RequireRole(UserRole.Admin.ToString());
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

var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

app.Use(async (context, next) =>
{
    var rawCorrelationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var correlationId = !string.IsNullOrWhiteSpace(rawCorrelationId) && rawCorrelationId.Length <= 64
        ? rawCorrelationId.Trim()
        : Guid.NewGuid().ToString("D");

    context.TraceIdentifier = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"] = "0";

    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Correlation");
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        await next();
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.MapControllers();

app.Run();

public partial class Program;
