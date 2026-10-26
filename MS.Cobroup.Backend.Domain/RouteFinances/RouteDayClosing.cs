namespace MS.Cobroup.Backend.Domain.RouteFinances;

public sealed class RouteDayClosing
{
    private RouteDayClosing()
    {
    }

    public RouteDayClosing(
        Guid id,
        Guid tenantId,
        Guid routeId,
        DateOnly businessDate,
        bool isClosed,
        DateTimeOffset? closedAtUtc,
        Guid? closedByUserId,
        string? closedByDisplayName,
        DateTimeOffset? reopenedAtUtc,
        Guid? reopenedByUserId,
        string? reopenedByDisplayName)
    {
        Id = id;
        TenantId = tenantId;
        RouteId = routeId;
        BusinessDate = businessDate;
        IsClosed = isClosed;
        ClosedAtUtc = closedAtUtc;
        ClosedByUserId = closedByUserId;
        ClosedByDisplayName = closedByDisplayName;
        ReopenedAtUtc = reopenedAtUtc;
        ReopenedByUserId = reopenedByUserId;
        ReopenedByDisplayName = reopenedByDisplayName;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid RouteId { get; private set; }

    public DateOnly BusinessDate { get; private set; }

    public bool IsClosed { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public string? ClosedByDisplayName { get; private set; }

    public DateTimeOffset? ReopenedAtUtc { get; private set; }

    public Guid? ReopenedByUserId { get; private set; }

    public string? ReopenedByDisplayName { get; private set; }

    public static RouteDayClosing CreateClosed(
        Guid tenantId,
        Guid routeId,
        DateOnly businessDate,
        Guid closedByUserId,
        string closedByDisplayName,
        DateTimeOffset closedAtUtc) =>
        new(
            Guid.NewGuid(),
            tenantId,
            routeId,
            businessDate,
            isClosed: true,
            closedAtUtc: closedAtUtc,
            closedByUserId: closedByUserId,
            closedByDisplayName: closedByDisplayName,
            reopenedAtUtc: null,
            reopenedByUserId: null,
            reopenedByDisplayName: null);

    public void Close(Guid closedByUserId, string closedByDisplayName, DateTimeOffset closedAtUtc)
    {
        IsClosed = true;
        ClosedByUserId = closedByUserId;
        ClosedByDisplayName = closedByDisplayName;
        ClosedAtUtc = closedAtUtc;
    }

    public void Reopen(Guid reopenedByUserId, string reopenedByDisplayName, DateTimeOffset reopenedAtUtc)
    {
        IsClosed = false;
        ReopenedByUserId = reopenedByUserId;
        ReopenedByDisplayName = reopenedByDisplayName;
        ReopenedAtUtc = reopenedAtUtc;
    }
}
