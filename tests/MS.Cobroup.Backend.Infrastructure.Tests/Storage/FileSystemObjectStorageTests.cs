using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MS.Cobroup.Backend.Domain.Interfaces.Services;
using MS.Cobroup.Backend.Infrastructure.Storage;
using Xunit;

namespace MS.Cobroup.Backend.Infrastructure.Tests.Storage;

public sealed class FileSystemObjectStorageTests : IDisposable
{
    private readonly string testRoot;

    public FileSystemObjectStorageTests()
    {
        testRoot = Path.Combine(Path.GetTempPath(), "cobroup_storage_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(testRoot))
        {
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch
            {
                // Cleanup best-effort
            }
        }
    }

    private FileSystemObjectStorage CreateStorage(long maxBytes = 10 * 1024 * 1024)
    {
        var options = Options.Create(new FileSystemStorageOptions
        {
            Provider = "FileSystem",
            RootPath = testRoot,
            MaxFileSizeBytes = maxBytes
        });
        return new FileSystemObjectStorage(options);
    }

    private static byte[] CreateMinimalValidJpeg(int width = 100, int height = 100)
    {
        using var ms = new MemoryStream();
        ms.Write([0xFF, 0xD8]); // SOI
        ms.Write([0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]); // APP0
        var hHigh = (byte)(height >> 8);
        var hLow = (byte)(height & 0xFF);
        var wHigh = (byte)(width >> 8);
        var wLow = (byte)(width & 0xFF);
        ms.Write([0xFF, 0xC0, 0x00, 0x11, 0x08, hHigh, hLow, wHigh, wLow, 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01]); // SOF0
        ms.Write([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]); // SOS
        ms.Write([0x12, 0x34, 0x56, 0x78]); // dummy image payload
        ms.Write([0xFF, 0xD9]); // EOI
        return ms.ToArray();
    }

    private static void WritePngChunk(MemoryStream ms, string type, byte[] data)
    {
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)data.Length);
        ms.Write(lenBytes);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        ms.Write(typeBytes);
        ms.Write(data);

