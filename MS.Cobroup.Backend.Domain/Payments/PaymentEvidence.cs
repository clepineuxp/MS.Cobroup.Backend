namespace MS.Cobroup.Backend.Domain.Payments;

public sealed class PaymentEvidence
{
    private PaymentEvidence()
    {
    }

    public PaymentEvidence(
        Guid id,
        Guid paymentId,
        Guid tenantId,
        string fileName,
        string contentType,
        long byteSize,
        string storagePath,
        string fileHashSha256,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc)
    {
        Id = id;
        PaymentId = paymentId;
        TenantId = tenantId;
        FileName = fileName;
        ContentType = contentType;
        ByteSize = byteSize;
        StoragePath = storagePath;
        FileHashSha256 = fileHashSha256;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = uploadedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid TenantId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long ByteSize { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string FileHashSha256 { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public string? DeletionReason { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    public static PaymentEvidence Create(
        Guid paymentId,
        Guid tenantId,
        string fileName,
        string contentType,
        long byteSize,
        string storagePath,
        string fileHashSha256,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileHashSha256);

        if (byteSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteSize), "Byte size must be positive.");
        }

        return new(
            Guid.NewGuid(),
            paymentId,
            tenantId,
            fileName.Trim(),
            contentType.Trim().ToLowerInvariant(),
            byteSize,
            storagePath.Trim(),
            fileHashSha256.Trim().ToLowerInvariant(),
            uploadedByUserId,
            uploadedAtUtc);
    }

    public static PaymentEvidence Create(
        Guid paymentId,
        Guid tenantId,
        string fileName,
        string contentType,
        long byteSize,
        string storagePath,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAtUtc) =>
        Create(paymentId, tenantId, fileName, contentType, byteSize, storagePath, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", uploadedByUserId, uploadedAtUtc);

    public void MarkDeleted(Guid adminUserId, string reason, DateTimeOffset deletedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        IsDeleted = true;
        DeletedByUserId = adminUserId;
        DeletionReason = reason.Trim();
        DeletedAtUtc = deletedAtUtc;
    }
}
