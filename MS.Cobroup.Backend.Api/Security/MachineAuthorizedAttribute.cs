using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Domain.Interfaces.Directories;

namespace MS.Cobroup.Backend.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class MachineAuthorizedAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var machineId = context.HttpContext.Request.Headers["X-Machine-Id"].FirstOrDefault();
        var routeValue = context.HttpContext.Request.Headers["X-Route-Id"].FirstOrDefault();
        if (!Guid.TryParse(routeValue, out var routeId) || string.IsNullOrWhiteSpace(machineId))
        {
            context.Result = new ObjectResult(new { code = "machine_authorization_required", message = "Selecciona una ruta y registra este dispositivo." }) { StatusCode = StatusCodes.Status428PreconditionRequired };
            return;
        }
        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization[7..].Trim() : string.Empty;
        var directory = context.HttpContext.RequestServices.GetRequiredService<IMachineAuthorizationDirectory>();
        var result = await directory.CheckAsync(token, routeId, machineId, context.HttpContext.RequestAborted);
        if (result.Outcome is MachineAuthorizationDirectoryOutcome.Allowed) { await next(); return; }
        context.Result = result.Outcome is MachineAuthorizationDirectoryOutcome.Unauthorized
            ? new UnauthorizedResult()
            : new ObjectResult(new { code = "machine_authorization_required", message = result.Outcome is MachineAuthorizationDirectoryOutcome.Unavailable ? "No fue posible validar la autorización del dispositivo." : "Este dispositivo no está autorizado para la ruta seleccionada." }) { StatusCode = result.Outcome is MachineAuthorizationDirectoryOutcome.Unavailable ? 503 : 428 };
    }
}
