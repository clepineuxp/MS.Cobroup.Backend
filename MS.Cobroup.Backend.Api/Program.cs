using MS.Cobroup.Backend.Core.Services;
using MS.Cobroup.Backend.Domain.Interfaces.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IAccessScopeAuthorizer, AccessScopeAuthorizer>();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
