namespace Inspecta.Domain.Entities;

public sealed record CertificateIssueSnapshot(
    string ClientName,
    string Location,
    string Item,
    string? SerialNumber = null)
{
    public static CertificateIssueSnapshot Create(string clientName, string location, string item, string? serialNumber = null)
    {
        if (string.IsNullOrWhiteSpace(clientName))
            throw new ArgumentException("Client name is required.", nameof(clientName));
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location is required.", nameof(location));
        if (string.IsNullOrWhiteSpace(item))
            throw new ArgumentException("Item description is required.", nameof(item));

        return new CertificateIssueSnapshot(
            clientName.Trim(),
            location.Trim(),
            item.Trim(),
            string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim());
    }
}
