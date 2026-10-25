namespace Inspecta.Domain.Entities;

public sealed record ItemDate(string Name, DateOnly Value)
{
    public static ItemDate Create(string name, DateOnly value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Date name is required.", nameof(name));
        return new ItemDate(name.Trim(), value);
    }
}
