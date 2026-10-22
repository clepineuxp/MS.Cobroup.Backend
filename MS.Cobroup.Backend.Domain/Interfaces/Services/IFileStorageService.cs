namespace MS.Cobroup.Backend.Domain.Interfaces.Services;

public interface IFileStorageService
{
    Task<StoredFileResult> StoreEvidenceAsync(
        Guid tenantId,
        Guid paymentId,
        string fileName,
        string declaredContentType,
        Stream stream,
        CancellationToken cancellationToken);

    Task<Stream?> ReadEvidenceAsync(
        Guid tenantId,
        string storageKey,
        CancellationToken cancellationToken);

    Task DeleteEvidenceAsync(
        Guid tenantId,
        string storageKey,
        CancellationToken cancellationToken);
}

public sealed record StoredFileResult(
    string StorageKey,
    string ContentType,
    long ByteSize,
    string FileHashSha256)
{
    /// <summary>
    /// Alias de compatibilidad hacia atrás que devuelve la clave lógica relativa.
    /// </summary>
    public string StoragePath => StorageKey;
}
