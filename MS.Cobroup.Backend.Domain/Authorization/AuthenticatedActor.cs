namespace MS.Cobroup.Backend.Domain.Authorization;

public sealed record AuthenticatedActor(
    Guid UserId,
    Guid TenantId,
    IReadOnlySet<UserRole> Roles,
    IReadOnlySet<Guid> RouteIds);
