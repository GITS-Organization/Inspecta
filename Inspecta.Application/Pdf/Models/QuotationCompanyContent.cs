using System.Text.Json;

namespace Inspecta.Application.Pdf.Models;

/// <summary>The addresses, phones, emails and website a quotation prints on its cover and in its footer.</summary>
public sealed record QuotationContacts(
    IReadOnlyList<string> Addresses,
    IReadOnlyList<string> Phones,
    IReadOnlyList<string> Emails,
    string Website)
{
    public static QuotationContacts Empty => new([], [], [], string.Empty);

    public bool IsEmpty => Addresses.Count == 0 && Phones.Count == 0 && Emails.Count == 0 && string.IsNullOrWhiteSpace(Website);
}

/// <summary>What a company can set about its quotation: the terms and conditions, the bank accounts and the contacts.</summary>
public sealed record QuotationCompanyContent(
    IReadOnlyList<QuotationTermsSection> Terms,
    IReadOnlyList<QuotationBankAccount> BankAccounts,
    QuotationContacts Contacts)
{
    public static QuotationCompanyContent Empty => new([], [], QuotationContacts.Empty);
}

/// <summary>
/// Everything a quotation prints that a company or a coordinator can change later, as it was when the quotation was sent. It is kept with the sent
/// quotation, so a download of it years later prints what the client was sent.
/// </summary>
public sealed record QuotationContentSnapshot(
    QuotationCompanyContent Content,
    string PreparedBy,
    string PreparedByTitle,
    string? PreparedByPhone,
    string? PreparedByEmail)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>The snapshot kept in <paramref name="json"/>, or <c>null</c> when there is none or it cannot be read (the quotation then prints as it did before snapshots).</summary>
    public static QuotationContentSnapshot? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var snapshot = JsonSerializer.Deserialize<QuotationContentSnapshot>(json, JsonOptions);
            return snapshot?.Content is null ? null : snapshot;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
