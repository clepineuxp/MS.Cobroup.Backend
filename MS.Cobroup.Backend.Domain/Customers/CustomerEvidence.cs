namespace MS.Cobroup.Backend.Domain.Customers;

public sealed class CustomerEvidence
{
    private CustomerEvidence() { }
    internal CustomerEvidence(Guid customerId, int slot, string fileName, string contentType, long byteSize,
        string storageKey, string hash, Guid uploadedByUserId, DateTimeOffset uploadedAtUtc)
    {
        if (slot < 2 || slot > 5 || byteSize <= 0 || uploadedByUserId == Guid.Empty)
            throw new ArgumentException("Invalid customer evidence.");
        CustomerId = customerId; Slot = slot; FileName = fileName; ContentType = contentType;
        ByteSize = byteSize; StorageKey = storageKey; FileHashSha256 = hash;
        UploadedByUserId = uploadedByUserId; UploadedAtUtc = uploadedAtUtc;
    }
    public Guid CustomerId { get; private set; }
    public int Slot { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long ByteSize { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string FileHashSha256 { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }
}
