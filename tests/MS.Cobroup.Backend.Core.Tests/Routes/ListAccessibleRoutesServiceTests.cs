using MS.Cobroup.Backend.Core.Routes;

namespace MS.Cobroup.Backend.Core.Tests.Routes;

public sealed class ListAccessibleRoutesServiceTests
{
    [Fact]
    public async Task DelegatesValidatedAccessTokenToAdministrativeDirectory()
    {
        var directory = new MemoryDirectory();
        var service = new ListAccessibleRoutesService(directory);

        var result = await service.ExecuteAsync("access-token", CancellationToken.None);

        Assert.Equal(RouteDirectoryOutcome.Success, result.Outcome);
        Assert.Equal("access-token", directory.AccessToken);
        Assert.Single(result.Routes);
    }

    [Fact]
    public async Task MissingAccessTokenIsRejectedWithoutCallingDirectory()
    {
        var directory = new MemoryDirectory();
        var result = await new ListAccessibleRoutesService(directory).ExecuteAsync("", CancellationToken.None);

        Assert.Equal(RouteDirectoryOutcome.Unauthorized, result.Outcome);
        Assert.Null(directory.AccessToken);
    }

    private sealed class MemoryDirectory : IAdministrativeRouteDirectory
    {
        public string? AccessToken { get; private set; }

        public Task<RouteDirectoryResult> ListAccessibleAsync(string accessToken, CancellationToken cancellationToken)
        {
            AccessToken = accessToken;
            IReadOnlyCollection<AccessibleRoute> routes =
            [new(Guid.NewGuid(), "Centro", null, "Collector", 100, 100)];
            return Task.FromResult(RouteDirectoryResult.Success(routes));
        }
    }
}
