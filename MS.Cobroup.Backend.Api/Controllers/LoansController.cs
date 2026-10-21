using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Api.Security;
using MS.Cobroup.Backend.Core.Loans;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/loans")]
[Authorize(Policy = "tenant-user")]
public sealed class LoansController(LoanService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Originate(
        OriginateLoanRequest request,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.OriginateAsync(
            new OriginateLoanCommand(
                actor,
                CustomerActorReader.ReadBearerToken(Request),
                new OriginateLoanInput(
                    request.CustomerId,
                    request.PrincipalAmount,
                    request.InterestRate,
                    request.PaymentFrequency,
                    request.FrequencyNDays,
                    request.InstallmentCount,
                    request.LoanTemplateId,
                    request.LoanTemplateVersion,
                    request.ExceptionRequestId,
                    request.StartDate)),
            cancellationToken);

        return result.Outcome is LoanOutcome.Created
            ? Created($"/api/operations/v1/loans/{result.Loan!.Id}", result.Loan)
            : MapResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? routeId,
        [FromQuery] Guid? customerId,
        [FromQuery] LoanStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.SearchAsync(
            new SearchLoansQuery(
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
            LoanSearchOutcome.Success => Ok(result.Page),
            LoanSearchOutcome.Invalid => BadRequest(new { message = result.ErrorMessage }),
            LoanSearchOutcome.Unauthorized => Unauthorized(),
            LoanSearchOutcome.Forbidden => Forbid(),
            LoanSearchOutcome.Unavailable => Problem(
                "The route authorization directory is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem("Unexpected loan search result.")
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
            new GetLoanQuery(actor, CustomerActorReader.ReadBearerToken(Request), id),
            cancellationToken);

        return MapResult(result);
    }

    [HttpGet("by-customer/{customerId:guid}")]
    public async Task<IActionResult> ListByCustomer(Guid customerId, CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.ListByCustomerAsync(
            actor,
            CustomerActorReader.ReadBearerToken(Request),
            customerId,
            cancellationToken);

        return result.Outcome switch
        {
            LoanListOutcome.Success => Ok(result.Loans),
            LoanListOutcome.NotFound => NotFound(new { message = result.ErrorMessage }),
            LoanListOutcome.Forbidden => Forbid(),
            LoanListOutcome.Unauthorized => Unauthorized(),
            LoanListOutcome.Unavailable => Problem(
                "The route authorization directory is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem("Unexpected loan list result.")
        };
    }

    [HttpPost("preview-schedule")]
    public IActionResult PreviewSchedule([FromBody] PreviewLoanScheduleRequest request)
    {
        if (request.PrincipalAmount <= 0)
        {
            return BadRequest(new { message = "El monto principal debe ser positivo." });
        }

        if (request.InterestRate <= 0)
        {
            return BadRequest(new { message = "La tasa de interés debe ser positiva." });
        }

        if (request.InstallmentCount <= 0)
        {
            return BadRequest(new { message = "El número de cuotas debe ser positivo." });
        }

        if (request.PaymentFrequency is PaymentFrequency.EveryNDays && (!request.FrequencyNDays.HasValue || request.FrequencyNDays.Value <= 0))
        {
            return BadRequest(new { message = "Los días de frecuencia deben ser positivos cuando se selecciona Cada N Días." });
        }

        var schedule = LoanScheduleCalculator.Calculate(
            request.PrincipalAmount,
            request.InterestRate,
            request.PaymentFrequency,
            request.FrequencyNDays,
            request.InstallmentCount,
            request.StartDate);

        return Ok(schedule);
    }

    private IActionResult MapResult(LoanResult result) => result.Outcome switch
    {
        LoanOutcome.Success => Ok(result.Loan),
        LoanOutcome.Created => Created($"/api/operations/v1/loans/{result.Loan!.Id}", result.Loan),
        LoanOutcome.Invalid => BadRequest(new { message = result.ErrorMessage }),
        LoanOutcome.NotFound => NotFound(new { message = result.ErrorMessage }),
        LoanOutcome.Unauthorized => Unauthorized(),
        LoanOutcome.Forbidden => Forbid(),
        LoanOutcome.Conflict => Conflict(new { message = result.ErrorMessage }),
        LoanOutcome.Unavailable => Problem(
            "The authorization or template directory is temporarily unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable),
        _ => Problem("Unexpected loan operation result.")
    };
}

public sealed record OriginateLoanRequest(
    Guid CustomerId,
    decimal PrincipalAmount,
    decimal InterestRate,
    PaymentFrequency PaymentFrequency,
    int? FrequencyNDays,
    int InstallmentCount,
    Guid? LoanTemplateId,
    int? LoanTemplateVersion,
    Guid? ExceptionRequestId,
    DateOnly StartDate);

public sealed record PreviewLoanScheduleRequest(
    decimal PrincipalAmount,
    decimal InterestRate,
    PaymentFrequency PaymentFrequency,
    int? FrequencyNDays,
    int InstallmentCount,
    DateOnly StartDate);
