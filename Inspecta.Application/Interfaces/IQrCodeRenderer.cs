namespace Inspecta.Application.Interfaces;

/// <summary>Draws a certificate's QR code as a PNG image (the QR is not printed on the certificate itself).</summary>
public interface IQrCodeRenderer
{
    byte[] RenderPng(string text);
}
