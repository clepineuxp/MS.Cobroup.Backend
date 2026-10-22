using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
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
using MS.Cobroup.Backend.Api.Security;

var builder = WebApplication.CreateBuilder(args);
var operationsConnectionString = builder.Configuration.GetConnectionString("OperationsDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:OperationsDatabase is required.");

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Service is live"), tags: ["live"])
    .AddCheck<OperationsDatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<JwksHealthCheck>("jwks", tags: ["ready"]);

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

var storageSection = builder.Configuration.GetSection(MS.Cobroup.Backend.Infrastructure.Storage.FileSystemStorageOptions.SectionName);
var storageProvider = storageSection["Provider"] ?? "FileSystem";

if (string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
{
    throw new NotSupportedException("El proveedor de almacenamiento 'S3' está previsto para una fase futura de evolución arquitectónica y no está habilitado en la presente versión.");
}

if (!string.Equals(storageProvider, "FileSystem", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException($"Proveedor de almacenamiento desconocido '{storageProvider}'. Solo 'FileSystem' está soportado actualmente.");
}

var evidenceRootPath = storageSection["EvidencePath"] ?? storageSection["RootPath"];
if (string.IsNullOrWhiteSpace(evidenceRootPath))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Storage:EvidencePath is required in non-development environments for durable shared evidence storage.");
    }
    evidenceRootPath = Path.Combine(AppContext.BaseDirectory, "storage", "evidences");
}

builder.Services.Configure<MS.Cobroup.Backend.Infrastructure.Storage.FileSystemStorageOptions>(options =>
{
    options.Provider = storageProvider;
    options.RootPath = evidenceRootPath;
    if (long.TryParse(storageSection["MaxFileSizeBytes"], out var maxBytes) && maxBytes > 0)
    {
        options.MaxFileSizeBytes = maxBytes;
    }
});

builder.Services.AddSingleton<MS.Cobroup.Backend.Domain.Interfaces.Services.IFileStorageService, MS.Cobroup.Backend.Infrastructure.Storage.FileSystemObjectStorage>();

builder.Services.AddDbContext<OperationsDbContext>(options =>
    options.UseNpgsql(operationsConnectionString, postgres =>
    {
        postgres.MigrationsAssembly(typeof(OperationsDbContext).Assembly.FullName);
        postgres.MigrationsHistoryTable("__EFMigrationsHistory", "operations");
    }));

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient<IAdministrativeRouteDirectory, AdminRouteDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["AdministrativeApi:BaseUrl"]
        ?? throw new InvalidOperationException("AdministrativeApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient<IAdministrativeLoanTemplateDirectory, AdminLoanTemplateDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["AdministrativeApi:BaseUrl"]
        ?? throw new InvalidOperationException("AdministrativeApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient<IOperationalPaymentMethodDirectory, AdminPaymentMethodDirectoryClient>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["AdministrativeApi:BaseUrl"]
        ?? throw new InvalidOperationException("AdministrativeApi:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient("JwksClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<IJwksValidationService, JwksValidationService>();
builder.Services.AddHostedService<JwksPreloadHostedService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var validation = AccessJwtValidator.Validate(context.Principal, context.SecurityToken);
                if (!validation.IsValid)
                {
                    context.Fail(validation.FailureReason!);
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IServiceProvider>((options, sp) =>
    {
        var jwksService = sp.GetRequiredService<IJwksValidationService>();
        options.TokenValidationParameters = jwksService.TokenValidationParameters;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("tenant-user", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("organization_id");
    });
    options.AddPolicy("tenant-admin", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("organization_id");
        policy.RequireRole(UserRole.Admin.ToString());
    });
});

var app = builder.Build();

var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
var knownProxiesConfig = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>()
    ?? ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.1"];
foreach (var proxy in knownProxiesConfig)
{
    if (System.Net.IPAddress.TryParse(proxy, out var ip))
    {
        forwardedOptions.KnownProxies.Add(ip);
    }
    else if (System.Net.IPNetwork.TryParse(proxy, out var network))
    {
        forwardedOptions.KnownIPNetworks.Add(network);
    }
}
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
