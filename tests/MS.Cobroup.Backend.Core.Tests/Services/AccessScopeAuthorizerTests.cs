using MS.Cobroup.Backend.Core.Services;
using MS.Cobroup.Backend.Domain.Authorization;

namespace MS.Cobroup.Backend.Core.Tests.Services;

public sealed class AccessScopeAuthorizerTests
{
    private readonly AccessScopeAuthorizer _authorizer = new();

    [Fact]
    public void IsInTenant_DeniesAnotherTenant()
    {
        var actor = CreateActor(UserRole.Collector);

        Assert.False(_authorizer.IsInTenant(actor, Guid.NewGuid()));
    }

    [Fact]
    public void CanAccessRoute_DeniesUnassignedCollector()
    {
        var actor = CreateActor(UserRole.Collector);

        Assert.False(_authorizer.CanAccessRoute(actor, actor.TenantId, Guid.NewGuid()));
    }

    [Fact]
    public void CanAccessRoute_AllowsAdminWithinAuthorizationContract()
    {
        var actor = CreateActor(UserRole.Admin);

        Assert.True(_authorizer.CanAccessRoute(actor, actor.TenantId, Guid.NewGuid()));
    }

    [Fact]
    public void CanAccessRoute_DeniesAdminFromAnotherTenant()
    {
        var actor = CreateActor(UserRole.Admin);

        Assert.False(_authorizer.CanAccessRoute(actor, Guid.NewGuid(), Guid.NewGuid()));
    }

    private static AuthenticatedActor CreateActor(UserRole role) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        new HashSet<UserRole> { role },
        new HashSet<Guid>());
}
