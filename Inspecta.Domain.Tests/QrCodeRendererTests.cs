using System.IO.Compression;
using Inspecta.Infrastructure.Services;

namespace Inspecta.Domain.Tests;

public sealed class QrCodeRendererTests
{
    private static (int Size, byte[] Pixels) Decode(byte[] png)
    {
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        var size = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        Assert.Equal(size, (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
        Assert.Equal(8, png[24]);
        Assert.Equal(2, png[25]);

        var idatLength = (png[33] << 24) | (png[34] << 16) | (png[35] << 8) | png[36];
        Assert.Equal("IDAT", System.Text.Encoding.ASCII.GetString(png, 37, 4));
        using var inflated = new ZLibStream(new MemoryStream(png, 41, idatLength), CompressionMode.Decompress);
        using var rows = new MemoryStream();
        inflated.CopyTo(rows);
        var data = rows.ToArray();
        Assert.Equal((size * 3 + 1) * size, data.Length);

        var pixels = new byte[size * size * 3];
        for (var y = 0; y < size; y++)
            Buffer.BlockCopy(data, y * (size * 3 + 1) + 1, pixels, y * size * 3, size * 3);
        return (size, pixels);
    }

    [Fact]
    public void TheCodeIsAValidPngWithTheCompanyMarkInItsMiddleAndTheEdgesLeftForScanning()
    {
        var png = new QrCodeRenderer().RenderPng("https://example.com/verify/ABCD-1234");
        var (size, pixels) = Decode(png);

        bool Coloured(int x, int y) => Math.Abs(pixels[(y * size + x) * 3] - pixels[(y * size + x) * 3 + 2]) > 40;

        var middle = size / 2;
        var coloured = 0;
        for (var y = middle - size / 10; y < middle + size / 10; y++)
        for (var x = middle - size / 10; x < middle + size / 10; x++)
            if (Coloured(x, y)) coloured++;
        Assert.True(coloured > 100, "the mark is drawn in the middle");

        // Only the middle is covered: the corners (where the scanner finds the code) stay black and white.
        for (var y = 0; y < size; y += 7)
        for (var x = 0; x < size; x += 7)
            if (Math.Abs(x - middle) > size / 4 || Math.Abs(y - middle) > size / 4)
                Assert.False(Coloured(x, y));
    }

    [Fact]
    public void TheSameTextAlwaysGivesTheSameImage()
    {
        var renderer = new QrCodeRenderer();
        Assert.Equal(renderer.RenderPng("same"), renderer.RenderPng("same"));
        Assert.NotEqual(renderer.RenderPng("same"), renderer.RenderPng("other"));
    }
}
