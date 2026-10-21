using MS.Cobroup.Backend.Domain.Authorization;

namespace MS.Cobroup.Backend.Domain.Interfaces.Services;

public interface IAccessScopeAuthorizer
{
    bool IsInTenant(AuthenticatedActor actor, Guid tenantId);

    bool CanAccessRoute(AuthenticatedActor actor, Guid tenantId, Guid routeId);
}
