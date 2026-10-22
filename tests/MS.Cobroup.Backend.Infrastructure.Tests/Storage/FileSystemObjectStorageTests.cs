using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MS.Cobroup.Backend.Domain.Interfaces.Services;
using MS.Cobroup.Backend.Infrastructure.Storage;

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

    [Fact]
    public async Task StoreEvidenceAsync_ValidJpeg_StoresAtomically_ReturnsRelativeLogicalKeyAndCleansTemp()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        // Construct valid JPEG bytes
        var payload = new byte[1024];
        payload[0] = 0xFF;
        payload[1] = 0xD8;
        payload[2] = 0xFF;
        payload[3] = 0xE0;
        Random.Shared.NextBytes(payload.AsSpan(4));

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

        // 1. Result verification
        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(payload.Length, result.ByteSize);
        Assert.Equal(expectedHash, result.FileHashSha256);

        // 2. Relative logical key verification (free of physical absolute paths and drive letters)
        var expectedKey = $"{tenantId:D}/{paymentId:D}/{expectedHash}.jpg";
        Assert.Equal(expectedKey, result.StorageKey);
        Assert.Equal(expectedKey, result.StoragePath);
        Assert.DoesNotContain(":", result.StorageKey);
        Assert.False(Path.IsPathRooted(result.StorageKey));

        // 3. Physical file verification
        var physicalPath = Path.Combine(testRoot, tenantId.ToString("D"), paymentId.ToString("D"), $"{expectedHash}.jpg");
        Assert.True(File.Exists(physicalPath));
        var physicalBytes = await File.ReadAllBytesAsync(physicalPath);
        Assert.Equal(payload, physicalBytes);

        // 4. Verify temp directory is clean
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
        var pngPayload = new byte[512];
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        pngHeader.CopyTo(pngPayload, 0);

        using (var msPng = new MemoryStream(pngPayload))
        {
            var pngResult = await storage.StoreEvidenceAsync(tenantId, paymentId, "doc.png", "image/png", msPng, CancellationToken.None);
            Assert.Equal("image/png", pngResult.ContentType);
            Assert.EndsWith(".png", pngResult.StorageKey);
        }

        // WebP: RIFF....WEBP
        var webpPayload = new byte[512];
        byte[] riff = [0x52, 0x49, 0x46, 0x46];
        byte[] webp = [0x57, 0x45, 0x42, 0x50];
        riff.CopyTo(webpPayload, 0);
        webp.CopyTo(webpPayload, 8);

        using (var msWebp = new MemoryStream(webpPayload))
        {
            var webpResult = await storage.StoreEvidenceAsync(tenantId, paymentId, "doc.webp", "image/webp", msWebp, CancellationToken.None);
            Assert.Equal("image/webp", webpResult.ContentType);
            Assert.EndsWith(".webp", webpResult.StorageKey);
        }
    }

    [Fact]
    public async Task StoreEvidenceAsync_InvalidMagicBytes_ThrowsAndCleansUpTemp()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        // Non-image bytes (e.g., text or script)
        var invalidPayload = "<?php echo 'malicious'; ?>"u8.ToArray();
        using var ms = new MemoryStream(invalidPayload);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "script.jpg", "image/jpeg", ms, CancellationToken.None));

        Assert.Contains("formato de imagen permitido", ex.Message);

        // Verify .tmp cleaned
        var tempDir = Path.Combine(testRoot, ".tmp");
        if (Directory.Exists(tempDir))
        {
            Assert.Empty(Directory.GetFiles(tempDir));
        }
    }

    [Fact]
    public async Task StoreEvidenceAsync_ExceedsSizeLimit_ThrowsAndCleansUpTemp()
    {
        // 1 KB limit
        var storage = CreateStorage(maxBytes: 1024);
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var payload = new byte[2048];
        payload[0] = 0xFF;
        payload[1] = 0xD8;
        payload[2] = 0xFF;
        using var ms = new MemoryStream(payload);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "huge.jpg", "image/jpeg", ms, CancellationToken.None));

        Assert.Contains("límite máximo permitido", ex.Message);

        var tempDir = Path.Combine(testRoot, ".tmp");
        if (Directory.Exists(tempDir))
        {
            Assert.Empty(Directory.GetFiles(tempDir));
        }
    }

    [Fact]
    public async Task StoreEvidenceAsync_Cancellation_CleansUpTempFile()
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var payload = new byte[1024];
        payload[0] = 0xFF;
        payload[1] = 0xD8;
        payload[2] = 0xFF;
        using var ms = new MemoryStream(payload);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            storage.StoreEvidenceAsync(tenantId, paymentId, "cancel.jpg", "image/jpeg", ms, cts.Token));

        var tempDir = Path.Combine(testRoot, ".tmp");
        if (Directory.Exists(tempDir))
        {
            Assert.Empty(Directory.GetFiles(tempDir));
        }
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32")]
    [InlineData("/var/cobroup/evidences/file.jpg")]
    [InlineData("C:\\secrets\\key.pem")]
    [InlineData("tenant/../payment/file.jpg")]
    [InlineData("bad:stream:file.jpg")]
    public async Task ReadEvidenceAsync_PathTraversalOrAbsolute_ThrowsInvalidOperationException(string maliciousKey)
    {
        var storage = CreateStorage();
        var tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.ReadEvidenceAsync(tenantId, maliciousKey, CancellationToken.None));
    }

    [Fact]
    public async Task ReadEvidenceAsync_WrongTenantPrefix_ThrowsUnauthorizedTenant()
    {
        var storage = CreateStorage();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var key = $"{tenant1:D}/{paymentId:D}/file.jpg";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.ReadEvidenceAsync(tenant2, key, CancellationToken.None));

        Assert.Contains("Acceso no autorizado", ex.Message);
    }

    [Fact]
    public async Task CrossReplicaSimulation_WriteFromReplica1_ReadFromReplica2()
    {
        // Simulates two Kubernetes pod replicas mounting the same shared PVC (ReadWriteMany in prod)
        var replica1 = CreateStorage();
        var replica2 = CreateStorage();

        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var payload = new byte[512];
        payload[0] = 0xFF;
        payload[1] = 0xD8;
        payload[2] = 0xFF;
        Random.Shared.NextBytes(payload.AsSpan(3));

        using var ms = new MemoryStream(payload);
        var stored = await replica1.StoreEvidenceAsync(tenantId, paymentId, "voucher.jpg", "image/jpeg", ms, CancellationToken.None);

        // Read from the other replica instance pointing to the same shared mount
        using (var readStream = await replica2.ReadEvidenceAsync(tenantId, stored.StorageKey, CancellationToken.None))
        {
            Assert.NotNull(readStream);

            using var msRead = new MemoryStream();
            await readStream.CopyToAsync(msRead);
            Assert.Equal(payload, msRead.ToArray());
        }

        // Delete from replica 2
        await replica2.DeleteEvidenceAsync(tenantId, stored.StorageKey, CancellationToken.None);

        // Replica 1 confirms it's gone
        using var verifyStream = await replica1.ReadEvidenceAsync(tenantId, stored.StorageKey, CancellationToken.None);
        Assert.Null(verifyStream);
    }

    [Fact]
    public async Task RecreateService_DemonstratesPersistenceAcrossLifecycles()
    {
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var payload = new byte[256];
        payload[0] = 0xFF;
        payload[1] = 0xD8;
        payload[2] = 0xFF;

        string key;
        // Scope 1: Instance 1 stores file
        {
            var storage1 = CreateStorage();
            using var ms = new MemoryStream(payload);
            var result = await storage1.StoreEvidenceAsync(tenantId, paymentId, "proof.jpg", "image/jpeg", ms, CancellationToken.None);
            key = result.StorageKey;
        }

        // Scope 2: Instance 2 (after app restart) reads file
        {
            var storage2 = CreateStorage();
            using var stream = await storage2.ReadEvidenceAsync(tenantId, key, CancellationToken.None);
            Assert.NotNull(stream);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            Assert.Equal(payload, ms.ToArray());
        }
    }
}
