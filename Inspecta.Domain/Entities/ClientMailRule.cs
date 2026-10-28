using System.Text.RegularExpressions;

namespace Inspecta.Domain.Entities;

/// <summary>
/// A sender address or domain of a team. Mail from a matching sender becomes a request straight away;
/// mail from anyone else waits as a "maybe" for the coordinator.
/// </summary>
public sealed class ClientMailRule
{
    private static readonly Regex DomainPattern = new(
        @"^(?=.{3,255}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private ClientMailRule() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }
    public string Pattern { get; private set; } = string.Empty;
    public Guid? ClientId { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsDomain => !Pattern.Contains('@', StringComparison.Ordinal);

    public static ClientMailRule Create(Guid managerId, string pattern, Guid? clientId, Guid createdById)
    {
        if (managerId == Guid.Empty) throw new ArgumentException("Manager is required.", nameof(managerId));
        if (createdById == Guid.Empty) throw new ArgumentException("Creator is required.", nameof(createdById));
        if (clientId == Guid.Empty) throw new ArgumentException("Client id cannot be empty.", nameof(clientId));

        return new ClientMailRule
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            Pattern = NormalizePattern(pattern),
            ClientId = clientId,
            CreatedById = createdById,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Lower-cases the pattern and accepts <c>name@host.com</c>, <c>@host.com</c> or <c>host.com</c>.</summary>
    public static string NormalizePattern(string? pattern)
    {
        var value = (pattern ?? string.Empty).Trim().ToLowerInvariant();
        if (value.StartsWith("*@", StringComparison.Ordinal)) value = value[2..];
        if (value.StartsWith('@')) value = value[1..];
        if (value.Length == 0)
            throw new ArgumentException("A sender email address or domain is required.", nameof(pattern));

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at < 0)
        {
            if (!DomainPattern.IsMatch(value))
                throw new ArgumentException("Enter a valid email address or domain such as example.com.", nameof(pattern));
            return value;
        }

        var local = value[..at];
        var domain = value[(at + 1)..];
        if (local.Length == 0 || local.Length > 64 || local.Any(char.IsWhiteSpace) ||
            domain.Contains('@', StringComparison.Ordinal) || !DomainPattern.IsMatch(domain))
            throw new ArgumentException("Enter a valid email address or domain such as example.com.", nameof(pattern));
        return value;
    }

    public bool Matches(string? senderAddress)
    {
        var address = senderAddress?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(address)) return false;
        if (!IsDomain) return address == Pattern;

        var at = address.LastIndexOf('@');
        return at >= 0 && address[(at + 1)..] == Pattern;
    }

    /// <summary>Returns the matching rule, preferring an exact address over a domain.</summary>
    public static ClientMailRule? FindMatch(IEnumerable<ClientMailRule> rules, string? senderAddress)
    {
        var matches = rules.Where(rule => rule.Matches(senderAddress)).ToList();
        return matches.FirstOrDefault(rule => !rule.IsDomain) ?? matches.FirstOrDefault();
    }
}
