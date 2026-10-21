using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MS.Cobroup.Backend.Core.Payments;

namespace MS.Cobroup.Backend.Infrastructure.Payments;

public sealed class AdminPaymentMethodDirectoryClient(HttpClient httpClient) : IOperationalPaymentMethodDirectory
{
    public async Task<PaymentMethodDirectoryResult> ListAvailableAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/payment-methods/available");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                return PaymentMethodDirectoryResult.Unauthorized();
            }

            if (response.StatusCode is HttpStatusCode.Forbidden)
            {
                return PaymentMethodDirectoryResult.Forbidden();
            }

            if (!response.IsSuccessStatusCode)
            {
                return PaymentMethodDirectoryResult.Unavailable();
            }

            var items = await response.Content.ReadFromJsonAsync<OperationalPaymentMethodItem[]>(cancellationToken)
                ?? [];
            return PaymentMethodDirectoryResult.Success(items);
        }
        catch (HttpRequestException)
        {
            return PaymentMethodDirectoryResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PaymentMethodDirectoryResult.Unavailable();
        }
    }
}
