namespace Inspecta.Application.Certificates;

/// <summary>Where certificates point to: the address the QR code opens (<c>Certificates:PublicBaseUrl</c>).</summary>
public sealed record CertificateOptions(string PublicBaseUrl = "https://api.gitsplatform.com")
{
    /// <summary>The page that confirms a certificate is genuine; this is what the QR code holds.</summary>
    public string VerifyUrl(string verificationCode) => $"{PublicBaseUrl.TrimEnd('/')}/verify/{verificationCode}";
}
