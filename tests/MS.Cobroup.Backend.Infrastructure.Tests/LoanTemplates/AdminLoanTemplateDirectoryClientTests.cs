using System.Net;
using System.Text;
using MS.Cobroup.Backend.Core.LoanTemplates;
using MS.Cobroup.Backend.Infrastructure.LoanTemplates;

namespace MS.Cobroup.Backend.Infrastructure.Tests.LoanTemplates;

public sealed class AdminLoanTemplateDirectoryClientTests
{
    [Fact]
    public async Task ForwardsBearerTokenAndMapsSuccessfulContract()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal("api/v1/loan-templates/available", request.RequestUri?.PathAndQuery.TrimStart('/'));
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("token", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "[{\"id\":\"4b9a03d6-4eaa-461e-a52c-9b7f0e652f7b\",\"name\":\"Diario\",\"description\":null,\"classification\":\"Low\",\"status\":\"Published\",\"version\":1,\"maxLoanDays\":30,\"maxPrincipalAmount\":1000000,\"allowedPercentages\":[5,10],\"updatedAtUtc\":\"2026-10-21T12:00:00Z\"}]",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://authority.test/") };

        var result = await new AdminLoanTemplateDirectoryClient(client)
            .ListAvailableAsync("token", CancellationToken.None);

        Assert.Equal(LoanTemplateDirectoryOutcome.Success, result.Outcome);
        var template = Assert.Single(result.Templates);
        Assert.Equal("Diario", template.Name);
        Assert.Equal([5m, 10m], template.AllowedPercentages);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, LoanTemplateDirectoryOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, LoanTemplateDirectoryOutcome.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, LoanTemplateDirectoryOutcome.Unavailable)]
    public async Task MapsUpstreamFailures(HttpStatusCode status, LoanTemplateDirectoryOutcome expected)
    {
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(status)))
        {
            BaseAddress = new Uri("https://authority.test/")
        };

        var result = await new AdminLoanTemplateDirectoryClient(client)
            .ListAvailableAsync("token", CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
