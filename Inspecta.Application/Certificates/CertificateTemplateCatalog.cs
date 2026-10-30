using System.Text.Json;

namespace Inspecta.Application.Certificates;

/// <summary>
/// The certificate templates and the machines they belong to, loaded from the JSON files embedded under
/// <c>Certificates/Templates</c>. A template is chosen by the machine's name (not its type or standard); a name the
/// catalogue does not know gets the <c>basic</c> template.
/// </summary>
public static class CertificateTemplateCatalog
{
    public const string BasicKey = "basic";
    private const string CatalogFile = "_catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<Loaded> Data = new(Load);

    public static IReadOnlyCollection<CertificateTemplate> Templates => Data.Value.Templates.Values.ToArray();
    public static IReadOnlyList<CatalogItem> Items => Data.Value.Items;
    public static CertificateTemplate Basic => Data.Value.Templates[BasicKey];

    /// <summary>The template with this key, or null when there is none.</summary>
    public static CertificateTemplate? Find(string? key) =>
        key is not null && Data.Value.Templates.TryGetValue(key, out var template) ? template : null;

    /// <summary>
    /// Picks the template for a machine from the names that describe it (the request line first, then the registered
    /// item). The first name the catalogue knows wins.
    /// </summary>
    public static TemplateMatch Resolve(params string?[] names)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (Data.Value.ByName.TryGetValue(Normalize(name), out var item))
                return new TemplateMatch(Data.Value.Templates[item.Template], item);
        }

        return new TemplateMatch(Basic, null);
    }

    public static string Normalize(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private sealed record CatalogFileModel(int Version, List<CatalogItem> Items);

    private sealed record Loaded(
        IReadOnlyDictionary<string, CertificateTemplate> Templates,
        IReadOnlyList<CatalogItem> Items,
        IReadOnlyDictionary<string, CatalogItem> ByName);

    private static Loaded Load()
    {
        var assembly = typeof(CertificateTemplateCatalog).Assembly;
        var templates = new Dictionary<string, CertificateTemplate>();
        CatalogFileModel? catalog = null;
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(name => name.Contains(".Certificates.Templates.", StringComparison.Ordinal) &&
                                    name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            if (resource.EndsWith(CatalogFile, StringComparison.OrdinalIgnoreCase))
            {
                catalog = JsonSerializer.Deserialize<CatalogFileModel>(stream, JsonOptions);
                continue;
            }

            var template = JsonSerializer.Deserialize<CertificateTemplate>(stream, JsonOptions)
                ?? throw new InvalidOperationException($"Certificate template {resource} is empty.");
            templates[template.Key] = template;
        }

        if (catalog is null || !templates.ContainsKey(BasicKey))
            throw new InvalidOperationException("The certificate templates are missing from the application.");

        var byName = new Dictionary<string, CatalogItem>();
        foreach (var item in catalog.Items)
        {
            if (!templates.ContainsKey(item.Template))
                throw new InvalidOperationException($"Catalog item '{item.Name}' uses the unknown template '{item.Template}'.");
            foreach (var name in item.Aliases.Prepend(item.Name))
                byName[Normalize(name)] = item;
        }

        return new Loaded(templates, catalog.Items, byName);
    }
}
