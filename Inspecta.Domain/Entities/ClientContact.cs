namespace Inspecta.Domain.Entities;

public sealed class ClientContact
{
    private ClientContact() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool IsPrimary { get; private set; }

    public static ClientContact Create(Guid clientId, string name, string? email = null, string? phone = null, bool isPrimary = false)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("Client is required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Contact name is required.", nameof(name));
        return new ClientContact
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = name.Trim(),
            Email = email?.Trim(),
            Phone = phone?.Trim(),
            IsPrimary = isPrimary
        };
    }
}