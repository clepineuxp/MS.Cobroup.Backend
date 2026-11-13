namespace MS.Cobroup.Backend.Domain.Customers;

public sealed class Customer
{
    private readonly List<CustomerEvidence> additionalEvidences = [];
    public IReadOnlyCollection<CustomerEvidence> AdditionalEvidences => additionalEvidences.AsReadOnly();
    public int EvidenceCount => (EvidenceStorageKey is null ? 0 : 1) + additionalEvidences.Count;

    public void AddEvidence(string fileName, string contentType, long byteSize, string storageKey, string hash, Guid actor, DateTimeOffset now)
    {
        if (EvidenceCount >= 5) throw new InvalidOperationException("Se permiten máximo 5 evidencias por cliente.");
        if (EvidenceStorageKey is null) ReplaceEvidence(fileName, contentType, byteSize, storageKey, hash, actor, now);
        else additionalEvidences.Add(new CustomerEvidence(Id, additionalEvidences.Count + 2,
            Require(fileName, nameof(fileName), 255), Require(contentType, nameof(contentType), 100), byteSize,
            Require(storageKey, nameof(storageKey), 500), Require(hash, nameof(hash), 64), actor, now));
        UpdatedAtUtc = now;
    }

    private readonly List<CustomerPhone> phones = [];
    private readonly List<CustomerLocation> locations = [];

    private Customer()
    {
    }

    private Customer(
        Guid tenantId,
        Guid routeId,
        string firstNames,
        string lastNames,
        string? businessOrWorkName,
        string? observations,
        IReadOnlyCollection<CustomerPhoneValue> requestedPhones,
        IReadOnlyCollection<CustomerLocationValue> requestedLocations,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc,
        string identification)
    {
        if (tenantId == Guid.Empty || routeId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Tenant, route and creator are required.");
        }

        Identification = RequireIdentification(identification, allowLegacyZero: false);
        Id = Guid.NewGuid();
        TenantId = tenantId;
        RouteId = routeId;
        FirstNames = Require(firstNames, nameof(firstNames), 120);
        LastNames = Require(lastNames, nameof(lastNames), 120);
        BusinessOrWorkName = NormalizeOptional(businessOrWorkName, nameof(businessOrWorkName), 160);
        Observations = NormalizeOptional(observations, nameof(observations), 1000);
        Status = CustomerStatus.Active;
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        ReplaceContacts(requestedPhones, requestedLocations);
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid RouteId { get; private set; }

    public string Identification { get; private set; } = string.Empty;

    public string FirstNames { get; private set; } = string.Empty;

    public string LastNames { get; private set; } = string.Empty;

    public string? BusinessOrWorkName { get; private set; }

    public string? Observations { get; private set; }

    public CustomerStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string? EvidenceFileName { get; private set; }

    public string? EvidenceContentType { get; private set; }

    public long? EvidenceByteSize { get; private set; }

    public string? EvidenceStorageKey { get; private set; }

    public string? EvidenceFileHashSha256 { get; private set; }

    public Guid? EvidenceUploadedByUserId { get; private set; }

    public DateTimeOffset? EvidenceUploadedAtUtc { get; private set; }

    public IReadOnlyCollection<CustomerPhone> Phones => phones.AsReadOnly();

    public IReadOnlyCollection<CustomerLocation> Locations => locations.AsReadOnly();

    public void ReplaceEvidence(
        string fileName,
        string contentType,
        long byteSize,
        string storageKey,
        string fileHashSha256,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc)
    {
        EvidenceFileName = Require(fileName, nameof(fileName), 255);
        EvidenceContentType = Require(contentType, nameof(contentType), 100);
        if (byteSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteSize));
        }

        EvidenceByteSize = byteSize;
        EvidenceStorageKey = Require(storageKey, nameof(storageKey), 500);
        EvidenceFileHashSha256 = Require(fileHashSha256, nameof(fileHashSha256), 64);
        if (uploadedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Evidence uploader is required.", nameof(uploadedByUserId));
        }

