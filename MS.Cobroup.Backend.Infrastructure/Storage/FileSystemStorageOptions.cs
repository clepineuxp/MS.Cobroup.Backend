namespace MS.Cobroup.Backend.Infrastructure.Storage;

public sealed class FileSystemStorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "FileSystem";

    /// <summary>
    /// Directorio raíz del almacenamiento persistente (montaje del PVC en Kubernetes).
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Límite máximo en bytes permitido para archivos cargados (por defecto 10 MB).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
