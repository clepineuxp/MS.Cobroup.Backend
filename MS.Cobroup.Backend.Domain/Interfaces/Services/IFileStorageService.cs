namespace MS.Cobroup.Backend.Domain.Interfaces.Services;

/// <summary>
/// Puerto de almacenamiento de archivos binarios agnóstico a la infraestructura (PVC local, S3 futuro).
/// Opera con claves lógicas canónicas relativas y flujos binarios.
/// El dominio permanece libre de rutas físicas, FileInfo, PVC y SDKs de terceros.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Guarda un archivo binario bajo la clave lógica canónica especificada.
    /// Si el archivo ya existe con idéntico tamaño y hash SHA-256, la operación es idempotente y exitosa.
    /// Si existe con contenido diferente, lanza una excepción de integridad sin sobreescribir.
    /// </summary>
    Task<StoredFileResult> PutAsync(
        string logicalKey,
        Stream stream,
        string declaredContentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Abre un flujo de lectura para la clave lógica especificada.
    /// Retorna null si el archivo no existe.
    /// </summary>
    Task<Stream?> OpenReadAsync(
        string logicalKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Elimina el archivo asociado a la clave lógica.
    /// Retorna true si existía y fue eliminado, false si no existía.
    /// </summary>
    Task<bool> DeleteAsync(
        string logicalKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Comprueba la existencia del archivo bajo la clave lógica especificada.
    /// </summary>
    Task<bool> ExistsAsync(
        string logicalKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene los metadatos básicos del archivo sin transferir el contenido completo.
    /// Retorna null si el archivo no existe.
    /// </summary>
    Task<FileMetadataResult?> GetMetadataAsync(
        string logicalKey,
        CancellationToken cancellationToken);

    // ==========================================
    // Fachada de compatibilidad para Evidencias
    // ==========================================
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

public sealed record FileMetadataResult(
    string StorageKey,
    string ContentType,
    long ByteSize,
    DateTimeOffset LastModifiedUtc);
