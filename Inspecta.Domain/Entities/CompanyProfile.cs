using System.Text.Json;
using System.Text.RegularExpressions;

namespace Inspecta.Domain.Entities;

/// <summary>
/// The identity of the company that owns a team (a technical manager): the name, address, contacts, logo and colours that are
/// printed on its certificates, its quotations and the signature of its emails. A team without one gets the built-in profile.
/// </summary>
public sealed class CompanyProfile
{
    public const int MaxLogoBytes = 1024 * 1024;
    public const int MaxContacts = 5;
    public const string DefaultPrimaryColor = "#184896";
    public const string DefaultAccentColor = "#34A046";

    private static readonly Regex Color = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private CompanyProfile() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }

    /// <summary>The name on quotations and in the signature of the emails.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>The name in the header and footer of a certificate; <see cref="Name"/> when empty.</summary>
    public string? CertificateTitle { get; private set; }

    /// <summary>The company as named in the certificate's notes and signature box; <see cref="Name"/> when empty.</summary>
    public string? IssuerName { get; private set; }

    public string? Tagline { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string PhonesJson { get; private set; } = "[]";
    public string EmailsJson { get; private set; } = "[]";
    public string Website { get; private set; } = string.Empty;

    /// <summary>Who signs for the company in the certificate's signature box.</summary>
    public string? SignerName { get; private set; }

    public byte[]? LogoPng { get; private set; }
    public byte[]? AccreditationsPng { get; private set; }
    public string PrimaryColor { get; private set; } = DefaultPrimaryColor;
    public string AccentColor { get; private set; } = DefaultAccentColor;
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<string> Phones => Read(PhonesJson);
    public IReadOnlyList<string> Emails => Read(EmailsJson);

    public static CompanyProfile Create(Guid managerId)
    {
        if (managerId == Guid.Empty) throw new ArgumentException("Manager is required.", nameof(managerId));
        return new CompanyProfile { Id = Guid.NewGuid(), ManagerId = managerId, UpdatedAt = DateTimeOffset.UtcNow };
    }

    /// <summary>Replaces the text details. Images and the rest are changed with their own methods.</summary>
    public void Update(
        string name,
        string? certificateTitle,
        string? issuerName,
        string? tagline,
        string address,
        IReadOnlyList<string>? phones,
        IReadOnlyList<string>? emails,
        string? website,
        string? signerName,
        string? primaryColor,
        string? accentColor)
    {
        Name = Required(name, "Name", 200);
        CertificateTitle = Optional(certificateTitle, "Certificate title", 200);
        IssuerName = Optional(issuerName, "Issuer name", 200);
        Tagline = Optional(tagline, "Tagline", 200);
        Address = Required(address, "Address", 500);
        PhonesJson = Write(phones, "phone", 40);
        EmailsJson = Write(emails, "email", 320);
        Website = Optional(website, "Website", 200) ?? string.Empty;
        SignerName = Optional(signerName, "Signer name", 200);
        PrimaryColor = ColorOrDefault(primaryColor, DefaultPrimaryColor, "Primary colour");
        AccentColor = ColorOrDefault(accentColor, DefaultAccentColor, "Accent colour");
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Sets the logo (a PNG of at most 1 MB) or removes it with <c>null</c>.</summary>
    public void SetLogo(byte[]? png)
    {
        LogoPng = CheckPng(png, "logo");
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Sets the accreditation marks printed in the foot of a certificate (a PNG of at most 1 MB) or removes them.</summary>
    public void SetAccreditations(byte[]? png)
    {
        AccreditationsPng = CheckPng(png, "accreditations image");
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static byte[]? CheckPng(byte[]? png, string what)
    {
        if (png is not { Length: > 0 }) return null;
        if (png.Length > MaxLogoBytes) throw new ArgumentException($"The {what} is larger than 1 MB.");
        if (!png.AsSpan().StartsWith(PngSignature)) throw new ArgumentException($"The {what} must be a PNG image.");
        return png;
    }

    private static string Required(string? value, string field, int maxLength)
    {
        var text = Optional(value, field, maxLength);
        return text ?? throw new ArgumentException($"{field} is required.");
    }

    private static string? Optional(string? value, string field, int maxLength)
    {
        var text = string.Concat((value ?? string.Empty).Where(character => !char.IsControl(character))).Trim();
        if (text.Length == 0) return null;
        if (text.Length > maxLength) throw new ArgumentException($"{field} is longer than {maxLength} characters.");
        return text;
    }

    private static string ColorOrDefault(string? value, string fallback, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = value.Trim();
        return Color.IsMatch(text) ? text.ToUpperInvariant() : throw new ArgumentException($"{field} must look like #RRGGBB.");
    }

    private static string Write(IReadOnlyList<string>? values, string what, int maxLength)
    {
        var items = (values ?? [])
            .Select(value => Optional(value, what, maxLength))
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (items.Count > MaxContacts) throw new ArgumentException($"At most {MaxContacts} {what}s can be given.");
        return JsonSerializer.Serialize(items);
    }

    private static IReadOnlyList<string> Read(string json) =>
        JsonSerializer.Deserialize<List<string>>(json) ?? [];
}
