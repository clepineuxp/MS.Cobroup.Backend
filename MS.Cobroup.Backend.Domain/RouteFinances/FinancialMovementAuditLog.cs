namespace MS.Cobroup.Backend.Domain.RouteFinances;

public enum FinancialMovementEntityType
{
    Loan,
    Payment,
    RouteFinancialEntry
}

public enum FinancialMovementAuditAction
{
    Created,
    Updated,
    Deleted
}

public sealed class FinancialMovementAuditLog
{
    private FinancialMovementAuditLog()
    {
    }

    public FinancialMovementAuditLog(
        Guid id,
        Guid tenantId,
        Guid routeId,
        FinancialMovementEntityType entityType,
        Guid entityId,
        FinancialMovementAuditAction action,
        string concept,
        decimal amount,
        string details,
        Guid performedByUserId,
        string performedByDisplayName,
        string performedByUserRole,
        DateTimeOffset timestampUtc)
    {
        Id = id;
        TenantId = tenantId;
        RouteId = routeId;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        Concept = concept;
        Amount = amount;
        Details = details;
        PerformedByUserId = performedByUserId;
        PerformedByDisplayName = performedByDisplayName;
        PerformedByUserRole = performedByUserRole;
        TimestampUtc = timestampUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid RouteId { get; private set; }

    public FinancialMovementEntityType EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public FinancialMovementAuditAction Action { get; private set; }

    public string Concept { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Details { get; private set; } = string.Empty;

    public Guid PerformedByUserId { get; private set; }

    public string PerformedByDisplayName { get; private set; } = string.Empty;

    public string PerformedByUserRole { get; private set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; private set; }

    public static FinancialMovementAuditLog Create(
        Guid tenantId,
        Guid routeId,
        FinancialMovementEntityType entityType,
        Guid entityId,
        FinancialMovementAuditAction action,
        string concept,
        decimal amount,
        string details,
        Guid performedByUserId,
        string performedByDisplayName,
        string performedByUserRole,
        DateTimeOffset timestampUtc) =>
        new(
            Guid.NewGuid(),
            tenantId,
            routeId,
            entityType,
            entityId,
            action,
            concept,
            amount,
            details,
            performedByUserId,
            performedByDisplayName,
            performedByUserRole,
            timestampUtc);
}
