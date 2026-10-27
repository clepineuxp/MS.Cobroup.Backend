using System.Net.Http.Headers;
using System.Net.Http.Json;
using MS.Cobroup.Backend.Domain.Interfaces.Directories;

namespace MS.Cobroup.Backend.Infrastructure.MachineAuth;

public sealed class AdminMachineAuthorizationDirectoryClient(HttpClient httpClient) : IMachineAuthorizationDirectory
{
    public async Task<MachineAuthorizationDirectoryResult> CheckAsync(string accessToken, Guid routeId, string machineId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/machine-auth/check?routeId={routeId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Machine-Id", machineId);
        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return MachineAuthorizationDirectoryResult.Unauthorized();
            if ((int)response.StatusCode == 428) return MachineAuthorizationDirectoryResult.Denied();
            if (!response.IsSuccessStatusCode) return MachineAuthorizationDirectoryResult.Unavailable();
            var payload = await response.Content.ReadFromJsonAsync<CheckPayload>(cancellationToken: ct);
            return payload?.Allowed == true ? MachineAuthorizationDirectoryResult.Allowed(payload.AuthorizedUntilUtc, payload.Indefinite) : MachineAuthorizationDirectoryResult.Denied();
        }
        catch (HttpRequestException) { return MachineAuthorizationDirectoryResult.Unavailable(); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return MachineAuthorizationDirectoryResult.Unavailable(); }
    }
    private sealed record CheckPayload(bool Allowed, DateTimeOffset? AuthorizedUntilUtc, bool Indefinite);
}
