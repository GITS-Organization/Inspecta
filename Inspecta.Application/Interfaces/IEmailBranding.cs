namespace Inspecta.Application.Interfaces;

/// <summary>The company details printed in the signature of an email, and the logo that goes with them.</summary>
public sealed record EmailBranding(string CompanyName, string Address, string Website, byte[]? LogoPng);

/// <summary>Where the signature of the emails gets the company's address, website and logo.</summary>
public interface IEmailBranding
{
    EmailBranding Get();
}
