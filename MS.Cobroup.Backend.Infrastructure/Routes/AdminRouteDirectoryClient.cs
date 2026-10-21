using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MS.Cobroup.Backend.Core.Routes;

namespace MS.Cobroup.Backend.Infrastructure.Routes;

public sealed class AdminRouteDirectoryClient(HttpClient httpClient) : IAdministrativeRouteDirectory
{
    public async Task<RouteDirectoryResult> ListAccessibleAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/route-access");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                return RouteDirectoryResult.Unauthorized();
            }

            if (response.StatusCode is HttpStatusCode.Forbidden)
            {
                return RouteDirectoryResult.Forbidden();
            }

            if (!response.IsSuccessStatusCode)
            {
                return RouteDirectoryResult.Unavailable();
            }

            var routes = await response.Content.ReadFromJsonAsync<AccessibleRoute[]>(cancellationToken)
                ?? [];
            return RouteDirectoryResult.Success(routes);
        }
        catch (HttpRequestException)
        {
            return RouteDirectoryResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RouteDirectoryResult.Unavailable();
        }
    }
}
