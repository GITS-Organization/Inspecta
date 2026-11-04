namespace Inspecta.Application.Certificates;

/// <summary>A technical field of a certificate (for example "Span length"). <see cref="Prefill"/> names the machine detail that fills it.</summary>
public sealed record TemplateField(string Key, string Label, string? Prefill);

public sealed record TemplateChecklistItem(string Key, string Label);

public sealed record TemplateChecklistSection(string Key, string Title, IReadOnlyList<TemplateChecklistItem> Items);

/// <summary>
/// What the inspector fills for one kind of machine: its technical fields and, for some machines, a checklist.
/// Everything else on the certificate (questionnaire, notes, signatures) is the same for all templates.
/// </summary>
public sealed record CertificateTemplate(
    string Key,
    int Version,
    string Name,
    IReadOnlyList<TemplateField> Fields,
    IReadOnlyList<TemplateChecklistSection> Checklist)
{
    /// <summary>Every checklist row as <c>section.item</c>, in the order of the form.</summary>
    public IEnumerable<string> ChecklistPaths =>
        Checklist.SelectMany(section => section.Items.Select(item => $"{section.Key}.{item.Key}"));
}

/// <summary>
/// A machine kind known to the catalogue: its name (the one the screens offer in a list), the standard(s) it is inspected against (fixed by
/// the name), its template and its <see cref="Kind"/>. A gear is a lifting accessory (sling, shackle, hook...) inspected in numbers: its
/// certificate carries a quantity and many serial numbers, and it is not registered as an item.
/// </summary>
public sealed record CatalogItem(string Name, IReadOnlyList<string> Aliases, IReadOnlyList<string> Standards, string Template, string Kind = CatalogKinds.Equipment)
{
    /// <summary>A gear is counted in numbers and divided into certificates; it is not registered as an item.</summary>
    public bool Gear => Kind == CatalogKinds.Gear;
}

/// <summary>The three kinds of machine a catalogue name belongs to.</summary>
public static class CatalogKinds
{
    /// <summary>A lifting accessory: sling, shackle, hook, clamp, harness...</summary>
    public const string Gear = "gear";

    /// <summary>Lifting equipment: cranes, hoists, jacks, forklifts, lifts...</summary>
    public const string Equipment = "equipment";

    /// <summary>A machine that does not lift: compressors, generators, loaders, vehicles...</summary>
    public const string NonLifting = "non_lifting";
}

/// <summary>The template chosen for a machine, and the catalogue entry when its name was recognised.</summary>
public sealed record TemplateMatch(CertificateTemplate Template, CatalogItem? Item);
