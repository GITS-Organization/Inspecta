using System.Text.RegularExpressions;
using System.Text.Json;

namespace Inspecta.Domain.Entities;

/// <summary>A titled list of terms on the quotation. <c>{days}</c> in a point is replaced with the validity in days; <c>**text**</c> is bold.</summary>
/// <param name="ShowBankAccounts">Print the bank accounts table in this section (at most one section may).</param>
public sealed record QuotationContentTerm(string? Title, IReadOnlyList<string>? Points, bool ShowBankAccounts = false);

/// <summary>A bank account printed in the terms of the quotation.</summary>
public sealed record QuotationContentBankAccount(
    string? Beneficiary,
    string? Bank,
    string? Currency = null,
    string? AccountNumber = null,
    string? Iban = null,
    string? Swift = null);

/// <summary>The addresses, phones and emails printed on the cover and in the footer of the quotation, and the website.</summary>
public sealed record QuotationContentContacts(
    IReadOnlyList<string>? Addresses = null,
    IReadOnlyList<string>? Phones = null,
    IReadOnlyList<string>? Emails = null,
    string? Website = null);

/// <summary>
/// What a company says about its quotations: the sections of its terms and conditions, its bank accounts and the contacts printed on the
/// cover and in the footer. One row per company, replaced as a whole. No row means the company never saved any: the built-in company then
/// prints its own built-in content and every other company prints none (no terms page, its profile contacts).
/// </summary>
public sealed class CompanyQuotationContent
{
    public const int MaxTermSections = 12;
    public const int MaxTermTitle = 120;
    public const int MaxPointsPerSection = 15;
    public const int MaxPoint = 600;
    public const int MaxBankAccounts = 5;
    public const int MaxBankName = 200;
    public const int MaxCurrency = 10;
    public const int MaxAccountNumber = 40;
    public const int MaxIban = 34;
    public const int MaxContacts = 3;
    public const int MaxAddress = 300;
    public const int MaxPhone = 40;
    public const int MaxEmail = 320;
    public const int MaxWebsite = 200;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex IbanShape = new("^[A-Za-z0-9 ]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SwiftShape = new("^([A-Za-z0-9]{8}|[A-Za-z0-9]{11})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private CompanyQuotationContent() { }

    /// <summary>The company, the same id as the tenant (<c>ManagerId</c>) everywhere else.</summary>
    public Guid CompanyId { get; private set; }

    public string TermsJson { get; private set; } = "[]";
    public string BankAccountsJson { get; private set; } = "[]";
    public string ContactsJson { get; private set; } = "{}";
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Who saved it last.</summary>
    public Guid? UpdatedById { get; private set; }

    public IReadOnlyList<QuotationContentTerm> Terms =>
        JsonSerializer.Deserialize<List<QuotationContentTerm>>(TermsJson, JsonOptions) ?? [];

    public IReadOnlyList<QuotationContentBankAccount> BankAccounts =>
        JsonSerializer.Deserialize<List<QuotationContentBankAccount>>(BankAccountsJson, JsonOptions) ?? [];

    public QuotationContentContacts Contacts =>
        JsonSerializer.Deserialize<QuotationContentContacts>(ContactsJson, JsonOptions) ?? new QuotationContentContacts();

    public static CompanyQuotationContent Create(Guid companyId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Company is required.", nameof(companyId));
        return new CompanyQuotationContent { CompanyId = companyId, UpdatedAt = DateTimeOffset.UtcNow };
    }

    /// <summary>
    /// Replaces everything. Text is trimmed and stripped of control characters, blank list entries are dropped, and anything over a limit
    /// throws an <see cref="ArgumentException"/> that says what to fix; nothing is changed then.
    /// </summary>
    public void Replace(
        IReadOnlyList<QuotationContentTerm>? terms,
        IReadOnlyList<QuotationContentBankAccount>? bankAccounts,
        QuotationContentContacts? contacts,
        Guid? updatedById,
        DateTimeOffset? now = null)
    {
        var cleanTerms = CleanTerms(terms ?? []);
        var cleanAccounts = CleanAccounts(bankAccounts ?? []);
        var cleanContacts = CleanContacts(contacts ?? new QuotationContentContacts());

        TermsJson = JsonSerializer.Serialize(cleanTerms, JsonOptions);
        BankAccountsJson = JsonSerializer.Serialize(cleanAccounts, JsonOptions);
        ContactsJson = JsonSerializer.Serialize(cleanContacts, JsonOptions);
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
        UpdatedById = updatedById;
    }

    private static List<QuotationContentTerm> CleanTerms(IReadOnlyList<QuotationContentTerm> terms)
    {
        if (terms.Count > MaxTermSections) throw new ArgumentException($"At most {MaxTermSections} terms sections can be given.");
        var result = new List<QuotationContentTerm>();
        foreach (var term in terms)
        {
            var title = Required(term?.Title, "A terms section title", MaxTermTitle);
            var points = (term!.Points ?? [])
                .Select(point => CompanyProfile.Optional(point, $"A point of \"{title}\"", MaxPoint))
                .Where(point => point is not null)
                .Select(point => point!)
                .ToList();
            if (points.Count > MaxPointsPerSection)
                throw new ArgumentException($"At most {MaxPointsPerSection} points can be given in the section \"{title}\".");
            result.Add(new QuotationContentTerm(title, points, term.ShowBankAccounts));
        }

        if (result.Count(term => term.ShowBankAccounts) > 1)
            throw new ArgumentException("Only one terms section can show the bank accounts.");
        return result;
    }

    private static List<QuotationContentBankAccount> CleanAccounts(IReadOnlyList<QuotationContentBankAccount> accounts)
    {
        if (accounts.Count > MaxBankAccounts) throw new ArgumentException($"At most {MaxBankAccounts} bank accounts can be given.");
        var result = new List<QuotationContentBankAccount>();
        foreach (var account in accounts)
        {
            var beneficiary = Required(account?.Beneficiary, "The beneficiary name of a bank account", MaxBankName);
            var bank = Required(account.Bank, $"The bank name of the account of \"{beneficiary}\"", MaxBankName);
            var currency = CompanyProfile.Optional(account.Currency, "The currency of a bank account", MaxCurrency) ?? string.Empty;
            var number = CompanyProfile.Optional(account.AccountNumber, "The account number", MaxAccountNumber) ?? string.Empty;
            var iban = CompanyProfile.Optional(account.Iban, "The IBAN", MaxIban) ?? string.Empty;
            var swift = CompanyProfile.Optional(account.Swift, "The SWIFT code", 11) ?? string.Empty;
            if (number.Length == 0 && iban.Length == 0)
                throw new ArgumentException($"The bank account of \"{beneficiary}\" needs an account number or an IBAN.");
            if (iban.Length > 0 && !IbanShape.IsMatch(iban))
                throw new ArgumentException("An IBAN can only have letters, digits and spaces.");
            if (swift.Length > 0 && !SwiftShape.IsMatch(swift))
                throw new ArgumentException("A SWIFT code has 8 or 11 letters or digits.");
            result.Add(new QuotationContentBankAccount(beneficiary, bank, currency, number, iban, swift));
        }

        return result;
    }

    private static QuotationContentContacts CleanContacts(QuotationContentContacts contacts)
    {
        var emails = List(contacts.Emails, "email", "emails", MaxEmail);
        if (emails.Any(email => !email.Contains('@', StringComparison.Ordinal)))
            throw new ArgumentException("Every email must contain @.");
        return new QuotationContentContacts(
            List(contacts.Addresses, "address", "addresses", MaxAddress),
            List(contacts.Phones, "phone", "phones", MaxPhone),
            emails,
            CompanyProfile.Optional(contacts.Website, "The website", MaxWebsite) ?? string.Empty);
    }

    private static List<string> List(IReadOnlyList<string>? values, string what, string plural, int maxLength)
    {
        var items = (values ?? [])
            .Select(value => CompanyProfile.Optional(value, $"An {what}", maxLength))
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (items.Count > MaxContacts) throw new ArgumentException($"At most {MaxContacts} {plural} can be given.");
        return items;
    }

    private static string Required(string? value, string field, int maxLength) =>
        CompanyProfile.Optional(value, field, maxLength) ?? throw new ArgumentException($"{field} is required.");
}
