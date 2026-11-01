using System.IO.Compression;
using Inspecta.Application.Interfaces;
using QRCoder;

namespace Inspecta.Infrastructure.Services;

/// <summary>
/// Draws the QR code with the company mark in its middle. The code is made at the highest error-correction level
/// (about 30% of it can be covered and it still reads) and the mark covers only a few percent of it, on a white square.
/// </summary>
public sealed class QrCodeRenderer : IQrCodeRenderer
{
    private const int ModulePixels = 12;
    private const int QuietModules = 4;
    private const int LogoSide = 144;
    private const string LogoResource = "Inspecta.Infrastructure.Services.qr-logo.rgb";

    /// <summary>Width of the mark, with its white square, as a share of the code's width.</summary>
    private const double LogoShare = 0.2;

    private static readonly Lazy<byte[]> Logo = new(LoadLogo);

    public byte[] RenderPng(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.H);
        var matrix = data.ModuleMatrix;
        var size = matrix.Count * ModulePixels; // the matrix already includes the quiet zone
        var pixels = new byte[size * size * 3];
        Array.Fill(pixels, (byte)255);

        for (var row = 0; row < matrix.Count; row++)
        for (var column = 0; column < matrix.Count; column++)
        {
            if (!matrix[row][column]) continue;
            for (var y = row * ModulePixels; y < (row + 1) * ModulePixels; y++)
            for (var x = column * ModulePixels; x < (column + 1) * ModulePixels; x++)
            {
                var at = (y * size + x) * 3;
                pixels[at] = pixels[at + 1] = pixels[at + 2] = 0;
            }
        }

        DrawLogo(pixels, size, matrix.Count);
        return EncodePng(pixels, size);
    }

    private static void DrawLogo(byte[] pixels, int size, int modules)
    {
        var codeWidth = (modules - 2 * QuietModules) * ModulePixels;
        var side = (int)(codeWidth * LogoShare);
        var padding = Math.Max(ModulePixels / 2, side / 14);
        var markSide = side - 2 * padding;
        var left = (size - side) / 2;
        var markLeft = left + padding;
        var logo = Logo.Value;

        // The white square is already white from the background; clear any dark modules under it.
        for (var y = left; y < left + side; y++)
        for (var x = left; x < left + side; x++)
        {
            var at = (y * size + x) * 3;
            pixels[at] = pixels[at + 1] = pixels[at + 2] = 255;
        }

        // Bilinear sampling of the stored 144 px mark, scaled to the space it has.
        for (var y = 0; y < markSide; y++)
        for (var x = 0; x < markSide; x++)
        {
            var sourceX = (x + 0.5) * LogoSide / markSide - 0.5;
            var sourceY = (y + 0.5) * LogoSide / markSide - 0.5;
            var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, LogoSide - 1);
            var y0 = Math.Clamp((int)Math.Floor(sourceY), 0, LogoSide - 1);
            var x1 = Math.Min(x0 + 1, LogoSide - 1);
            var y1 = Math.Min(y0 + 1, LogoSide - 1);
            var fx = Math.Clamp(sourceX - x0, 0, 1);
            var fy = Math.Clamp(sourceY - y0, 0, 1);
            var at = ((left + padding + y) * size + markLeft + x) * 3;
            for (var channel = 0; channel < 3; channel++)
            {
                var top = logo[(y0 * LogoSide + x0) * 3 + channel] * (1 - fx) + logo[(y0 * LogoSide + x1) * 3 + channel] * fx;
                var bottom = logo[(y1 * LogoSide + x0) * 3 + channel] * (1 - fx) + logo[(y1 * LogoSide + x1) * 3 + channel] * fx;
                pixels[at + channel] = (byte)Math.Round(top * (1 - fy) + bottom * fy);
            }
        }
    }

    private static byte[] LoadLogo()
    {
        using var stream = typeof(QrCodeRenderer).Assembly.GetManifestResourceStream(LogoResource)
            ?? throw new InvalidOperationException("The QR code mark is missing from the build.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static byte[] EncodePng(byte[] pixels, int size)
    {
        var rows = new byte[(size * 3 + 1) * size];
        for (var y = 0; y < size; y++)
            Buffer.BlockCopy(pixels, y * size * 3, rows, y * (size * 3 + 1) + 1, size * 3);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(rows, 0, rows.Length);

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        WriteInt(header, 0, size);
        WriteInt(header, 4, size);
        header[8] = 8; // bit depth
        header[9] = 2; // RGB
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        stream.Write(length);
        var body = new byte[4 + data.Length];
        for (var i = 0; i < 4; i++) body[i] = (byte)type[i];
        Buffer.BlockCopy(data, 0, body, 4, data.Length);
        stream.Write(body);
        var crc = new byte[4];
        WriteInt(crc, 0, (int)Crc32(body));
        stream.Write(crc);
    }

    private static void WriteInt(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
