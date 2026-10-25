namespace Inspecta.Domain.Entities;

public sealed class ClientContact
{
    private ClientContact() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Role { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsMain => IsPrimary;

    public static ClientContact Create(
        Guid clientId,
        string name,
        string? email = null,
        string? phone = null,
        bool isPrimary = false,
        string? role = null)
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
            Role = role?.Trim(),
            IsPrimary = isPrimary
        };
    }

    public void Update(string name, string? role, string? email, string? phone, bool isPrimary)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Contact name is required.", nameof(name));

        Name = name.Trim();
        Role = role?.Trim();
        Email = email?.Trim();
        Phone = phone?.Trim();
        IsPrimary = isPrimary;
    }
}