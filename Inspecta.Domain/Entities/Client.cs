namespace Inspecta.Domain.Entities;

public sealed class Client
{
    private Client() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string CompanyName => Name;
    public string? Location { get; private set; }
    public string? CertificatesEmail { get; private set; }
    public string? TaxNumber { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public List<ClientContact> Contacts { get; private set; } = [];

    public static Client Create(
        string name,
        string? notes = null,
        string? location = null,
        string? certificatesEmail = null,
        string? taxNumber = null,
        double? latitude = null,
        double? longitude = null,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Client name is required.", nameof(name));
        ValidateCoordinates(latitude, longitude);
        return new Client
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Location = location?.Trim(),
            CertificatesEmail = EmptyToNull(certificatesEmail),
            TaxNumber = EmptyToNull(taxNumber),
            Latitude = latitude,
            Longitude = longitude,
            Notes = notes?.Trim(),
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    public void UpdateDetails(
        string name,
        string? location,
        string? certificatesEmail,
        string? taxNumber,
        double? latitude,
        double? longitude,
        string? notes)
    {
        EnsureNotArchived();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Client name is required.", nameof(name));
        ValidateCoordinates(latitude, longitude);

        Name = name.Trim();
        Location = location?.Trim();
        CertificatesEmail = EmptyToNull(certificatesEmail);
        TaxNumber = EmptyToNull(taxNumber);
        Latitude = latitude;
        Longitude = longitude;
        Notes = notes?.Trim();
    }

    public void ReplaceContacts(IEnumerable<ClientContact> contacts)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(contacts);
        var values = contacts.ToArray();
        if (values.Any(contact => contact.ClientId != Id))
            throw new ArgumentException("All contacts must belong to this client.", nameof(contacts));
        if (values.Count(contact => contact.IsPrimary) > 1)
            throw new InvalidOperationException("A client can have only one primary contact.");

        Contacts.Clear();
        Contacts.AddRange(values);
    }

    public void Archive()
    {
        ArchivedAt ??= DateTimeOffset.UtcNow;
    }

    private void EnsureNotArchived()
    {
        if (ArchivedAt is not null)
            throw new InvalidOperationException("Archived clients cannot be edited.");
    }

    private static void ValidateCoordinates(double? latitude, double? longitude)
    {
        if (latitude.HasValue != longitude.HasValue)
            throw new ArgumentException("Latitude and longitude must both be provided or both be empty.");
        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude));
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}