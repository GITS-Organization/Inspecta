using System.Text.RegularExpressions;

namespace Inspecta.Application.Certificates;

/// <summary>The outcome of checking a verification host: the lower-case host, or an error code (<c>invalid_host</c>, <c>host_reserved</c>) and a message.</summary>
public sealed record HostCheck(string? Host, string? ErrorCode, string? Message)
{
    public bool IsValid => Host is not null;
}

/// <summary>What a company may use as its verification address: a plain DNS name that is not ours.</summary>
public static class VerificationHost
{
    /// <summary>The service's own domain: it and everything under it is reserved.</summary>
    public const string ReservedDomain = "gitsplatform.com";

    private const int MaxLength = 253;
    private static readonly Regex Label = new("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Checks and normalises a host typed by a person. <paramref name="ownHost"/> is the host of the configured public address
    /// (<c>Certificates:PublicBaseUrl</c>), which is reserved as well.
    /// </summary>
    public static HostCheck Check(string? input, string? ownHost = null)
    {
        var host = input?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(host)) return Invalid("Enter the address, for example verify.yourcompany.com.");
        if (host.Contains("://") || host.Contains('/') || host.Contains('\\') || host.Contains('?') || host.Contains('#'))
            return Invalid("Enter only the address (for example verify.yourcompany.com), without https:// or a path.");
        if (host.Contains('@')) return Invalid("The address cannot contain @.");
        if (host.Contains(':') || host.Contains('[')) return Invalid("The address cannot contain a port or be an IP address.");
        if (host.Contains('_')) return Invalid("The address cannot contain an underscore.");
        if (host.Length > MaxLength) return Invalid($"The address is too long (at most {MaxLength} characters).");
        if (host.EndsWith('.')) return Invalid("Remove the dot at the end of the address.");
        if (host.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '.')))
            return Invalid("Use only letters, digits, hyphens and dots (for an international name use its xn-- form).");

        var labels = host.Split('.');
        if (labels.Length < 2) return Invalid("Enter a full address with a dot, for example verify.yourcompany.com.");
        if (labels.Any(label => !Label.IsMatch(label)))
            return Invalid("Each part of the address must be 1 to 63 letters, digits or hyphens, and cannot start or end with a hyphen.");
        if (labels[^1].All(char.IsAsciiDigit)) return Invalid("The address cannot be an IP address.");

        if (host == ReservedDomain || host.EndsWith("." + ReservedDomain, StringComparison.Ordinal) || host == ownHost?.Trim().ToLowerInvariant())
            return new HostCheck(null, "host_reserved", "This address belongs to the service itself. Use an address under your own domain.");
        return new HostCheck(host, null, null);
    }

    /// <summary>The host of a configured address such as <c>https://api.gitsplatform.com</c>, or null when it is not an absolute address.</summary>
    public static string? HostOf(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host) ? uri.Host.ToLowerInvariant() : null;

    /// <summary>A request host as the public page sees it: lower-case, without a trailing dot.</summary>
    public static string? Normalize(string? host) =>
        host?.Trim().TrimEnd('.').ToLowerInvariant() is { Length: > 0 } value ? value : null;

    private static HostCheck Invalid(string message) => new(null, "invalid_host", message);
}
