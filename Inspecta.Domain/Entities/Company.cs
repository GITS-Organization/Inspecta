namespace Inspecta.Domain.Entities;

public sealed class Company
{
    private Company() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Company Create(string name, DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name is required.", nameof(name));

        return new Company
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    public void Deactivate() => IsActive = false;
}
