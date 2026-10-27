using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class DemoRequest
{
    private DemoRequest() { }

    public Guid Id { get; private set; }
    public DemoRequestType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Company { get; private set; }
    public string? Role { get; private set; }
    public string? Message { get; private set; }
    public string Language { get; private set; } = "en";
    public string Source { get; private set; } = "website";
    public DateTimeOffset CreatedAt { get; private set; }

    public static DemoRequest Create(
        DemoRequestType type,
        string name,
        string email,
        string? company,
        string? role,
        string? message,
        string language,
        string source,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (language is not ("en" or "ar"))
            throw new ArgumentException("Language must be en or ar.", nameof(language));
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source is required.", nameof(source));

        return new DemoRequest
        {
            Id = Guid.NewGuid(),
            Type = type,
            Name = name.Trim(),
            Email = email.Trim(),
            Company = company?.Trim(),
            Role = role?.Trim(),
            Message = message?.Trim(),
            Language = language,
            Source = source.Trim(),
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }
}
