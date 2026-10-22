namespace MS.Cobroup.Backend.Api.Security;

public sealed class JwksPreloadHostedService(
    IJwksValidationService jwksValidationService,
    IHostEnvironment environment,
    ILogger<JwksPreloadHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await jwksValidationService.RefreshJwksAsync(force: true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not preload JWKS keys at startup.");
        }

        if (!environment.IsDevelopment() && !jwksValidationService.HasKeys)
        {
            throw new InvalidOperationException("No public signing keys available for JWT validation outside Development.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
