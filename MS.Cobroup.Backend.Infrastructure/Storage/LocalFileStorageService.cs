using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MS.Cobroup.Backend.Domain.Interfaces.Services;

namespace MS.Cobroup.Backend.Infrastructure.Storage;

/// <summary>
/// Adaptador de compatibilidad que delega en <see cref="FileSystemObjectStorage"/>.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly FileSystemObjectStorage innerStorage;

    public LocalFileStorageService(IConfiguration configuration)
    {
        var root = configuration["Storage:EvidencePath"]
            ?? configuration["Storage:RootPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage", "evidences");

        var maxBytes = 10L * 1024 * 1024;
        if (long.TryParse(configuration["Storage:MaxFileSizeBytes"], out var parsed) && parsed > 0)
        {
            maxBytes = parsed;
        }

        var options = Options.Create(new FileSystemStorageOptions
        {
            Provider = "FileSystem",
            RootPath = root,
            MaxFileSizeBytes = maxBytes
        });

        innerStorage = new FileSystemObjectStorage(options);
    }

    public Task<StoredFileResult> PutAsync(
        string logicalKey,
        Stream stream,
        string declaredContentType,
        CancellationToken cancellationToken) =>
        innerStorage.PutAsync(logicalKey, stream, declaredContentType, cancellationToken);

    public Task<Stream?> OpenReadAsync(
        string logicalKey,
        CancellationToken cancellationToken) =>
        innerStorage.OpenReadAsync(logicalKey, cancellationToken);

    public Task<bool> DeleteAsync(
        string logicalKey,
        CancellationToken cancellationToken) =>
        innerStorage.DeleteAsync(logicalKey, cancellationToken);

    public Task<bool> ExistsAsync(
        string logicalKey,
        CancellationToken cancellationToken) =>
        innerStorage.ExistsAsync(logicalKey, cancellationToken);

    public Task<FileMetadataResult?> GetMetadataAsync(
        string logicalKey,
        CancellationToken cancellationToken) =>
        innerStorage.GetMetadataAsync(logicalKey, cancellationToken);

    public Task<StoredFileResult> StoreEvidenceAsync(
        Guid tenantId,
        Guid paymentId,
        string fileName,
        string declaredContentType,
        Stream stream,
        CancellationToken cancellationToken) =>
        innerStorage.StoreEvidenceAsync(tenantId, paymentId, fileName, declaredContentType, stream, cancellationToken);

    public Task<Stream?> ReadEvidenceAsync(
        Guid tenantId,
        string storageKey,
        CancellationToken cancellationToken) =>
        innerStorage.ReadEvidenceAsync(tenantId, storageKey, cancellationToken);

    public Task DeleteEvidenceAsync(
        Guid tenantId,
        string storageKey,
        CancellationToken cancellationToken) =>
        innerStorage.DeleteEvidenceAsync(tenantId, storageKey, cancellationToken);
}
