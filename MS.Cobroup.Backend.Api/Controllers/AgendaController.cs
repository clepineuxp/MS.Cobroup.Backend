using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Api.Security;
using MS.Cobroup.Backend.Core.Agenda;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/agenda")]
[Authorize(Policy = "tenant-user")]
[MachineAuthorized]
public sealed class AgendaController(AgendaService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAgenda(
        [FromQuery] Guid? routeId,
        [FromQuery] DateOnly? targetDate,
        [FromQuery] decimal? originLat,
        [FromQuery] decimal? originLon,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var token = CustomerActorReader.ReadBearerToken(Request);
        var query = new GetAgendaQuery(actor, token, routeId, targetDate, originLat, originLon);
        var result = await service.GetAgendaAsync(query, cancellationToken);

        return MapResult(result);
    }

    [HttpGet("map-locations")]
    public async Task<IActionResult> GetMapLocations(
        [FromQuery] Guid? routeId,
        [FromQuery] DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var token = CustomerActorReader.ReadBearerToken(Request);
        var query = new GetMapLocationsQuery(actor, token, routeId, targetDate);
        var result = await service.GetMapLocationsAsync(query, cancellationToken);

        return MapResult(result);
    }

    [HttpGet("route-analytics")]
    public async Task<IActionResult> GetRouteAnalytics(
        [FromQuery] Guid? routeId,
        [FromQuery] DateOnly? targetDate,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var token = CustomerActorReader.ReadBearerToken(Request);
        var query = new GetRouteAnalyticsQuery(actor, token, routeId, targetDate);
        var result = await service.GetRouteAnalyticsAsync(query, cancellationToken);

        return MapResult(result);
    }

    [HttpPost("optimize-sequence")]
    public async Task<IActionResult> OptimizeSequence(
        [FromBody] OptimizeSequenceInput input,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var token = CustomerActorReader.ReadBearerToken(Request);
        var command = new OptimizeSequenceCommand(actor, token, input);
        var result = await service.OptimizeSequenceAsync(command, cancellationToken);

        return MapResult(result);
    }

    [HttpGet("non-payment-reasons")]
    public async Task<IActionResult> GetNonPaymentReasons(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await service.GetNonPaymentReasonsAsync(actor, includeInactive, cancellationToken);
        return MapResult(result);
    }

    [HttpPost("non-payment-reasons")]
    public async Task<IActionResult> CreateNonPaymentReason(
        [FromBody] CreateNonPaymentReasonInput input,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var command = new CreateNonPaymentReasonCommand(actor, input);
        var result = await service.CreateNonPaymentReasonAsync(command, cancellationToken);
        return MapResult(result);
    }

    [HttpPut("non-payment-reasons/{id:guid}/status")]
    public async Task<IActionResult> ToggleNonPaymentReasonStatus(
        [FromRoute] Guid id,
        [FromBody] ToggleReasonStatusInput input,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var command = new ToggleNonPaymentReasonStatusCommand(actor, id, input.IsActive);
        var result = await service.ToggleNonPaymentReasonStatusAsync(command, cancellationToken);
        return MapResult(result);
    }

    [HttpPut("non-payment-reasons/{id:guid}")]
    public async Task<IActionResult> UpdateNonPaymentReason(
        [FromRoute] Guid id,
        [FromBody] UpdateNonPaymentReasonInput input,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var command = new UpdateNonPaymentReasonCommand(actor, id, input);
        var result = await service.UpdateNonPaymentReasonAsync(command, cancellationToken);
        return MapResult(result);
    }

    [HttpDelete("non-payment-reasons/{id:guid}")]
    public async Task<IActionResult> DeleteNonPaymentReason(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var command = new DeleteNonPaymentReasonCommand(actor, id);
        var result = await service.DeleteNonPaymentReasonAsync(command, cancellationToken);
        return MapResult(result);
    }

    [HttpPost("will-not-pay")]
    public async Task<IActionResult> RegisterWillNotPay(
        [FromBody] RegisterWillNotPayInput input,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var token = CustomerActorReader.ReadBearerToken(Request);
        var command = new RegisterWillNotPayCommand(actor, token, input);
        var result = await service.RegisterWillNotPayAsync(command, cancellationToken);
        return MapResult(result);
    }

    [HttpDelete("will-not-pay")]
    public async Task<IActionResult> CancelWillNotPay(
        [FromQuery] Guid routeId,
        [FromQuery] Guid customerId,
        [FromQuery] Guid? loanId,
        [FromQuery] DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var token = CustomerActorReader.ReadBearerToken(Request);
        var command = new CancelWillNotPayCommand(actor, token, new CancelWillNotPayInput(routeId, customerId, loanId, businessDate));
        var result = await service.CancelWillNotPayAsync(command, cancellationToken);
        return MapResult(result);
    }

    private IActionResult MapResult<T>(AgendaResult<T> result) =>
        result.Outcome switch
        {
            AgendaOutcome.Success => Ok(result.Data),
            AgendaOutcome.Forbidden => Forbid(),
            AgendaOutcome.Unauthorized => Unauthorized(),
            AgendaOutcome.Invalid => BadRequest(new ProblemDetails
            {
                Title = "Parámetros de consulta inválidos",
                Detail = result.ErrorMessage,
                Status = StatusCodes.Status400BadRequest
            }),
            _ => Problem(result.ErrorMessage ?? "Servicio no disponible.", statusCode: StatusCodes.Status503ServiceUnavailable)
        };
}
