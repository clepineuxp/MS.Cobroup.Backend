using MS.Cobroup.Backend.Core.LoanTemplates;

namespace MS.Cobroup.Backend.Core.Tests.LoanTemplates;

public sealed class ListAvailableLoanTemplatesServiceTests
{
    [Fact]
    public async Task DelegatesValidatedAccessTokenToAdministrativeDirectory()
    {
        var directory = new MemoryDirectory();
        var result = await new ListAvailableLoanTemplatesService(directory)
            .ExecuteAsync("access-token", CancellationToken.None);

        Assert.Equal(LoanTemplateDirectoryOutcome.Success, result.Outcome);
        Assert.Equal("access-token", directory.AccessToken);
        Assert.Single(result.Templates);
    }

    [Fact]
    public async Task MissingAccessTokenIsRejectedWithoutCallingDirectory()
    {
        var directory = new MemoryDirectory();
        var result = await new ListAvailableLoanTemplatesService(directory)
            .ExecuteAsync("", CancellationToken.None);

        Assert.Equal(LoanTemplateDirectoryOutcome.Unauthorized, result.Outcome);
        Assert.Null(directory.AccessToken);
    }

    private sealed class MemoryDirectory : IAdministrativeLoanTemplateDirectory
    {
        public string? AccessToken { get; private set; }

        public Task<LoanTemplateDirectoryResult> ListAvailableAsync(string accessToken, CancellationToken cancellationToken)
        {
            AccessToken = accessToken;
            IReadOnlyCollection<AvailableLoanTemplate> templates =
            [new(Guid.NewGuid(), "Diario", null, "Low", "Published", 1, 30, 1_000_000m, [5m], DateTimeOffset.UtcNow)];
            return Task.FromResult(LoanTemplateDirectoryResult.Success(templates));
        }
    }
}
