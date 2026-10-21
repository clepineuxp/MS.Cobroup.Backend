using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Interfaces.Services;

namespace MS.Cobroup.Backend.Core.Services;

public sealed class AccessScopeAuthorizer : IAccessScopeAuthorizer
{
    public bool IsInTenant(AuthenticatedActor actor, Guid tenantId) =>
        actor.TenantId != Guid.Empty && actor.TenantId == tenantId;

    public bool CanAccessRoute(AuthenticatedActor actor, Guid tenantId, Guid routeId) =>
        IsInTenant(actor, tenantId) &&
        routeId != Guid.Empty &&
        (actor.Roles.Contains(UserRole.Admin) || actor.RouteIds.Contains(routeId));
}
