namespace MS.Cobroup.Backend.Core.LoanTemplates;

public sealed class ListAvailableLoanTemplatesService(IAdministrativeLoanTemplateDirectory directory)
{
    public Task<LoanTemplateDirectoryResult> ExecuteAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(LoanTemplateDirectoryResult.Unauthorized());
        }

        return directory.ListAvailableAsync(accessToken, cancellationToken);
    }
}