        EvidenceUploadedByUserId = uploadedByUserId;
        EvidenceUploadedAtUtc = uploadedAtUtc;
        UpdatedAtUtc = uploadedAtUtc;
    }

    public static Customer Create(
        Guid tenantId,
        Guid routeId,
        string firstNames,
        string lastNames,
        string? businessOrWorkName,
        string? observations,
        IReadOnlyCollection<CustomerPhoneValue> phones,
        IReadOnlyCollection<CustomerLocationValue> locations,
        Guid createdByUserId,
        DateTimeOffset createdAtUtc,
        string identification) =>
        new(
            tenantId,
            routeId,
            firstNames,
            lastNames,
            businessOrWorkName,
            observations,
            phones,
            locations,
            createdByUserId,
            createdAtUtc,
            identification);

    public void Update(
        Guid routeId,
        string firstNames,
        string lastNames,
        string? businessOrWorkName,
        string? observations,
        CustomerStatus status,
        IReadOnlyCollection<CustomerPhoneValue> requestedPhones,
        IReadOnlyCollection<CustomerLocationValue> requestedLocations,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc,
        string identification)
    {
        if (routeId == Guid.Empty || updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Route and updater are required.");
        }

        Identification = RequireIdentification(identification, allowLegacyZero: Identification == "0");
        RouteId = routeId;
        FirstNames = Require(firstNames, nameof(firstNames), 120);
        LastNames = Require(lastNames, nameof(lastNames), 120);
        BusinessOrWorkName = NormalizeOptional(businessOrWorkName, nameof(businessOrWorkName), 160);
        Observations = NormalizeOptional(observations, nameof(observations), 1000);
        Status = status;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
        ReplaceContacts(requestedPhones, requestedLocations);
    }

    private void ReplaceContacts(
        IReadOnlyCollection<CustomerPhoneValue> requestedPhones,
        IReadOnlyCollection<CustomerLocationValue> requestedLocations)
    {
        if (requestedPhones.Count is < 1 or > 6)
        {
            throw new ArgumentException("A customer must have between 1 and 6 phones.", nameof(requestedPhones));
        }

        if (requestedLocations.Count is < 1 or > 4)
        {
            throw new ArgumentException("A customer must have between 1 and 4 locations.", nameof(requestedLocations));
        }

        if (requestedPhones.Count(item => item.IsPrimary) != 1)
        {
            throw new ArgumentException("Exactly one phone must be primary.", nameof(requestedPhones));
        }

        if (requestedLocations.Count(item => item.IsPrimary) != 1)
        {
            throw new ArgumentException("Exactly one location must be primary.", nameof(requestedLocations));
        }

        var normalizedPhones = requestedPhones
            .Select(item => new CustomerPhoneValue(CustomerPhone.Normalize(item.Number), item.IsPrimary))
            .ToArray();
        if (normalizedPhones.Select(item => item.Number).Distinct(StringComparer.Ordinal).Count() != normalizedPhones.Length)
        {
            throw new ArgumentException("Customer phones cannot be duplicated.", nameof(requestedPhones));
        }

        phones.Clear();
        phones.AddRange(normalizedPhones.Select(item => CustomerPhone.Create(Id, item.Number, item.IsPrimary)));
        locations.Clear();
        locations.AddRange(requestedLocations.Select(item => CustomerLocation.Create(
            Id,
            item.Latitude,
            item.Longitude,
            item.Alias,
            item.Address,
            item.IsPrimary,
            item.Source)));
    }

    private static string RequireIdentification(string value, bool allowLegacyZero)
    {
        var normalized = Require(value, nameof(Identification), 64);
        if (normalized == "0" && !allowLegacyZero)
        {
            throw new ArgumentException("Identification 0 is reserved for existing customers migrated without identification.", nameof(Identification));
        }
        return normalized;
    }

    private static string Require(string value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} must contain between 1 and {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{parameterName} cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }
}

public sealed record CustomerPhoneValue(string Number, bool IsPrimary);

public sealed record CustomerLocationValue(
    decimal Latitude,
    decimal Longitude,
    string? Alias,
    string? Address,
    bool IsPrimary,
    LocationSource Source);
