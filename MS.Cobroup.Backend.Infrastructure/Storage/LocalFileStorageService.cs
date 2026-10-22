using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using MS.Cobroup.Backend.Domain.Interfaces.Services;

namespace MS.Cobroup.Backend.Infrastructure.Storage;

public sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffHeader = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpHeader = [0x57, 0x45, 0x42, 0x50];

    private readonly string storageRoot = configuration["Storage:EvidencePath"]
        ?? Path.Combine(AppContext.BaseDirectory, "storage", "evidences");

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

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("El archivo de evidencia no contiene datos.");
        }

        if (bytes.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"El archivo de evidencia supera el límite máximo permitido de {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var detectedType = DetectContentType(bytes);
        if (detectedType is null)
        {
            throw new InvalidOperationException("El archivo cargado no corresponde a un formato de imagen permitido (JPEG, PNG o WebP).");
        }

        var extension = detectedType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin"
        };

        var hashBytes = SHA256.HashData(bytes);
        var hashHex = Convert.ToHexStringLower(hashBytes);

        var tenantFolder = Path.Combine(storageRoot, tenantId.ToString("D"), paymentId.ToString("D"));
        Directory.CreateDirectory(tenantFolder);

        var targetFileName = $"{hashHex}{extension}";
        var fullPath = Path.Combine(tenantFolder, targetFileName);

        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);

        var relativeStoragePath = Path.Combine(tenantId.ToString("D"), paymentId.ToString("D"), targetFileName).Replace('\\', '/');

        return new StoredFileResult(
            relativeStoragePath,
            detectedType,
            bytes.Length,
            hashHex);
    }

    public Task<Stream?> ReadEvidenceAsync(
        Guid tenantId,
        string storagePath,
        CancellationToken cancellationToken)
    {
        var sanitized = storagePath.Replace('\\', '/').TrimStart('/');
        // Prevent path traversal
        if (sanitized.Contains(".."))
        {
            throw new InvalidOperationException("Ruta de archivo no válida.");
        }

        var expectedTenantPrefix = tenantId.ToString("D");
        if (!sanitized.StartsWith(expectedTenantPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Acceso no autorizado al archivo del tenant.");
        }

        var fullPath = Path.Combine(storageRoot, sanitized);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = File.OpenRead(fullPath);
        return Task.FromResult<Stream?>(fileStream);
    }

    private static string? DetectContentType(byte[] data)
    {
        if (data.Length >= JpegHeader.Length && data.AsSpan(0, JpegHeader.Length).SequenceEqual(JpegHeader))
        {
            return "image/jpeg";
        }

        if (data.Length >= PngHeader.Length && data.AsSpan(0, PngHeader.Length).SequenceEqual(PngHeader))
        {
            return "image/png";
        }

        if (data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual(RiffHeader) && data.AsSpan(8, 4).SequenceEqual(WebpHeader))
        {
            return "image/webp";
        }

        return null;
    }
}
