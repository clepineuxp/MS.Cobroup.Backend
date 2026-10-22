using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Core.LoanTemplates;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/loan-templates")]
[Authorize(Policy = "tenant-user")]
public sealed class LoanTemplatesController(ListAvailableLoanTemplatesService templates) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.ToString();
        var accessToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..].Trim()
            : string.Empty;
        var result = await templates.ExecuteAsync(accessToken, cancellationToken);
        return result.Outcome switch
        {
            LoanTemplateDirectoryOutcome.Success => Ok(result.Templates),
            LoanTemplateDirectoryOutcome.Unauthorized => Unauthorized(),
            LoanTemplateDirectoryOutcome.Forbidden => Forbid(),
            LoanTemplateDirectoryOutcome.Unavailable => Problem(
                "The administrative loan template directory is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem("Unexpected loan template directory result.")
        };
    }
}
