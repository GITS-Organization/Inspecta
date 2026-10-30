using Inspecta.Application.Interfaces;
using QRCoder;

namespace Inspecta.Infrastructure.Services;

public sealed class QrCodeRenderer : IQrCodeRenderer
{
    public byte[] RenderPng(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(12);
    }
}
