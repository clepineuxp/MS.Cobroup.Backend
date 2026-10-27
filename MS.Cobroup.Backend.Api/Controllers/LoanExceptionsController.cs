using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Api.Security;
using MS.Cobroup.Backend.Core.LoanExceptions;
using MS.Cobroup.Backend.Domain.LoanExceptions;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/loan-exceptions")]
[Authorize(Policy = "tenant-user")]
[MachineAuthorized]
public sealed class LoanExceptionsController(LoanExceptionService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateLoanExceptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.CreateAsync(
            new CreateLoanExceptionCommand(
                actor,
                CustomerActorReader.ReadBearerToken(Request),
                new CreateLoanExceptionInput(
                    request.CustomerId,
                    request.ProposedAmount,
                    request.ProposedDays,
                    request.ProposedPercentage,
                    request.Reason)),
            cancellationToken);

        return result.Outcome is LoanExceptionOutcome.Created
            ? Created($"/api/operations/v1/loan-exceptions/{result.Exception!.Id}", result.Exception)
            : MapResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? routeId,
        [FromQuery] Guid? customerId,
        [FromQuery] LoanExceptionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.SearchAsync(
            new SearchLoanExceptionsQuery(
                actor,
                CustomerActorReader.ReadBearerToken(Request),
                routeId,
                customerId,
                status,
                page,
                pageSize),
            cancellationToken);

        return result.Outcome switch
        {
            LoanExceptionOutcome.Success => Ok(result.Page),
            LoanExceptionOutcome.Invalid => BadRequest(new { message = result.Error }),
            LoanExceptionOutcome.Unauthorized => Unauthorized(),
            LoanExceptionOutcome.Forbidden => Forbid(),
            LoanExceptionOutcome.Unavailable => Problem(
                "The route authorization directory is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem("Unexpected loan exception search result.")
        };
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.GetAsync(
            new GetLoanExceptionQuery(actor, CustomerActorReader.ReadBearerToken(Request), id),
            cancellationToken);

        return MapResult(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.CancelAsync(
            new CancelLoanExceptionCommand(actor, CustomerActorReader.ReadBearerToken(Request), id),
            cancellationToken);

        return MapResult(result);
    }

    [HttpPost("{id:guid}/decision")]
    [Authorize(Policy = "tenant-admin")]
    public async Task<IActionResult> ApplyDecision(
        Guid id,
        ApplyDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.ApplyDecisionAsync(
            new ApplyLoanExceptionDecisionCommand(
                actor,
                id,
                new ApplyLoanExceptionDecisionInput(request.Decision, request.DecisionNotes)),
            cancellationToken);

        return MapResult(result);
    }

    private IActionResult MapResult(LoanExceptionResult result) => result.Outcome switch
    {
        LoanExceptionOutcome.Success => Ok(result.Exception),
        LoanExceptionOutcome.Invalid => BadRequest(new { message = result.Error }),
        LoanExceptionOutcome.NotFound => NotFound(),
        LoanExceptionOutcome.Unauthorized => Unauthorized(),
        LoanExceptionOutcome.Forbidden => Forbid(),
        LoanExceptionOutcome.Conflict => Conflict(new { message = result.Error }),
        LoanExceptionOutcome.Unavailable => Problem(
            "The route authorization directory is temporarily unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable),
        _ => Problem("Unexpected loan exception operation result.")
    };
}

public sealed record CreateLoanExceptionRequest(
    Guid CustomerId,
    decimal ProposedAmount,
    int ProposedDays,
    decimal ProposedPercentage,
    string Reason);

public sealed record ApplyDecisionRequest(
    LoanExceptionDecision Decision,
    string? DecisionNotes);
