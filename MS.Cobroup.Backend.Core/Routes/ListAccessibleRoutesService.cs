namespace MS.Cobroup.Backend.Core.Routes;

public sealed class ListAccessibleRoutesService(IAdministrativeRouteDirectory directory)
{
    public Task<RouteDirectoryResult> ExecuteAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(RouteDirectoryResult.Unauthorized());
        }

        return directory.ListAccessibleAsync(accessToken, cancellationToken);
    }
}
