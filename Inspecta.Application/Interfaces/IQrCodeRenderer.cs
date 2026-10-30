namespace Inspecta.Application.Interfaces;

/// <summary>Draws a certificate's QR code as a PNG image, for the certificate itself and to send to the client.</summary>
public interface IQrCodeRenderer
{
    byte[] RenderPng(string text);
}
