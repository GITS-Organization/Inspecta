using Inspecta.Application.Interfaces;
using Inspecta.Infrastructure.Pdf;
using Inspecta.Infrastructure.Pdf.Templates;

namespace Inspecta.Infrastructure.Services;

/// <summary>The company details and logo of the certificate forms, for the signature of the emails.</summary>
public sealed class EmailBrandingProvider : IEmailBranding
{
    private static readonly EmailBranding Branding = new(
        DefaultCompanyProfile.Instance.Name,
        DefaultCompanyProfile.Instance.Address,
        DefaultCompanyProfile.Instance.Website,
        PdfAssets.CertificateLogo);

    public EmailBranding Get() => Branding;
}
