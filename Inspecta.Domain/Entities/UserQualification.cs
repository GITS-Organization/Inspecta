using System.Text.Json;

namespace Inspecta.Domain.Entities;

public sealed record UserQualification(string Name, DateOnly? ValidUntil = null)
{
    public static UserQualification Create(string name, DateOnly? validUntil = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Qualification name is required.", nameof(name));

        return new UserQualification(name.Trim(), validUntil);
    }

    public static IReadOnlyList<UserQualification> ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Qualifications must be a JSON array.");

        var qualifications = new List<UserQualification>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                qualifications.Add(Create(item.GetString() ?? string.Empty));
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
                throw new JsonException("A qualification must be a string or object.");

            var name = item.TryGetProperty("name", out var nameProperty) &&
                nameProperty.ValueKind == JsonValueKind.String
                    ? nameProperty.GetString()
                    : null;
            DateOnly? validUntil = null;
            if (item.TryGetProperty("validUntil", out var dateProperty) &&
                dateProperty.ValueKind != JsonValueKind.Null)
            {
                if (dateProperty.ValueKind != JsonValueKind.String)
                    throw new JsonException("Qualification validUntil must be a valid date or null.");
                var dateText = dateProperty.GetString();
                if (DateOnly.TryParse(dateText, out var parsedDate))
                {
                    validUntil = parsedDate;
                }
                else if (DateTimeOffset.TryParse(dateText, out var parsedDateTime))
                {
                    validUntil = DateOnly.FromDateTime(parsedDateTime.DateTime);
                }
                else
                {
                    throw new JsonException("Qualification validUntil must be a valid date or null.");
                }
            }

            qualifications.Add(Create(name ?? string.Empty, validUntil));
        }

        return qualifications;
    }
}
