using System.Buffers.Binary;

namespace MS.Cobroup.Backend.Infrastructure.Storage;

/// <summary>
/// Validador estructural estricto para formatos de imagen (JPEG, PNG, WebP).
/// Realiza decodificación y verificación completa de firmas, chunks, CRC-32, terminación
/// y dimensiones máximas para proteger contra decompression bombs, polyglot files y archivos truncados.
/// </summary>
public static class ImageStructuralValidator
{
    public const int MaxDimensionPixels = 8192;

    private static readonly byte[] JpegSoi = [0xFF, 0xD8];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] PngIhdr = "IHDR"u8.ToArray();
    private static readonly byte[] PngIend = "IEND"u8.ToArray();
    private static readonly byte[] RiffHeader = "RIFF"u8.ToArray();
    private static readonly byte[] WebpHeader = "WEBP"u8.ToArray();

    // Tabla de lookup para CRC-32 estándar IEEE 802.3 (requerido por especificación PNG RFC 2083)
    private static readonly uint[] Crc32Table = InitializeCrcTable();

    private static uint[] InitializeCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var entry = i;
            for (var j = 0; j < 8; j++)
            {
                entry = (entry & 1) == 1 ? (entry >> 1) ^ 0xEDB88320u : entry >> 1;
            }
            table[i] = entry;
        }
        return table;
    }

    public static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = (crc >> 8) ^ Crc32Table[(crc ^ b) & 0xFF];
        }
        return crc ^ 0xFFFFFFFFu;
    }

    public static string ValidateAndDetect(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < 16)
        {
            throw new InvalidOperationException("El archivo es demasiado pequeño para ser una imagen válida.");
        }

        var span = data.AsSpan();

        if (span.StartsWith(JpegSoi))
        {
            ValidateJpeg(span);
            return "image/jpeg";
        }

        if (span.StartsWith(PngSignature))
        {
            ValidatePng(span);
            return "image/png";
        }

        if (span.Length >= 12 && span[..4].SequenceEqual(RiffHeader) && span.Slice(8, 4).SequenceEqual(WebpHeader))
        {
            ValidateWebp(span);
            return "image/webp";
        }

        throw new InvalidOperationException("El archivo cargado no corresponde a un formato de imagen permitido (JPEG, PNG o WebP).");
    }

    private static void ValidateJpeg(ReadOnlySpan<byte> data)
    {
        // 1. Debe comenzar con SOI (FF D8)
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            throw new InvalidOperationException("Estructura JPEG inválida: Cabecera SOI ausente.");
        }

        var offset = 2;
        var foundSof = false;
        var foundEoi = false;
        var eoiOffset = -1;

        while (offset < data.Length)
        {
            if (data[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            // Consumir posibles bytes 0xFF de relleno
            while (offset < data.Length && data[offset] == 0xFF)
            {
                offset++;
            }

            if (offset >= data.Length)
            {
                break;
            }

            var marker = data[offset];
            offset++;

            // 0x00 es escape de byte en scan data, ignorar
            if (marker == 0x00)
            {
                continue;
            }

            // Marcadores de reinicio RST0..RST7 (0xD0..0xD7) no tienen longitud
            if (marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            // Marcador EOI (FF D9)
            if (marker == 0xD9)
            {
                foundEoi = true;
                eoiOffset = offset;
                break;
            }

            // SOS (Start of Scan): datos de imagen comprimida comienzan aquí
            if (marker == 0xDA)
            {
                if (offset + 2 > data.Length)
                {
                    throw new InvalidOperationException("Estructura JPEG inválida: Cabecera SOS truncada.");
                }

                var sosLength = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
                offset += sosLength;

                // Escanear los datos comprimidos buscando EOI (FF D9)
                while (offset < data.Length - 1)
                {
                    if (data[offset] == 0xFF)
                    {
                        var nextByte = data[offset + 1];
                        if (nextByte == 0xD9) // EOI
                        {
                            foundEoi = true;
                            eoiOffset = offset + 2;
                            break;
                        }

                        if (nextByte == 0x00 || (nextByte is >= 0xD0 and <= 0xD7))
                        {
                            offset += 2;
                            continue;
                        }
                    }
                    offset++;
                }
                break;
            }

            // Otros segmentos con longitud (2 bytes big endian, incluye los propios 2 bytes de longitud)
            if (offset + 2 > data.Length)
            {
                throw new InvalidOperationException("Estructura JPEG inválida: Longitud de segmento truncada.");
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
            if (segmentLength < 2)
            {
                throw new InvalidOperationException("Estructura JPEG inválida: Longitud de segmento corrupta.");
            }

            if (offset + segmentLength > data.Length)
            {
                throw new InvalidOperationException("Estructura JPEG inválida: Archivo truncado dentro de segmento.");
            }

            // SOF0..SOF15 (excepto DHT 0xC4, JPG 0xC8, DAC 0xCC)
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                if (segmentLength >= 7)
                {
                    var height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 3, 2));
                    var width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 5, 2));

                    if (width == 0 || height == 0)
                    {
                        throw new InvalidOperationException("Dimensiones de imagen JPEG inválidas (0 píxeles).");
                    }

                    if (width > MaxDimensionPixels || height > MaxDimensionPixels)
                    {
                        throw new InvalidOperationException(
                            $"Las dimensiones de la imagen JPEG ({width}x{height}) superan el límite de seguridad de {MaxDimensionPixels}x{MaxDimensionPixels} píxeles (protección decompression bomb).");
                    }

                    foundSof = true;
                }
            }

            offset += segmentLength;
        }

        // El marcador SOF es OBLIGATORIO para cualquier archivo JPEG válido, sin excepciones de tamaño
        if (!foundSof)
        {
            throw new InvalidOperationException("Estructura JPEG inválida: No se encontró un segmento SOF con dimensiones legibles.");
        }

        // El marcador EOI es OBLIGATORIO
        if (!foundEoi)
        {
            throw new InvalidOperationException("Estructura JPEG inválida: Falta el marcador final EOI (archivo truncado o corrupto).");
        }

        // Protección contra polyglots: no permitir carga útil no autorizada tras el EOI
        if (eoiOffset > 0 && eoiOffset < data.Length)
        {
            var trailing = data[eoiOffset..];
            var nonZeroTrailing = 0;
            foreach (var b in trailing)
            {
                if (b != 0x00 && b != 0xFF)
                {
                    nonZeroTrailing++;
                }
            }

            if (nonZeroTrailing > 4)
            {
                throw new InvalidOperationException("Estructura JPEG inválida: Contenido sospechoso no autorizado después del marcador EOI (archivo polyglot detectado).");
            }
        }
    }

    private static void ValidatePng(ReadOnlySpan<byte> data)
    {
        if (data.Length < 33)
        {
            throw new InvalidOperationException("Estructura PNG inválida: Archivo demasiado corto para cabecera IHDR.");
        }

        // 1. Firma mágica PNG (8 bytes)
        if (!data[..8].SequenceEqual(PngSignature))
        {
            throw new InvalidOperationException("Estructura PNG inválida: Firma mágica incorrecta.");
        }

        var offset = 8;
        var foundIhdr = false;
        var foundIdat = false;
        var foundIend = false;

        // 2. Recorrer chunks de forma estricta según especificación PNG
        while (offset + 8 <= data.Length)
        {
            var chunkLength = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (chunkLength > int.MaxValue)
            {
                throw new InvalidOperationException("Estructura PNG inválida: Longitud de chunk corrupta.");
            }

            var chunkType = data.Slice(offset + 4, 4);
            var totalChunkSize = 4 + 4 + (int)chunkLength + 4; // len + type + data + crc

            if (offset + totalChunkSize > data.Length)
            {
                throw new InvalidOperationException("Estructura PNG inválida: Archivo truncado dentro de chunk.");
            }

            // Comprobar CRC-32 sobre [Tipo (4 bytes) + Datos (chunkLength bytes)]
            var typeAndData = data.Slice(offset + 4, 4 + (int)chunkLength);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8 + (int)chunkLength, 4));
            var computedCrc = ComputeCrc32(typeAndData);
            if (expectedCrc != computedCrc)
            {
                var typeStr = System.Text.Encoding.ASCII.GetString(chunkType);
                throw new InvalidOperationException($"Estructura PNG inválida: CRC-32 incorrecto en chunk '{typeStr}'.");
            }

            if (!foundIhdr)
            {
                // El primer chunk debe ser estrictamente IHDR
                if (!chunkType.SequenceEqual(PngIhdr))
                {
                    throw new InvalidOperationException("Estructura PNG inválida: El primer chunk debe ser IHDR.");
                }

                if (chunkLength != 13)
                {
                    throw new InvalidOperationException("Estructura PNG inválida: El chunk IHDR debe tener exactamente 13 bytes.");
                }

                var width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8, 4));
                var height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 12, 4));

                if (width == 0 || height == 0)
                {
                    throw new InvalidOperationException("Dimensiones de imagen PNG inválidas (0 píxeles).");
                }

                if (width > MaxDimensionPixels || height > MaxDimensionPixels)
                {
                    throw new InvalidOperationException(
                        $"Las dimensiones de la imagen PNG ({width}x{height}) superan el límite de seguridad de {MaxDimensionPixels}x{MaxDimensionPixels} píxeles (protección decompression bomb).");
                }

                foundIhdr = true;
            }
            else
            {
                if (chunkType.SequenceEqual(PngIhdr))
                {
                    throw new InvalidOperationException("Estructura PNG inválida: Múltiples chunks IHDR no permitidos.");
                }

                if (chunkType.SequenceEqual("IDAT"u8))
                {
                    foundIdat = true;
                }
                else if (chunkType.SequenceEqual(PngIend))
                {
                    foundIend = true;
                    offset += totalChunkSize;

                    // Protección estricta contra polyglots: IEND es el final absoluto del archivo
                    if (offset < data.Length)
                    {
                        throw new InvalidOperationException("Estructura PNG inválida: Contenido no autorizado después del chunk IEND (archivo polyglot detectado).");
                    }
                    break;
                }
            }

            offset += totalChunkSize;
        }

        if (!foundIhdr)
        {
            throw new InvalidOperationException("Estructura PNG inválida: Falta chunk IHDR.");
        }

        if (!foundIdat)
        {
            throw new InvalidOperationException("Estructura PNG inválida: Falta chunk IDAT (el archivo no contiene datos de imagen).");
        }

        if (!foundIend)
        {
            throw new InvalidOperationException("Estructura PNG inválida: Falta el chunk final IEND (archivo truncado o corrupto).");
        }
    }

    private static void ValidateWebp(ReadOnlySpan<byte> data)
    {
        if (data.Length < 16)
        {
            throw new InvalidOperationException("Estructura WebP inválida: Archivo demasiado corto para cabecera RIFF/WEBP.");
        }

        // 1. Cabecera RIFF
        if (!data[..4].SequenceEqual(RiffHeader) || !data.Slice(8, 4).SequenceEqual(WebpHeader))
        {
            throw new InvalidOperationException("Estructura WebP inválida: Cabecera RIFF/WEBP ausente o corrupta.");
        }

        // 2. Comprobar tamaño total declarado en RIFF
        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        var expectedTotalSize = riffSize + 8; // riffSize + 'RIFF' + 4 bytes size

        if (data.Length < expectedTotalSize)
        {
            throw new InvalidOperationException("Estructura WebP inválida: Archivo truncado respecto al tamaño declarado en cabecera RIFF.");
        }

        // Permitir como máximo 1 byte de alineación par
        if (data.Length > expectedTotalSize + 1)
        {
            throw new InvalidOperationException("Estructura WebP inválida: Datos adicionales no autorizados después del contenedor RIFF (archivo polyglot detectado).");
        }

        // 3. Recorrer chunks WebP
        var offset = 12;
        var foundFrame = false;

        while (offset + 8 <= data.Length)
        {
            var chunkType = data.Slice(offset, 4);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
            var paddedChunkSize = 8 + (int)chunkSize + ((int)chunkSize % 2);

            if (offset + 8 + chunkSize > data.Length)
            {
                throw new InvalidOperationException("Estructura WebP inválida: Chunk truncado dentro del contenedor.");
            }

            if (chunkType.SequenceEqual("VP8 "u8))
            {
                // Lossy VP8
                if (chunkSize < 10)
                {
                    throw new InvalidOperationException("Estructura WebP VP8 inválida: Cabecera de frame truncada.");
                }

                var frameHeader = data.Slice(offset + 8, (int)chunkSize);
                // Verificar código de inicio VP8 9D 01 2A
                if (frameHeader.Length >= 6 && frameHeader[3] == 0x9D && frameHeader[4] == 0x01 && frameHeader[5] == 0x2A)
                {
                    var width = BinaryPrimitives.ReadUInt16LittleEndian(frameHeader.Slice(6, 2)) & 0x3FFF;
                    var height = BinaryPrimitives.ReadUInt16LittleEndian(frameHeader.Slice(8, 2)) & 0x3FFF;

                    if (width == 0 || height == 0)
                    {
                        throw new InvalidOperationException("Dimensiones de imagen WebP VP8 inválidas (0 píxeles).");
                    }

                    if (width > MaxDimensionPixels || height > MaxDimensionPixels)
                    {
                        throw new InvalidOperationException(
                            $"Las dimensiones de la imagen WebP VP8 ({width}x{height}) superan el límite de {MaxDimensionPixels}x{MaxDimensionPixels} píxeles (protección decompression bomb).");
                    }

                    foundFrame = true;
                }
            }
            else if (chunkType.SequenceEqual("VP8L"u8))
            {
                // Lossless VP8L
                if (chunkSize < 5)
                {
                    throw new InvalidOperationException("Estructura WebP VP8L inválida: Cabecera truncada.");
                }

                var headerData = data.Slice(offset + 8, (int)chunkSize);
                if (headerData[0] != 0x2F)
                {
                    throw new InvalidOperationException("Estructura WebP VP8L inválida: Firma 0x2F ausente.");
                }

                var b1 = headerData[1];
                var b2 = headerData[2];
                var b3 = headerData[3];
                var b4 = headerData[4];

                var width = (b1 | ((b2 & 0x3F) << 8)) + 1;
                var height = (((b2 >> 6) | (b3 << 2) | ((b4 & 0x0F) << 10))) + 1;

                if (width == 0 || height == 0)
                {
                    throw new InvalidOperationException("Dimensiones de imagen WebP VP8L inválidas (0 píxeles).");
                }

                if (width > MaxDimensionPixels || height > MaxDimensionPixels)
                {
                    throw new InvalidOperationException(
                        $"Las dimensiones de la imagen WebP VP8L ({width}x{height}) superan el límite de {MaxDimensionPixels}x{MaxDimensionPixels} píxeles (protección decompression bomb).");
                }

                foundFrame = true;
            }
            else if (chunkType.SequenceEqual("VP8X"u8))
            {
                // Extended VP8X
                if (chunkSize < 10)
                {
                    throw new InvalidOperationException("Estructura WebP VP8X inválida: Cabecera truncada.");
                }

                var headerData = data.Slice(offset + 8, (int)chunkSize);
                var width = (headerData[4] | (headerData[5] << 8) | (headerData[6] << 16)) + 1;
                var height = (headerData[7] | (headerData[8] << 8) | (headerData[9] << 16)) + 1;

                if (width == 0 || height == 0)
                {
                    throw new InvalidOperationException("Dimensiones de imagen WebP VP8X inválidas (0 píxeles).");
                }

                if (width > MaxDimensionPixels || height > MaxDimensionPixels)
                {
                    throw new InvalidOperationException(
                        $"Las dimensiones de la imagen WebP VP8X ({width}x{height}) superan el límite de {MaxDimensionPixels}x{MaxDimensionPixels} píxeles (protección decompression bomb).");
                }

                foundFrame = true;
            }

            offset += paddedChunkSize;
        }

        if (!foundFrame)
        {
            throw new InvalidOperationException("Estructura WebP inválida: No se encontró ningún chunk de imagen válido (VP8, VP8L o VP8X).");
        }
    }
}
