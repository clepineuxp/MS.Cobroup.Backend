namespace MS.Cobroup.Backend.Domain.Agenda;

public sealed class NonPaymentReason
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private NonPaymentReason() { }

    public NonPaymentReason(
        Guid id,
        Guid tenantId,
        string name,
        string? description,
        bool isActive,
        int displayOrder,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        Description = description?.Trim();
        IsActive = isActive;
        DisplayOrder = displayOrder;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Update(string name, string? description, int displayOrder)
    {
        Name = name.Trim();
        Description = description?.Trim();
        DisplayOrder = displayOrder;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

