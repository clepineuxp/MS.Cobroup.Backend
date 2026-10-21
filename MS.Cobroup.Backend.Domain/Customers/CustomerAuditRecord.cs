namespace MS.Cobroup.Backend.Domain.Customers;

public sealed class CustomerAuditRecord
{
    private CustomerAuditRecord()
    {
    }

    private CustomerAuditRecord(
        Guid tenantId,
        Guid customerId,
        Guid actorUserId,
        CustomerAuditAction action,
        string detailsJson,
        DateTimeOffset occurredAtUtc)
    {
        if (tenantId == Guid.Empty || customerId == Guid.Empty || actorUserId == Guid.Empty)
        {
            throw new ArgumentException("Tenant, customer and actor are required.");
        }

        if (string.IsNullOrWhiteSpace(detailsJson) || detailsJson.Length > 4000)
        {
            throw new ArgumentException("Audit details are required and cannot exceed 4000 characters.", nameof(detailsJson));
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        CustomerId = customerId;
        ActorUserId = actorUserId;
        Action = action;
        DetailsJson = detailsJson;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public CustomerAuditAction Action { get; private set; }

    public string DetailsJson { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static CustomerAuditRecord Create(
        Guid tenantId,
        Guid customerId,
        Guid actorUserId,
        CustomerAuditAction action,
        string detailsJson,
        DateTimeOffset occurredAtUtc) =>
        new(tenantId, customerId, actorUserId, action, detailsJson, occurredAtUtc);
}

public enum CustomerAuditAction
{
    Created,
    Updated,
    Transferred,
    StatusChanged,
    TransferredAndStatusChanged
}
