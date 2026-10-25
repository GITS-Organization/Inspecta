namespace Inspecta.Domain.Entities;

public sealed record ItemChecklistEntry(string Name, bool IsCompleted)
{
    public static ItemChecklistEntry Create(string name, bool isCompleted)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Checklist item name is required.", nameof(name));
        return new ItemChecklistEntry(name.Trim(), isCompleted);
    }
}