        var toCrc = new byte[4 + data.Length];
        Array.Copy(typeBytes, 0, toCrc, 0, 4);
        Array.Copy(data, 0, toCrc, 4, data.Length);
        var crc = ImageStructuralValidator.ComputeCrc32(toCrc);

        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        ms.Write(crcBytes);
    }

    private static byte[] CreateMinimalValidPng(int width = 100, int height = 100)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]); // Signature

        // IHDR
        var ihdrData = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(4, 4), (uint)height);
        ihdrData[8] = 8; // 8 bit depth
        ihdrData[9] = 2; // RGB truecolor
        ihdrData[10] = 0; // deflate
        ihdrData[11] = 0; // filter
        ihdrData[12] = 0; // no interlace
        WritePngChunk(ms, "IHDR", ihdrData);

        // IDAT
        WritePngChunk(ms, "IDAT", [0x78, 0x01, 0x01, 0x01, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x01]);

        // IEND
        WritePngChunk(ms, "IEND", []);

        return ms.ToArray();
    }

    private static byte[] CreateMinimalValidWebp(int width = 100, int height = 100)
    {
        using var ms = new MemoryStream();
        ms.Write("RIFF"u8);
        var payloadLength = 4 + 8 + 10; // 'WEBP' + 'VP8 ' + 10 + 10 bytes payload
        var lenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(lenBytes, (uint)payloadLength);
        ms.Write(lenBytes);
        ms.Write("WEBP"u8);
        ms.Write("VP8 "u8);
        var chunkLenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(chunkLenBytes, 10);
        ms.Write(chunkLenBytes);
        var wLE = (ushort)(width & 0x3FFF);
        var hLE = (ushort)(height & 0x3FFF);
        ms.Write([0x00, 0x00, 0x00, 0x9D, 0x01, 0x2A]);
        var wBytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(wBytes, wLE);
        ms.Write(wBytes);
        var hBytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(hBytes, hLE);
        ms.Write(hBytes);
        return ms.ToArray();
    }

    // =====================================================================
    // B-09: Claves canónicas válidas e inválidas
    // =====================================================================

    [Fact]
    public async Task CanonicalKey_ValidGuidComponents_Accepted()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();
        var payload = new byte[32];
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

        // PutAsync acepta clave canónica bien formada con hash coincidente
        var key = $"tenants/{tenantId:D}/documents/{aggregateId:D}/{sha256}.pdf";
        using var ms = new MemoryStream(payload);
        var result = await storage.PutAsync(key, ms, "application/pdf", CancellationToken.None);
        Assert.Equal(key, result.StorageKey);
    }

    [Fact]
    public async Task CanonicalKey_HashMismatchWithPayload_IsRejected()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();
        var payload = new byte[32];
        var fakeHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        var key = $"tenants/{tenantId:D}/documents/{aggregateId:D}/{fakeHash}.pdf";
        using var ms = new MemoryStream(payload);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.PutAsync(key, ms, "application/pdf", CancellationToken.None));

        Assert.Contains("no coincide con el hash declarado", ex.Message);
    }

    [Theory]
    [InlineData("tenants/not-a-guid/evidences/00000000-0000-0000-0000-000000000001/file.jpg")]
    [InlineData("tenants/00000000-0000-0000-0000-000000000001/evidences/not-a-guid/file.jpg")]
    [InlineData("tenants/00000000-0000-0000-0000-000000000001/unauthorized_category/00000000-0000-0000-0000-000000000001/file.jpg")]
    [InlineData("tenants/00000000-0000-0000-0000-000000000001")] // incompleto
    public async Task CanonicalKey_InvalidGuidOrCategory_Rejected(string badKey)
    {
        var storage = CreateStorage();
        var payload = new byte[32];
        using var ms = new MemoryStream(payload);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.PutAsync(badKey, ms, "application/octet-stream", CancellationToken.None));
    }

    // =====================================================================
    // B-09: Path traversal, symlinks (platform)
    // =====================================================================

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32")]
    [InlineData("/var/cobroup/evidences/file.jpg")]
    [InlineData("C:\\secrets\\key.pem")]
    [InlineData("bad:stream:file.jpg")]
    [InlineData("tenants/unauthorized_category/123/file.jpg")]
    public async Task Security_PathTraversalOrInvalidCategory_ThrowsInvalidOperationException(string maliciousKey)
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.ReadEvidenceAsync(tenantId, maliciousKey, CancellationToken.None));
    }

    [Fact]
    public async Task Security_RootSiblingPrefixDirectoryEscape_ThrowsInvalidOperationException()
    {
        var storage = CreateStorage();
        var maliciousKey = "tenants/../" + Path.GetFileName(testRoot) + "-evil/file.jpg";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.ExistsAsync(maliciousKey, CancellationToken.None));
    }

    // =====================================================================
    // B-09: Colisión con mismo hash (idempotencia)
    // =====================================================================

    [Fact]
    public async Task PutAsync_IdempotentDuplicate_ReturnsSuccessWithoutOverwrite()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var payload = CreateMinimalValidJpeg(100, 100);
        var hashHex = Convert.ToHexStringLower(SHA256.HashData(payload));
        var canonicalKey = $"tenants/{tenantId:D}/evidences/{paymentId:D}/{hashHex}.jpg";

        using (var ms1 = new MemoryStream(payload))
        {
            var res1 = await storage.PutAsync(canonicalKey, ms1, "image/jpeg", CancellationToken.None);
            Assert.Equal(canonicalKey, res1.StorageKey);
        }

        // Segunda carga idéntica (idempotencia)
        using (var ms2 = new MemoryStream(payload))
        {
            var res2 = await storage.PutAsync(canonicalKey, ms2, "image/jpeg", CancellationToken.None);
            Assert.Equal(canonicalKey, res2.StorageKey);
            Assert.Equal(payload.Length, res2.ByteSize);
        }
    }

    // =====================================================================
    // B-09: Colisión con distinto contenido (conflicto de integridad)
    // =====================================================================

    [Fact]
    public async Task PutAsync_IntegrityConflictDifferentContent_ThrowsInvalidOperationException()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var payload1 = CreateMinimalValidJpeg(100, 100);
        var hashHex1 = Convert.ToHexStringLower(SHA256.HashData(payload1));
        var canonicalKey = $"tenants/{tenantId:D}/evidences/{paymentId:D}/{hashHex1}.jpg";

        using (var ms1 = new MemoryStream(payload1))
        {
            await storage.PutAsync(canonicalKey, ms1, "image/jpeg", CancellationToken.None);
        }

        // Carga diferente bajo la misma clave
        var payload2 = CreateMinimalValidJpeg(200, 200);
        using (var ms2 = new MemoryStream(payload2))
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                storage.PutAsync(canonicalKey, ms2, "image/jpeg", CancellationToken.None));

            Assert.Contains("Violación de integridad", ex.Message);
        }
    }

    // =====================================================================
    // B-09: Dos escrituras concurrentes del mismo objeto
    // =====================================================================

    [Fact]
    public async Task ConcurrentPut_SamePayload_OnlyOneCopyAndNoException()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var payload = CreateMinimalValidJpeg(100, 100);
        var hashHex = Convert.ToHexStringLower(SHA256.HashData(payload));
        var canonicalKey = $"tenants/{tenantId:D}/evidences/{paymentId:D}/{hashHex}.jpg";

        // Dos tareas concurrentes intentando escribir el mismo archivo
        var task1 = storage.PutAsync(canonicalKey, new MemoryStream(payload), "image/jpeg", CancellationToken.None);
        var task2 = storage.PutAsync(canonicalKey, new MemoryStream(payload), "image/jpeg", CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        // Ambas deben tener éxito (idempotencia concurrente)
        Assert.Equal(canonicalKey, results[0].StorageKey);
        Assert.Equal(canonicalKey, results[1].StorageKey);

        // Solo debe existir exactamente una copia
        var parentDir = Path.Combine(testRoot, "tenants", tenantId.ToString("D"), "evidences", paymentId.ToString("D"));
        var files = Directory.GetFiles(parentDir);
        Assert.Single(files);
    }

    // =====================================================================
    // B-09: Archivos truncados y polyglots
    // =====================================================================

    [Fact]
    public async Task TruncatedJpeg_WithoutEoiMarker_IsRejected()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var valid = CreateMinimalValidJpeg(100, 100);
        // Truncar los últimos 2 bytes (remover EOI FF D9)
        var truncated = valid[..^2];
        using var ms = new MemoryStream(truncated);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "trunc.jpg", "image/jpeg", ms, CancellationToken.None));

        Assert.Contains("truncado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PolyglotFile_PngHeaderWithJpegContent_IsRejectedOrDetectedByRealFormat()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        // Archivo con cabecera PNG pero resto de contenido incoherente (polyglot simulado)
        var pngSig = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var jpegContent = CreateMinimalValidJpeg(100, 100);
        var polyglot = pngSig.Concat(jpegContent).ToArray();

        using var ms = new MemoryStream(polyglot);

        // Debe ser rechazado porque la estructura PNG está incompleta o malformada
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "polyglot.png", "image/png", ms, CancellationToken.None));

        Assert.NotNull(ex); // El validador debe rechazarlo
    }

    [Fact]
    public async Task PutAsync_MimeExtensionMismatch_PngContentWithJpgExtension_IsRejected()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        // Contenido real PNG pero clave con extensión .jpg
        var pngPayload = CreateMinimalValidPng(100, 100);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(pngPayload));
        var mismatchedKey = $"tenants/{tenantId:D}/evidences/{paymentId:D}/{sha256}.jpg";

        using var ms = new MemoryStream(pngPayload);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.PutAsync(mismatchedKey, ms, "image/jpeg", CancellationToken.None));

        Assert.Contains("Incoherencia de tipo de archivo", ex.Message);
    }

    [Fact]
    public async Task Polyglot_PngWithTrailingBytesAfterIend_IsRejected()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var validPng = CreateMinimalValidPng(100, 100);
        var polyglot = validPng.Concat("MALICIOUS_POLYGLOT_PAYLOAD"u8.ToArray()).ToArray();

        using var ms = new MemoryStream(polyglot);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "poly.png", "image/png", ms, CancellationToken.None));

        Assert.Contains("polyglot", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Polyglot_JpegWithTrailingBytesAfterEoi_IsRejected()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var validJpeg = CreateMinimalValidJpeg(100, 100);
        var polyglot = validJpeg.Concat("EXTRA_UNAUTHORIZED_BYTES_AFTER_EOI"u8.ToArray()).ToArray();

        using var ms = new MemoryStream(polyglot);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "poly.jpg", "image/jpeg", ms, CancellationToken.None));

        Assert.Contains("polyglot", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JpegWithoutSofMarker_SmallOrLarge_IsRejected()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        // JPEG con SOI (FF D8), APP0, SOS, EOI pero SIN segmento SOF
        using var msJpeg = new MemoryStream();
        msJpeg.Write([0xFF, 0xD8]); // SOI
        msJpeg.Write([0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]); // APP0
        msJpeg.Write([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]); // SOS
        msJpeg.Write([0x12, 0x34]); // scan data
        msJpeg.Write([0xFF, 0xD9]); // EOI
        var noSofBytes = msJpeg.ToArray();

        using var ms = new MemoryStream(noSofBytes);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "nosof.jpg", "image/jpeg", ms, CancellationToken.None));

        Assert.Contains("SOF", ex.Message);
    }

    // =====================================================================
    // B-09: Imágenes con dimensiones excesivas (decompression bomb)
    // =====================================================================

    [Fact]
    public async Task DecompressionBomb_ImageExceeding8192Pixels_IsRejected()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var bombPayload = CreateMinimalValidPng(10000, 10000);
        using var ms = new MemoryStream(bombPayload);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "bomb.png", "image/png", ms, CancellationToken.None));

        Assert.Contains("decompression bomb", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =====================================================================
    // B-09: PNG con chunks inválidos
    // =====================================================================

    [Fact]
    public async Task PngWithMissingIendChunk_IsRejected()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        // PNG sin chunk IEND al final
        var validPng = CreateMinimalValidPng(100, 100);
        // Truncar los últimos 12 bytes (IEND chunk: 4 len + 4 type + 4 CRC)
        var truncatedPng = validPng[..^12];

        using var ms = new MemoryStream(truncatedPng);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "noend.png", "image/png", ms, CancellationToken.None));

        Assert.True(
            ex.Message.Contains("IEND", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("truncado", StringComparison.OrdinalIgnoreCase),
            $"Expected IEND or truncado in: {ex.Message}");
    }

    // =====================================================================
    // B-09: Almacenamiento de documentos no-imagen (NO deben pasar por validador de imagen)
    // =====================================================================

    [Fact]
    public async Task DocumentCategory_NonImageContent_StoredWithoutImageValidation()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();

        // Contenido que NO es una imagen (sería rechazado por el validador de imagen)
        var pdfContent = "%PDF-1.4 Hello World\n%%EOF"u8.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(pdfContent));
        var canonicalKey = $"tenants/{tenantId:D}/documents/{aggregateId:D}/{sha256}.pdf";

        using var ms = new MemoryStream(pdfContent);

        // No debe lanzar excepción porque documents no requiere validación de imagen
        var result = await storage.PutAsync(canonicalKey, ms, "application/pdf", CancellationToken.None);

        Assert.Equal(canonicalKey, result.StorageKey);
        Assert.Equal(pdfContent.Length, result.ByteSize);
    }

    [Fact]
    public async Task AttachmentCategory_NonImageContent_StoredWithoutImageValidation()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();

        // Contenido de texto plano
        var textContent = "Este es un documento de texto adjunto para la cobranza"u8.ToArray();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(textContent));
        var canonicalKey = $"tenants/{tenantId:D}/attachments/{aggregateId:D}/{sha256}.txt";

        using var ms = new MemoryStream(textContent);
        var result = await storage.PutAsync(canonicalKey, ms, "text/plain", CancellationToken.None);

        Assert.Equal(canonicalKey, result.StorageKey);
        Assert.Equal(textContent.Length, result.ByteSize);
    }

    // =====================================================================
    // B-09: Almacenamiento de evidencias con JPEG/PNG/WebP válidos
    // =====================================================================

    [Fact]
    public async Task StoreEvidenceAsync_ValidJpeg_StoresAtomically_ReturnsCanonicalKey()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var payload = CreateMinimalValidJpeg(200, 150);
        using var expectedHashAlg = SHA256.Create();
        var expectedHash = Convert.ToHexStringLower(expectedHashAlg.ComputeHash(payload));

        using var memoryStream = new MemoryStream(payload);

        var result = await storage.StoreEvidenceAsync(
            tenantId,
            paymentId,
            "recibo.jpg",
            "image/jpeg",
            memoryStream,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(payload.Length, result.ByteSize);
        Assert.Equal(expectedHash, result.FileHashSha256);

        // Clave canónica: tenants/{tenantId}/evidences/{paymentId}/{sha256}.jpg
        var expectedKey = $"tenants/{tenantId:D}/evidences/{paymentId:D}/{expectedHash}.jpg";
        Assert.Equal(expectedKey, result.StorageKey);

        // Verificar existencia física en disco
        var fullPath = Path.Combine(testRoot, "tenants", tenantId.ToString("D"), "evidences", paymentId.ToString("D"), $"{expectedHash}.jpg");
        Assert.True(File.Exists(fullPath));

        // Verificar que .tmp está limpio
        var tempDir = Path.Combine(testRoot, ".tmp");
        if (Directory.Exists(tempDir))
        {
            Assert.Empty(Directory.GetFiles(tempDir));
        }
    }

    [Fact]
    public async Task StoreEvidenceAsync_ValidPngAndWebp_DetectsMimeAndExtension()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        // PNG
        var pngPayload = CreateMinimalValidPng(120, 80);
        using (var msPng = new MemoryStream(pngPayload))
        {
            var pngResult = await storage.StoreEvidenceAsync(tenantId, paymentId, "doc.png", "image/png", msPng, CancellationToken.None);
            Assert.Equal("image/png", pngResult.ContentType);
            Assert.EndsWith(".png", pngResult.StorageKey);
        }

        // WebP
        var webpPayload = CreateMinimalValidWebp(160, 90);
        using (var msWebp = new MemoryStream(webpPayload))
        {
            var webpResult = await storage.StoreEvidenceAsync(tenantId, paymentId, "doc.webp", "image/webp", msWebp, CancellationToken.None);
            Assert.Equal("image/webp", webpResult.ContentType);
            Assert.EndsWith(".webp", webpResult.StorageKey);
        }
    }

    // =====================================================================
    // B-09: Lectura desde dos instancias sobre la misma raíz
    // =====================================================================

    [Fact]
    public async Task CrossReplicaSimulation_WriteFromReplica1_ReadFromReplica2()
    {
        var replica1 = CreateStorage();
        var replica2 = CreateStorage();

        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var payload = CreateMinimalValidJpeg(100, 100);

        using var ms = new MemoryStream(payload);
        var stored = await replica1.StoreEvidenceAsync(tenantId, paymentId, "voucher.jpg", "image/jpeg", ms, CancellationToken.None);

        using (var readStream = await replica2.ReadEvidenceAsync(tenantId, stored.StorageKey, CancellationToken.None))
        {
            Assert.NotNull(readStream);
            using var msRead = new MemoryStream();
            await readStream.CopyToAsync(msRead);
            Assert.Equal(payload, msRead.ToArray());
        }

        await replica2.DeleteEvidenceAsync(tenantId, stored.StorageKey, CancellationToken.None);
        using var verifyStream = await replica1.ReadEvidenceAsync(tenantId, stored.StorageKey, CancellationToken.None);
        Assert.Null(verifyStream);
    }

    // =====================================================================
    // B-09: Eliminación y reconciliación de temporales
    // =====================================================================

    [Fact]
    public async Task ReconcileOrphanTempsAsync_CleansAbandonedTempFiles()
    {
        var storage = CreateStorage();
        var tempDir = Path.Combine(testRoot, ".tmp");
        Directory.CreateDirectory(tempDir);

        var orphanFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(orphanFile, "abandoned");
        File.SetCreationTimeUtc(orphanFile, DateTime.UtcNow.AddHours(-3));

        var cleaned = await storage.ReconcileOrphanTempsAsync(TimeSpan.FromHours(1), CancellationToken.None);
        Assert.Equal(1, cleaned);
        Assert.False(File.Exists(orphanFile));
    }

    [Fact]
    public async Task ReconcileOrphanTempsAsync_RecentFiles_NotCleaned()
    {
        var storage = CreateStorage();
        var tempDir = Path.Combine(testRoot, ".tmp");
        Directory.CreateDirectory(tempDir);

        // Archivo reciente (30 minutos), no debe ser limpiado
        var recentFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(recentFile, "recent");

        var cleaned = await storage.ReconcileOrphanTempsAsync(TimeSpan.FromHours(1), CancellationToken.None);
        Assert.Equal(0, cleaned);
        Assert.True(File.Exists(recentFile));
    }

    // =====================================================================
    // B-09: Compatibilidad legado/canónica
    // =====================================================================

    [Fact]
    public async Task ReadEvidenceAsync_LegacyAndCanonicalKeyCompatibility_BothWork()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var payload = CreateMinimalValidJpeg(100, 100);

        // 1. Guardar con clave canónica
        var stored = await storage.StoreEvidenceAsync(tenantId, paymentId, "proof.jpg", "image/jpeg", new MemoryStream(payload), CancellationToken.None);

        // Leer usando clave canónica
        using (var stream = await storage.ReadEvidenceAsync(tenantId, stored.StorageKey, CancellationToken.None))
        {
            Assert.NotNull(stream);
        }

        // 2. Probar formato legado {tenantId}/{paymentId}/{sha256}.jpg
        var legacyKey = $"{tenantId:D}/{paymentId:D}/legacy.jpg";
        var legacyFullPath = Path.Combine(testRoot, tenantId.ToString("D"), paymentId.ToString("D"), "legacy.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyFullPath)!);
        await File.WriteAllBytesAsync(legacyFullPath, payload);

        using (var legacyStream = await storage.ReadEvidenceAsync(tenantId, legacyKey, CancellationToken.None))
        {
            Assert.NotNull(legacyStream);
        }
    }
}
