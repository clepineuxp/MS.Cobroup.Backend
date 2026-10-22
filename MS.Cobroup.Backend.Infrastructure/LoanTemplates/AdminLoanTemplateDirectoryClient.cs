using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MS.Cobroup.Backend.Core.LoanTemplates;

namespace MS.Cobroup.Backend.Infrastructure.LoanTemplates;

public sealed class AdminLoanTemplateDirectoryClient(HttpClient httpClient) : IAdministrativeLoanTemplateDirectory
{
    public async Task<LoanTemplateDirectoryResult> ListAvailableAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/loan-templates/available");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                return LoanTemplateDirectoryResult.Unauthorized();
            }

            if (response.StatusCode is HttpStatusCode.Forbidden)
            {
                return LoanTemplateDirectoryResult.Forbidden();
            }

            if (!response.IsSuccessStatusCode)
            {
                return LoanTemplateDirectoryResult.Unavailable();
            }

            var templates = await response.Content.ReadFromJsonAsync<AvailableLoanTemplate[]>(cancellationToken)
                ?? [];
            return LoanTemplateDirectoryResult.Success(templates);
        }
        catch (HttpRequestException)
        {
            return LoanTemplateDirectoryResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LoanTemplateDirectoryResult.Unavailable();
        }
    }
}
