using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Api.Security;
using MS.Cobroup.Backend.Core.Agenda;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/agenda")]
[Authorize(Policy = "tenant-user")]
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
