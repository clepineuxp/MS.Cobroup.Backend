using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Api.Security;
using MS.Cobroup.Backend.Core.RouteFinances;
using MS.Cobroup.Backend.Domain.RouteFinances;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/route-finances")]
[Authorize(Policy = "tenant-user")]
[MachineAuthorized]
public sealed class RouteFinancesController(RouteFinanceService service) : ControllerBase
{
    [HttpGet("{routeId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid routeId, CancellationToken cancellationToken)
    {
        if (!TryReadCommand(out var command)) return Unauthorized();
        return Map(await service.GetBalanceAsync(command, routeId, cancellationToken));
    }

    [HttpGet("entries")]
    public async Task<IActionResult> ListEntries(
        [FromQuery] Guid routeId,
        [FromQuery] string section,
        CancellationToken cancellationToken)
    {
        if (!TryReadCommand(out var command)) return Unauthorized();
        return Map(await service.ListEntriesAsync(command, routeId, section, cancellationToken));
    }

    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry(
        SaveRouteFinancialEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadCommand(out var command)) return Unauthorized();
        return Map(await service.CreateEntryAsync(command, request.ToInput(), cancellationToken));
    }

    [HttpPut("entries/{entryId:guid}")]
    public async Task<IActionResult> UpdateEntry(
        Guid entryId,
        SaveRouteFinancialEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadCommand(out var command)) return Unauthorized();
        return Map(await service.UpdateEntryAsync(command, entryId, request.ToInput(), cancellationToken));
    }

    [HttpDelete("entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(
        Guid entryId,
        CancellationToken cancellationToken)
    {
        if (!TryReadCommand(out var command)) return Unauthorized();
        return Map(await service.DeleteEntryAsync(command, entryId, cancellationToken));
    }

    [HttpPost("{routeId:guid}/close-day")]
    public async Task<IActionResult> CloseDay(
        Guid routeId,
        CancellationToken cancellationToken)
    {
        if (!TryReadCommand(out var command)) return Unauthorized();
        return Map(await service.CloseDayAsync(command, routeId, cancellationToken));
    }

    [HttpPost("{routeId:guid}/reopen-day")]
    public async Task<IActionResult> ReopenDay(
        Guid routeId,
        CancellationToken cancellationToken)
    {
        if (!TryReadCommand(out var command)) return Unauthorized();
        return Map(await service.ReopenDayAsync(command, routeId, cancellationToken));
    }

    [HttpGet("{routeId:guid}/closing-status")]
    public async Task<IActionResult> GetDayClosingStatus(
        Guid routeId,
        CancellationToken cancellationToken)
    {
        if (!TryReadCommand(out var command)) return Unauthorized();
        return Map(await service.GetDayClosingStatusAsync(command, routeId, cancellationToken));
    }

    [HttpGet("audit-trace")]
    public async Task<IActionResult> ListAuditTrace(
        [FromQuery] Guid? routeId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        if (!TryReadCommand(out var command)) return Unauthorized();
        return Map(await service.ListAuditTraceAsync(command, routeId, fromUtc, toUtc, cancellationToken));
    }

    private bool TryReadCommand(out RouteFinanceCommand command)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            command = null!;
            return false;
        }
        var displayName = User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("name")
            ?? User.FindFirstValue("preferred_username")
            ?? actor.UserId.ToString("D");
        command = new RouteFinanceCommand(actor, displayName, CustomerActorReader.ReadBearerToken(Request));
        return true;
    }

    private IActionResult Map(RouteFinanceResult result) => result.Outcome switch
    {
        RouteFinanceOutcome.Success when result.Balance is not null => Ok(result.Balance),
        RouteFinanceOutcome.Success when result.Entries is not null => Ok(result.Entries),
        RouteFinanceOutcome.Success when result.ClosingStatus is not null => Ok(result.ClosingStatus),
        RouteFinanceOutcome.Success when result.AuditLogs is not null => Ok(result.AuditLogs),
        RouteFinanceOutcome.Success when result.Entry is not null => Ok(result.Entry),
        RouteFinanceOutcome.Success => NoContent(),
        RouteFinanceOutcome.Created => Created($"/api/operations/v1/route-finances/entries/{result.Entry!.Id}", result.Entry),
        RouteFinanceOutcome.Invalid => BadRequest(new { message = result.Error }),
        RouteFinanceOutcome.NotFound => NotFound(),
        RouteFinanceOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error ?? "No autorizado para esta acción." }),
        RouteFinanceOutcome.Unauthorized => Unauthorized(),
        RouteFinanceOutcome.Unavailable => Problem("El directorio administrativo de rutas no está disponible.", statusCode: 503),
        _ => Problem("Resultado financiero inesperado.")
    };
}

public sealed record SaveRouteFinancialEntryRequest(
    Guid RouteId,
    RouteFinancialEntryType Type,
    string Concept,
    string? Description,
    decimal Amount)
{
    public SaveRouteFinancialEntryInput ToInput() => new(RouteId, Type, Concept, Description, Amount);
}
