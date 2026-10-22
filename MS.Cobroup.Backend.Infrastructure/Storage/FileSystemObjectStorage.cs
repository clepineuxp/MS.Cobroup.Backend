using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MS.Cobroup.Backend.Domain.Interfaces.Services;

namespace MS.Cobroup.Backend.Infrastructure.Storage;

public sealed class FileSystemObjectStorage : IFileStorageService
{
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffHeader = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpHeader = [0x57, 0x45, 0x42, 0x50];

    private readonly string storageRoot;
    private readonly long maxFileSizeBytes;

    public FileSystemObjectStorage(IOptions<FileSystemStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value ?? throw new ArgumentException("Storage options must be provided.", nameof(options));

        if (string.IsNullOrWhiteSpace(settings.RootPath))
        {
            throw new InvalidOperationException("FileSystemObjectStorage requiere una ruta raíz configurada (Storage:EvidencePath o Storage:RootPath).");
        }

        storageRoot = Path.GetFullPath(settings.RootPath);
        maxFileSizeBytes = settings.MaxFileSizeBytes > 0 ? settings.MaxFileSizeBytes : 10 * 1024 * 1024;
    }

    public async Task<StoredFileResult> StoreEvidenceAsync(
        Guid tenantId,
        Guid paymentId,
        string fileName,
        string declaredContentType,
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredContentType);
        ArgumentNullException.ThrowIfNull(stream);

        var tempDir = Path.Combine(storageRoot, ".tmp");
        Directory.CreateDirectory(tempDir);

        var tempFileName = $"{Guid.NewGuid():N}.tmp";
        var tempFilePath = Path.Combine(tempDir, tempFileName);

        string detectedType;
        long totalBytesRead = 0;
        string hashHex;

        try
        {
            using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var tempFileStream = new FileStream(
                tempFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                var buffer = new byte[81920];
                var firstChunk = true;
                detectedType = string.Empty;

                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    totalBytesRead += bytesRead;
                    if (totalBytesRead > maxFileSizeBytes)
                    {
                        throw new InvalidOperationException(
                            $"El archivo de evidencia supera el límite máximo permitido de {maxFileSizeBytes / (1024 * 1024)} MB.");
                    }

                    if (firstChunk)
                    {
                        var detected = DetectContentType(buffer.AsSpan(0, bytesRead));
                        if (detected is null)
                        {
                            throw new InvalidOperationException(
                                "El archivo cargado no corresponde a un formato de imagen permitido (JPEG, PNG o WebP).");
                        }
                        detectedType = detected;
                        firstChunk = false;
                    }

                    incrementalHash.AppendData(buffer, 0, bytesRead);
                    await tempFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                }

                if (totalBytesRead == 0)
                {
                    throw new InvalidOperationException("El archivo de evidencia no contiene datos.");
                }
            }

            var hashBytes = incrementalHash.GetHashAndReset();
            hashHex = Convert.ToHexStringLower(hashBytes);

            var extension = detectedType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".bin"
            };

            var tenantFolder = Path.Combine(storageRoot, tenantId.ToString("D"), paymentId.ToString("D"));
            Directory.CreateDirectory(tenantFolder);

            var targetFileName = $"{hashHex}{extension}";
            var fullPath = Path.Combine(tenantFolder, targetFileName);

            // Operación atómica de publicación en el mismo volumen persistente (PVC)
            File.Move(tempFilePath, fullPath, overwrite: true);

            var relativeStorageKey = $"{tenantId:D}/{paymentId:D}/{targetFileName}";

            return new StoredFileResult(
                relativeStorageKey,
                detectedType,
                totalBytesRead,
                hashHex);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Limpieza garantizada; ignorar errores si el archivo ya fue movido exitosamente
                }
            }
        }
    }

    public Task<Stream?> ReadEvidenceAsync(
        Guid tenantId,
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveAndValidatePath(tenantId, storageKey);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Task.FromResult<Stream?>(fileStream);
    }

    public Task DeleteEvidenceAsync(
        Guid tenantId,
        string storageKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolveAndValidatePath(tenantId, storageKey);

        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
            }
            catch
            {
                // Ignorar errores de concurrencia en borrado
            }
        }

        return Task.CompletedTask;
    }

    private string ResolveAndValidatePath(Guid tenantId, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new InvalidOperationException("La clave de almacenamiento no puede ser nula o vacía.");
        }

        var normalized = storageKey.Replace('\\', '/').Trim();
        if (normalized.StartsWith('/'))
        {
            throw new InvalidOperationException("La clave de almacenamiento no puede ser una ruta absoluta.");
        }

        if (normalized.Contains("..") || normalized.Contains(':') || normalized.Contains('\0'))
        {
            throw new InvalidOperationException("Ruta de archivo no válida (intento de path traversal detectado).");
        }

        var expectedTenantPrefix = $"{tenantId:D}/";
        if (!normalized.StartsWith(expectedTenantPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Acceso no autorizado al archivo del tenant.");
        }

        var combined = Path.Combine(storageRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        var fullPath = Path.GetFullPath(combined);
        var fullRoot = Path.GetFullPath(storageRoot);

        // Garantizar que la ruta resuelta permanezca estrictamente dentro del root del PVC
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La ruta especificada escapa del directorio raíz de almacenamiento.");
        }

        return fullPath;
    }

    private static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.Length >= JpegHeader.Length && data[..JpegHeader.Length].SequenceEqual(JpegHeader))
        {
            return "image/jpeg";
        }

        if (data.Length >= PngHeader.Length && data[..PngHeader.Length].SequenceEqual(PngHeader))
        {
            return "image/png";
        }

        if (data.Length >= 12 && data[..4].SequenceEqual(RiffHeader) && data.Slice(8, 4).SequenceEqual(WebpHeader))
        {
            return "image/webp";
        }

        return null;
    }
}
