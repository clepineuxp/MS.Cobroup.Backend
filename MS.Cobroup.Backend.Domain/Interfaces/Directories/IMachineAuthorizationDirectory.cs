namespace MS.Cobroup.Backend.Domain.Interfaces.Directories;

public interface IMachineAuthorizationDirectory
{
    Task<MachineAuthorizationDirectoryResult> CheckAsync(string accessToken, Guid routeId, string machineId, CancellationToken cancellationToken);
}

public sealed record MachineAuthorizationDirectoryResult(MachineAuthorizationDirectoryOutcome Outcome, DateTimeOffset? AuthorizedUntilUtc = null, bool Indefinite = false)
{
    public static MachineAuthorizationDirectoryResult Allowed(DateTimeOffset? until, bool indefinite) => new(MachineAuthorizationDirectoryOutcome.Allowed, until, indefinite);
    public static MachineAuthorizationDirectoryResult Denied() => new(MachineAuthorizationDirectoryOutcome.Denied);
    public static MachineAuthorizationDirectoryResult Unauthorized() => new(MachineAuthorizationDirectoryOutcome.Unauthorized);
    public static MachineAuthorizationDirectoryResult Unavailable() => new(MachineAuthorizationDirectoryOutcome.Unavailable);
}
public enum MachineAuthorizationDirectoryOutcome { Allowed, Denied, Unauthorized, Unavailable }
