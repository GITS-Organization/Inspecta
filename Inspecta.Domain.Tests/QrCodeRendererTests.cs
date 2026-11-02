using Inspecta.Infrastructure.Services;

namespace Inspecta.Domain.Tests;

public sealed class QrCodeRendererTests
{
    [Fact]
    public void TheCodeIsASquarePngOfThePlainQrCode()
    {
        var png = new QrCodeRenderer().RenderPng("https://example.com/verify/ABCD-1234");

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        var width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        var height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Assert.Equal(width, height);
        Assert.True(width >= 200);
    }

    [Fact]
    public void TheSameTextAlwaysGivesTheSameImage()
    {
        var renderer = new QrCodeRenderer();
        Assert.Equal(renderer.RenderPng("same"), renderer.RenderPng("same"));
        Assert.NotEqual(renderer.RenderPng("same"), renderer.RenderPng("other"));
    }
}
