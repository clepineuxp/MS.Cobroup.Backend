namespace MS.Cobroup.Backend.Domain.Interfaces.Directories;

public interface IAdministrativeRouteDirectory
{
    Task<RouteDirectoryResult> ListAccessibleAsync(
        string accessToken,
        CancellationToken cancellationToken);
}

public sealed record AccessibleRoute(
    Guid Id,
    string Name,
    string? Description,
    string AccessAs,
    int? MaxActiveCustomers,
    int? MaxActiveLoans);

public enum RouteDirectoryOutcome
{
    Success,
    Unauthorized,
    Forbidden,
    Unavailable
}

public sealed record RouteDirectoryResult(
    RouteDirectoryOutcome Outcome,
    IReadOnlyCollection<AccessibleRoute> Routes)
{
    public static RouteDirectoryResult Success(IReadOnlyCollection<AccessibleRoute> routes) =>
        new(RouteDirectoryOutcome.Success, routes);

    public static RouteDirectoryResult Unauthorized() => new(RouteDirectoryOutcome.Unauthorized, []);

    public static RouteDirectoryResult Forbidden() => new(RouteDirectoryOutcome.Forbidden, []);

    public static RouteDirectoryResult Unavailable() => new(RouteDirectoryOutcome.Unavailable, []);
}
