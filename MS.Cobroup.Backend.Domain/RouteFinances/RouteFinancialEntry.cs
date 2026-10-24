namespace MS.Cobroup.Backend.Domain.RouteFinances;

public sealed class RouteFinancialEntry
{
    private RouteFinancialEntry()
    {
    }

    private RouteFinancialEntry(
        Guid id,
        Guid tenantId,
        Guid routeId,
        RouteFinancialEntryType type,
        string concept,
        string? description,
        decimal amount,
        Guid createdByUserId,
        string createdByDisplayName,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = RequireId(tenantId, nameof(tenantId));
        RouteId = RequireId(routeId, nameof(routeId));
        Type = RequireType(type);
        Concept = RequireText(concept, nameof(concept), 160);
        Description = OptionalText(description, nameof(description), 1000);
        Amount = RequireAmount(amount);
        CreatedByUserId = RequireId(createdByUserId, nameof(createdByUserId));
        CreatedByDisplayName = RequireText(createdByDisplayName, nameof(createdByDisplayName), 160);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        UpdatedByUserId = createdByUserId;
        UpdatedByDisplayName = CreatedByDisplayName;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RouteId { get; private set; }
    public RouteFinancialEntryType Type { get; private set; }
    public string Concept { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Amount { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string CreatedByDisplayName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public string UpdatedByDisplayName { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static RouteFinancialEntry Create(
        Guid tenantId,
        Guid routeId,
        RouteFinancialEntryType type,
        string concept,
        string? description,
        decimal amount,
        Guid createdByUserId,
        string createdByDisplayName,
        DateTimeOffset createdAtUtc) =>
        new(Guid.NewGuid(), tenantId, routeId, type, concept, description, amount,
            createdByUserId, createdByDisplayName, createdAtUtc);

    public void Update(
        RouteFinancialEntryType type,
        string concept,
        string? description,
        decimal amount,
        Guid updatedByUserId,
        string updatedByDisplayName,
        DateTimeOffset updatedAtUtc)
    {
        Type = RequireType(type);
        Concept = RequireText(concept, nameof(concept), 160);
        Description = OptionalText(description, nameof(description), 1000);
        Amount = RequireAmount(amount);
        UpdatedByUserId = RequireId(updatedByUserId, nameof(updatedByUserId));
        UpdatedByDisplayName = RequireText(updatedByDisplayName, nameof(updatedByDisplayName), 160);
        UpdatedAtUtc = updatedAtUtc;
    }

    private static Guid RequireId(Guid value, string name) =>
        value != Guid.Empty ? value : throw new ArgumentException("The identifier is required.", name);

    private static RouteFinancialEntryType RequireType(RouteFinancialEntryType value) =>
        Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));

    private static decimal RequireAmount(decimal value) =>
        value > 0m ? value : throw new ArgumentOutOfRangeException(nameof(value), "El monto debe ser mayor que cero.");

    private static string RequireText(string? value, string name, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 && normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"{name} debe contener entre 1 y {maximumLength} caracteres.", name);
    }

    private static string? OptionalText(string? value, string name, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"{name} no puede superar {maximumLength} caracteres.", name);
    }
}
