using System.Net;
using System.Text;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Infrastructure.Routes;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Routes;

public sealed class AdminRouteDirectoryClientTests
{
    [Fact]
    public async Task ForwardsBearerTokenAndMapsSuccessfulContract()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("token", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "[{\"id\":\"4b9a03d6-4eaa-461e-a52c-9b7f0e652f7b\",\"name\":\"Centro\",\"description\":null,\"accessAs\":\"Collector\",\"maxActiveCustomers\":25,\"maxActiveLoans\":30}]",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://authority.test/") };

        var result = await new AdminRouteDirectoryClient(client).ListAccessibleAsync("token", CancellationToken.None);

        Assert.Equal(RouteDirectoryOutcome.Success, result.Outcome);
        Assert.Equal("Centro", Assert.Single(result.Routes).Name);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, RouteDirectoryOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, RouteDirectoryOutcome.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, RouteDirectoryOutcome.Unavailable)]
    public async Task MapsUpstreamFailures(HttpStatusCode status, RouteDirectoryOutcome expected)
    {
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(status)))
        {
            BaseAddress = new Uri("https://authority.test/")
        };

        var result = await new AdminRouteDirectoryClient(client).ListAccessibleAsync("token", CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
