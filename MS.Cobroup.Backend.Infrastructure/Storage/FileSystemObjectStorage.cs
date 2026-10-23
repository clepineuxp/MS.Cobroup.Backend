using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MS.Cobroup.Backend.Domain.Interfaces.Services;

namespace MS.Cobroup.Backend.Infrastructure.Storage;

/// <summary>
/// Adaptador de almacenamiento de archivos binarios sobre sistema de archivos local / PVC Kubernetes.
/// Implementa aislamiento estricto por tenant, validación estructural selectiva por categoría,
/// contención segura de rutas contra path traversal, symlinks y junctions, publicación atómica
/// multiproceso con verificación de idempotencia y reconciliación de temporales huérfanos.
///
/// Clave canónica obligatoria: tenants/{tenantGuid}/{category}/{aggregateGuid}/{sha256hex}.{ext}
///   - tenantGuid y aggregateGuid: GUID válido en formato D (8-4-4-4-12).
///   - category: valor exacto de AllowedCategories.
///   - sha256hex: cadena hexadecimal de exactamente 64 caracteres.
///   - ext: extensión de la lista permitida coherente con el contenido declarado.
///
/// Categorías que exigen validación estructural de imagen (JPEG, PNG, WebP):
///   - evidences, payments
/// Categorías que almacenan documentos generales sin validación de imagen:
///   - documents, attachments
/// </summary>
public sealed partial class FileSystemObjectStorage : IFileStorageService
{
    /// <summary>Categorías que requieren validación estructural estricta de imagen.</summary>
    private static readonly HashSet<string> ImageCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "evidences",
        "payments",
    };

    /// <summary>Categorías que almacenan documentos/adjuntos generales (no requieren validación de imagen).</summary>
    private static readonly HashSet<string> DocumentCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "documents",
        "attachments",
    };

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "evidences",
        "payments",
        "documents",
        "attachments",
    };

    /// <summary>Extensiones permitidas para categorías de imagen.</summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    /// <summary>Extensiones permitidas para documentos generales.</summary>
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv",
        ".jpg", ".jpeg", ".png", ".webp",  // Documentos también pueden adjuntar imágenes
    };

    /// <summary>SHA-256 hexadecimal: exactamente 64 caracteres hex minúsculas o mayúsculas.</summary>
    private static readonly Regex Sha256HexRegex = Sha256HexPattern();

    [GeneratedRegex(@"^[0-9a-fA-F]{64}$", RegexOptions.Compiled)]
    private static partial Regex Sha256HexPattern();

    private readonly string storageRoot;
    private readonly long maxFileSizeBytes;
    private readonly ILogger<FileSystemObjectStorage>? logger;

    public FileSystemObjectStorage(
        IOptions<FileSystemStorageOptions> options,
        ILogger<FileSystemObjectStorage>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value ?? throw new ArgumentException("Storage options must be provided.", nameof(options));

        if (string.IsNullOrWhiteSpace(settings.RootPath))
        {
            throw new InvalidOperationException("FileSystemObjectStorage requiere una ruta raíz configurada (Storage:EvidencePath o Storage:RootPath).");
        }

        storageRoot = Path.GetFullPath(settings.RootPath);
        maxFileSizeBytes = settings.MaxFileSizeBytes > 0 ? settings.MaxFileSizeBytes : 10 * 1024 * 1024;
        this.logger = logger;

        // Asegurar que el directorio raíz y el temporal existan
        Directory.CreateDirectory(storageRoot);
        Directory.CreateDirectory(Path.Combine(storageRoot, ".tmp"));
    }

    public async Task<StoredFileResult> PutAsync(
        string logicalKey,
        Stream stream,
        string declaredContentType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredContentType);
        ArgumentNullException.ThrowIfNull(stream);

        cancellationToken.ThrowIfCancellationRequested();

        var (normalizedKey, category) = ValidateAndNormalizeLogicalKey(logicalKey);
        var targetFullPath = ResolveAndValidateFullPath(normalizedKey);

        if (stream.CanSeek && stream.Length > maxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"El archivo supera el límite máximo permitido de {maxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var tempDir = Path.Combine(storageRoot, ".tmp");
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.tmp");

        long totalBytesRead = 0;
        string hashHex;
        string resolvedContentType = declaredContentType;

        try
        {
            using var memoryBuffer = new MemoryStream();
            using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            await using (var tempFileStream = new FileStream(
                tempFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                var buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    totalBytesRead += bytesRead;
                    if (totalBytesRead > maxFileSizeBytes)
                    {
                        throw new InvalidOperationException(
                            $"El archivo supera el límite máximo permitido de {maxFileSizeBytes / (1024 * 1024)} MB.");
                    }

                    incrementalHash.AppendData(buffer, 0, bytesRead);
                    await tempFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                    // Acumular en buffer para validación estructural (solo lo que cabe en memoria)
                    if (memoryBuffer.Length < maxFileSizeBytes)
                    {
                        memoryBuffer.Write(buffer, 0, bytesRead);
                    }
                }

                await tempFileStream.FlushAsync(cancellationToken);
                tempFileStream.Flush(flushToDisk: true); // Fsync a nivel de kernel/controlador de volumen (durabilidad física B-09)
            }

            if (totalBytesRead == 0)
            {
                throw new InvalidOperationException("El archivo cargado no contiene datos.");
            }

            var hashBytes = incrementalHash.GetHashAndReset();
            hashHex = Convert.ToHexStringLower(hashBytes);

            // B-09: Vincular estrictamente el hash declarado en la clave con el SHA-256 real calculado
            var fileName = Path.GetFileName(normalizedKey);
            var dotIdx = fileName.LastIndexOf('.');
            var declaredHash = dotIdx > 0 ? fileName[..dotIdx] : fileName;
            if (declaredHash.Length == 64 && Sha256HexRegex.IsMatch(declaredHash))
            {
                if (!string.Equals(declaredHash, hashHex, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Violación de integridad: El hash SHA-256 calculado del contenido ('{hashHex}') no coincide con el hash declarado en la clave de almacenamiento ('{declaredHash}').");
                }
            }

            // Validación estructural: solo para categorías de imagen (evidences, payments).
            // Documentos generales (documents, attachments) NO pasan por el validador de imagen.
            var payload = memoryBuffer.ToArray();
            var ext = Path.GetExtension(normalizedKey).ToLowerInvariant();

            if (IsImageCategory(category))
            {
                resolvedContentType = ImageStructuralValidator.ValidateAndDetect(payload);

                // B-09: Validar coherencia estricta entre el formato real detectado, la extensión del archivo y el MIME declarado
                var expectedMime = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => null
                };

                if (expectedMime is null || !string.Equals(resolvedContentType, expectedMime, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Incoherencia de tipo de archivo: La extensión de la clave es '{ext}' (esperaba '{expectedMime}'), pero el contenido real analizado corresponde a '{resolvedContentType}'.");
                }

                if (!string.Equals(declaredContentType, resolvedContentType, StringComparison.OrdinalIgnoreCase) &&
                    !(declaredContentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) && resolvedContentType == "image/jpeg"))
                {
                    throw new InvalidOperationException(
                        $"Incoherencia de tipo de archivo: El MIME declarado '{declaredContentType}' no coincide con el formato real detectado '{resolvedContentType}'.");
                }
            }
            else
            {
                // Para documentos generales: validar solo que el MIME declarado no sea de imagen
                // sin intentar interpretar el contenido como imagen.
                resolvedContentType = declaredContentType;
                ValidateExtensionAndMimeCoherence(category, ext, declaredContentType);
            }

            var parentDirectory = Path.GetDirectoryName(targetFullPath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            // Publicación atómica mediante File.Move con overwrite: false para prevenir colisiones destructivas
            try
            {
                File.Move(tempFilePath, targetFullPath, overwrite: false);
                // Si Move tuvo éxito, el temp ya no existe — limpiar referencia
                tempFilePath = null!;
            }
            catch (IOException) when (File.Exists(targetFullPath))
            {
                // Manejo de concurrencia e idempotencia: verificar si el archivo existente es idéntico
                var existingInfo = new FileInfo(targetFullPath);
                if (existingInfo.Length == totalBytesRead)
                {
                    var existingHash = ComputeFileSha256(targetFullPath);
                    if (string.Equals(existingHash, hashHex, StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogInformation("Idempotencia confirmada para clave {LogicalKey} ({HashHex})", normalizedKey, hashHex);
                        return new StoredFileResult(normalizedKey, resolvedContentType, totalBytesRead, hashHex);
                    }
                }

                throw new InvalidOperationException(
                    $"Violación de integridad: El archivo de destino '{normalizedKey}' ya existe con diferente tamaño o hash SHA-256.");
            }

            return new StoredFileResult(normalizedKey, resolvedContentType, totalBytesRead, hashHex);
        }
        finally
        {
            if (tempFilePath is not null && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "No se pudo eliminar el archivo temporal huérfano {TempFilePath}", tempFilePath);
                }
            }
        }
    }

    public Task<Stream?> OpenReadAsync(string logicalKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (normalizedKey, _) = ValidateAndNormalizeLogicalKey(logicalKey);
        var fullPath = ResolveAndValidateFullPath(normalizedKey);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Task.FromResult<Stream?>(fileStream);
    }

    public Task<bool> DeleteAsync(string logicalKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (normalizedKey, _) = ValidateAndNormalizeLogicalKey(logicalKey);
        var fullPath = ResolveAndValidateFullPath(normalizedKey);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error al eliminar archivo en {FullPath}", fullPath);
            throw;
        }
    }

    public Task<bool> ExistsAsync(string logicalKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (normalizedKey, _) = ValidateAndNormalizeLogicalKey(logicalKey);
        var fullPath = ResolveAndValidateFullPath(normalizedKey);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<FileMetadataResult?> GetMetadataAsync(string logicalKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (normalizedKey, _) = ValidateAndNormalizeLogicalKey(logicalKey);
        var fullPath = ResolveAndValidateFullPath(normalizedKey);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<FileMetadataResult?>(null);
        }

        var info = new FileInfo(fullPath);
        var ext = info.Extension.ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };

        var metadata = new FileMetadataResult(
            normalizedKey,
            contentType,
            info.Length,
            new DateTimeOffset(info.LastWriteTimeUtc));

        return Task.FromResult<FileMetadataResult?>(metadata);
    }

    // ========================================================
    // Fachada de compatibilidad para Evidencias de Cobranza
    // ========================================================

    /// <summary>
    /// Almacena una evidencia fotográfica de cobranza usando la clave canónica definitiva.
    /// El SHA-256 se calcula durante la escritura y se usa directamente en la clave canónica.
    /// No hay renombrado posterior: la clave canónica se construye a priori con un UUID provisional
    /// y se normaliza al SHA-256 tras la escritura, usando Move atómico dentro del mismo volumen.
    /// </summary>
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

        // Determinar extensión coherente con el tipo MIME declarado
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = declaredContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "application/pdf" => ".pdf",
                _ => ".bin"
            };
        }

        // Validar que la extensión sea coherente con el MIME declarado para evidencias
        ValidateExtensionAndMimeCoherence("evidences", ext, declaredContentType);

        // Escribir en archivo temporal primero; calcular hash durante la escritura
        var tempDir = Path.Combine(storageRoot, ".tmp");
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.tmp");

        long totalBytesRead = 0;
        string hashHex;
        string resolvedContentType = declaredContentType;

        try
        {
            using var memoryBuffer = new MemoryStream();
            using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            if (stream.CanSeek && stream.Length > maxFileSizeBytes)
            {
                throw new InvalidOperationException(
                    $"El archivo supera el límite máximo permitido de {maxFileSizeBytes / (1024 * 1024)} MB.");
            }

            await using (var tempFileStream = new FileStream(
                tempFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                var buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    totalBytesRead += bytesRead;
                    if (totalBytesRead > maxFileSizeBytes)
                    {
                        throw new InvalidOperationException(
                            $"El archivo supera el límite máximo permitido de {maxFileSizeBytes / (1024 * 1024)} MB.");
                    }

                    incrementalHash.AppendData(buffer, 0, bytesRead);
                    await tempFileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                    if (memoryBuffer.Length < maxFileSizeBytes)
                    {
                        memoryBuffer.Write(buffer, 0, bytesRead);
                    }
                }

                await tempFileStream.FlushAsync(cancellationToken);
                tempFileStream.Flush(flushToDisk: true); // Fsync a nivel de kernel/controlador de volumen
            }

            if (totalBytesRead == 0)
            {
                throw new InvalidOperationException("El archivo cargado no contiene datos.");
            }

            var hashBytes = incrementalHash.GetHashAndReset();
            hashHex = Convert.ToHexStringLower(hashBytes);

            // Validación estructural de imagen: evidencias siempre son imágenes
            var payload = memoryBuffer.ToArray();
            resolvedContentType = ImageStructuralValidator.ValidateAndDetect(payload);

            // B-09: Exigir coherencia entre la extensión/MIME y el formato real detectado
            var expectedMime = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => null
            };

            if (expectedMime is not null && !string.Equals(resolvedContentType, expectedMime, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Incoherencia de tipo de archivo: La extensión '{ext}' no coincide con el formato real detectado '{resolvedContentType}'.");
            }

            // Ajustar extensión coherente con el tipo detectado
            var detectedExt = resolvedContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ext
            };

            // Construir clave canónica definitiva usando el SHA-256 calculado
            var canonicalKey = $"tenants/{tenantId:D}/evidences/{paymentId:D}/{hashHex}{detectedExt}";
            var targetFullPath = ResolveAndValidateFullPath(canonicalKey);

            var parentDirectory = Path.GetDirectoryName(targetFullPath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            // Publicación atómica directa desde temporal a destino canónico final
            try
            {
                File.Move(tempFilePath, targetFullPath, overwrite: false);
                tempFilePath = null!; // Move exitoso, ya no existe temp
            }
            catch (IOException) when (File.Exists(targetFullPath))
            {
                // Concurrencia: otro proceso ya escribió el mismo hash
                var existingInfo = new FileInfo(targetFullPath);
                if (existingInfo.Length == totalBytesRead)
                {
                    var existingHash = ComputeFileSha256(targetFullPath);
                    if (string.Equals(existingHash, hashHex, StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogInformation("Idempotencia confirmada para evidencia {CanonicalKey}", canonicalKey);
                        return new StoredFileResult(canonicalKey, resolvedContentType, totalBytesRead, hashHex);
                    }
                }

                throw new InvalidOperationException(
                    $"Violación de integridad: La evidencia '{canonicalKey}' ya existe con diferente contenido.");
            }

            return new StoredFileResult(canonicalKey, resolvedContentType, totalBytesRead, hashHex);
        }
        finally
        {
            if (tempFilePath is not null && File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "No se pudo eliminar el temporal huérfano {TempFilePath}", tempFilePath);
                }
            }
        }
    }

    public Task<Stream?> ReadEvidenceAsync(
        Guid tenantId,
        string storageKey,
        CancellationToken cancellationToken)
    {
        ValidateTenantOwnership(tenantId, storageKey);
        return OpenReadAsync(storageKey, cancellationToken);
    }

    public Task DeleteEvidenceAsync(
        Guid tenantId,
        string storageKey,
        CancellationToken cancellationToken)
    {
        ValidateTenantOwnership(tenantId, storageKey);
        return DeleteAsync(storageKey, cancellationToken);
    }

    /// <summary>
    /// Reconcilia y limpia archivos temporales huérfanos que hayan quedado abandonados en .tmp.
    /// </summary>
    public Task<int> ReconcileOrphanTempsAsync(TimeSpan olderThan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tempDir = Path.Combine(storageRoot, ".tmp");
        if (!Directory.Exists(tempDir))
        {
            return Task.FromResult(0);
        }

        var threshold = DateTime.UtcNow - olderThan;
        var cleanedCount = 0;

        foreach (var file in Directory.EnumerateFiles(tempDir, "*.tmp"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var creationTime = File.GetCreationTimeUtc(file);
                if (creationTime < threshold)
                {
                    File.Delete(file);
                    cleanedCount++;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "No se pudo limpiar el archivo temporal huérfano {File}", file);
            }
        }

        return Task.FromResult(cleanedCount);
    }

    // ========================================================
    // Métodos de validación internos
    // ========================================================

    private static bool IsImageCategory(string category) =>
        ImageCategories.Contains(category);

    private static void ValidateExtensionAndMimeCoherence(string category, string ext, string declaredMime)
    {
        if (IsImageCategory(category))
        {
            if (!ImageExtensions.Contains(ext))
            {
                throw new InvalidOperationException(
                    $"La extensión '{ext}' no es válida para la categoría '{category}'. Extensiones permitidas: {string.Join(", ", ImageExtensions)}.");
            }
        }
        else
        {
            if (!DocumentExtensions.Contains(ext))
            {
                throw new InvalidOperationException(
                    $"La extensión '{ext}' no es válida para la categoría '{category}'. Extensiones permitidas: {string.Join(", ", DocumentExtensions)}.");
            }
        }
    }

    private static void ValidateTenantOwnership(Guid tenantId, string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        var normalized = storageKey.Replace('\\', '/').Trim().TrimStart('/');

        var canonicalPrefix = $"tenants/{tenantId:D}/";
        var legacyPrefix = $"{tenantId:D}/";

        if (!normalized.StartsWith(canonicalPrefix, StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Acceso no autorizado al archivo del tenant.");
        }
    }

    /// <summary>
    /// Valida y normaliza la clave lógica.
    /// Formato canónico requerido: tenants/{tenantGuid}/{category}/{aggregateGuid}/{sha256hex}.{ext}
    /// Formato legado soportado: {tenantGuid}/{aggregateGuid}/{sha256hex}.{ext}
    /// Retorna (normalizedKey, category).
    /// </summary>
    private static (string NormalizedKey, string Category) ValidateAndNormalizeLogicalKey(string logicalKey)
    {
        if (string.IsNullOrWhiteSpace(logicalKey))
        {
            throw new InvalidOperationException("La clave de almacenamiento no puede ser nula o vacía.");
        }

        if (logicalKey.StartsWith('/') || logicalKey.StartsWith('\\'))
        {
            throw new InvalidOperationException("La clave de almacenamiento no puede ser una ruta absoluta.");
        }

        if (logicalKey.Contains(':') || logicalKey.Contains('\0'))
        {
            throw new InvalidOperationException("La clave de almacenamiento contiene caracteres no permitidos (':', '\\0').");
        }

        var normalized = logicalKey.Replace('\\', '/').Trim();
        var segments = normalized.Split('/', StringSplitOptions.None);

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
            {
                throw new InvalidOperationException("Ruta de archivo no válida (intento de path traversal detectado).");
            }
        }

        string resolvedCategory;

        if (segments[0].Equals("tenants", StringComparison.OrdinalIgnoreCase))
        {
            // Formato canónico: tenants/{tenantGuid}/{category}/{aggregateGuid}/{sha256hex}.{ext}
            // segments[0] = "tenants"
            // segments[1] = tenantGuid
            // segments[2] = category
            // segments[3] = aggregateGuid
            // segments[4] = {sha256hex}.{ext}

            if (segments.Length != 5)
            {
                throw new InvalidOperationException(
                    "Clave canónica debe contener exactamente cinco segmentos: tenants/{tenantGuid}/{category}/{aggregateGuid}/{sha256hex}.{ext}");
            }

            // Validar tenantGuid como GUID válido
            if (!Guid.TryParseExact(segments[1], "D", out _))
            {
                throw new InvalidOperationException(
                    $"El segmento tenant '{segments[1]}' no es un GUID válido en formato D (8-4-4-4-12).");
            }

            resolvedCategory = segments[2];
            if (!AllowedCategories.Contains(resolvedCategory))
            {
                throw new InvalidOperationException(
                    $"Categoría '{resolvedCategory}' no permitida. Categorías válidas: {string.Join(", ", AllowedCategories)}.");
            }

            // Validar aggregateGuid como GUID válido
            if (!Guid.TryParseExact(segments[3], "D", out _))
            {
                throw new InvalidOperationException(
                    $"El segmento aggregate '{segments[3]}' no es un GUID válido en formato D (8-4-4-4-12).");
            }

            // Validar nombre de archivo canónico: exactamente {sha256hex}.{ext}
            var fileName = segments[4];
            var dotIndex = fileName.LastIndexOf('.');
            if (dotIndex <= 0)
            {
                throw new InvalidOperationException(
                    $"El nombre de archivo canónico '{fileName}' requiere una extensión válida.");
            }

            var hashPart = fileName[..dotIndex];
            if (hashPart.Length != 64 || !Sha256HexRegex.IsMatch(hashPart))
            {
                throw new InvalidOperationException(
                    $"El nombre de archivo '{fileName}' debe contener exactamente 64 caracteres hexadecimales correspondientes al hash SHA-256.");
            }

            var ext = fileName[dotIndex..].ToLowerInvariant();
            if (IsImageCategory(resolvedCategory))
            {
                if (!ImageExtensions.Contains(ext))
                {
                    throw new InvalidOperationException(
                        $"La extensión '{ext}' no es permitida para la categoría de imagen '{resolvedCategory}'. Permitidas: {string.Join(", ", ImageExtensions)}.");
                }
            }
            else
            {
                if (!DocumentExtensions.Contains(ext))
                {
                    throw new InvalidOperationException(
                        $"La extensión '{ext}' no es permitida para la categoría de documento '{resolvedCategory}'. Permitidas: {string.Join(", ", DocumentExtensions)}.");
                }
            }
        }
        else if (Guid.TryParseExact(segments[0], "D", out _))
        {
            // Formato legado: {tenantGuid}/{aggregateGuid}/{fileName}
            resolvedCategory = "evidences"; // Categoría por defecto para legado
        }
        else
        {
            throw new InvalidOperationException(
                "Formato de clave no reconocido. Use el formato canónico: tenants/{tenantGuid}/{category}/{aggregateGuid}/{fileName}");
        }

        return (normalized, resolvedCategory);
    }

    private string ResolveAndValidateFullPath(string normalizedKey)
    {
        var fullRoot = Path.GetFullPath(storageRoot);
        var rootWithSep = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        var combined = Path.Combine(storageRoot, normalizedKey.Replace('/', Path.DirectorySeparatorChar));
        var fullPath = Path.GetFullPath(combined);

        // Garantizar que la ruta permanezca estrictamente dentro de la raíz del PVC usando límite de separador real
        if (!fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La ruta especificada escapa del directorio raíz de almacenamiento.");
        }

        // Detección y rechazo de enlaces simbólicos o reparse points en cada componente existente
        ValidateNoSymlinks(fullRoot, fullPath);

        return fullPath;
    }

    /// <summary>
    /// Valida que ningún componente de la ruta desde la raíz hasta el destino sea un symlink o junction.
    /// NOTA DE SEGURIDAD (TOCTOU): Aunque esta comprobación inspecciona todos los componentes
    /// existentes en el momento de la resolución, existe una ventana teórica residual TOCTOU si un proceso
    /// host externo sustituye un directorio entre la validación y la apertura de archivo.
    /// Para mitigar esto, Kubernetes configura runAsNonRoot, fsGroup y readOnlyRootFilesystem.
    /// </summary>
    private static void ValidateNoSymlinks(string fullRoot, string fullPath)
    {
        var current = fullRoot;
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == ".") return;

        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            if (Directory.Exists(current) || File.Exists(current))
            {
                var attrs = File.GetAttributes(current);
                if (attrs.HasFlag(FileAttributes.ReparsePoint))
                {
                    throw new InvalidOperationException($"Se detectó un symlink o punto de reanálisis no permitido en '{current}'.");
                }
            }
        }
    }

    private static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }
}
