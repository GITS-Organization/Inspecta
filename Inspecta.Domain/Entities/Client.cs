namespace Inspecta.Domain.Entities;

public sealed class Client
{
    private Client() { }

    public Guid Id { get; private set; }
    public string? LegacyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public List<ClientContact> Contacts { get; private set; } = [];

    public static Client Create(string name, string? legacyId = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Client name is required.", nameof(name));
        return new Client
        {
            Id = Guid.NewGuid(),
            LegacyId = legacyId,
            Name = name.Trim(),
            Notes = notes?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Archive()
    {
        ArchivedAt ??= DateTimeOffset.UtcNow;
    }
}