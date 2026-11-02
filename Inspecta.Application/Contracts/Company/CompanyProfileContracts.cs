using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Company;

/// <summary>The identity of a company: what its certificates, quotations and emails print. Every field is replaced by what is sent.</summary>
public sealed record SaveCompanyProfileRequest
{
    /// <summary>
    /// The name on quotations and in the signature of the emails. Give the name and the address together; leave both out to keep the
    /// built-in company details (the first customer's) and set only the images and colours.
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; init; }

    /// <summary>The name in the header and footer of a certificate (the name when left out).</summary>
    [MaxLength(200)]
    public string? CertificateTitle { get; init; }

    /// <summary>The company as named in the notes and the signature box of a certificate (the name when left out).</summary>
    [MaxLength(200)]
    public string? IssuerName { get; init; }

    /// <summary>The line under the name in the header of a certificate, for example <c>Industrial &amp; Energy Services.</c></summary>
    [MaxLength(200)]
    public string? Tagline { get; init; }

    [MaxLength(500)]
    public string? Address { get; init; }

    /// <summary>At most 5.</summary>
    public IReadOnlyList<string>? Phones { get; init; }

    /// <summary>At most 5. The first is the one printed on quotations.</summary>
    public IReadOnlyList<string>? Emails { get; init; }

    [MaxLength(200)]
    public string? Website { get; init; }

    /// <summary>Who signs for the company in the signature box of a certificate.</summary>
    [MaxLength(200)]
    public string? SignerName { get; init; }

    /// <summary><c>#RRGGBB</c>; the blue of the page bands (the default when left out).</summary>
    public string? PrimaryColor { get; init; }

    /// <summary><c>#RRGGBB</c>; the green of the page bands (the default when left out).</summary>
    public string? AccentColor { get; init; }

    /// <summary>The logo as base64 of a PNG (at most 1 MB). Left out keeps the current one; an empty text removes it. A company without its own logo gets the built-in one.</summary>
    public string? LogoBase64 { get; init; }

    /// <summary>The accreditation marks of the foot of a certificate, as base64 of a PNG (at most 1 MB). Left out keeps the current ones; an empty text removes them. A company without its own gets the built-in ones.</summary>
    public string? AccreditationsBase64 { get; init; }
}

public sealed record CompanyProfileResponse(
    Guid ManagerId,
    string Name,
    string? CertificateTitle,
    string? IssuerName,
    string? Tagline,
    string Address,
    IReadOnlyList<string> Phones,
    IReadOnlyList<string> Emails,
    string Website,
    string? SignerName,
    string PrimaryColor,
    string AccentColor,
    bool HasLogo,
    bool HasAccreditations,
    string? LogoUrl,
    DateTimeOffset UpdatedAt,
    bool UsesBuiltInDetails = false);
