using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>
/// What a company says about its quotations (terms, bank accounts, contacts) and what a quotation prints from it and from its coordinator.
/// No saved row means the company never saved any: the built-in company then prints its built-in content (nothing is written to the
/// database for it), every other company prints none.
/// </summary>
public sealed class QuotationContentService(
    ICompanyQuotationContentRepository repository,
    ICompanyBrandingProvider brandingProvider,
    IUserRepository userRepository,
    ICompanyRepository? companies = null) : IQuotationContentService, IQuotationContentProvider
{
    public async Task<QuotationContentResponse> GetAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        var row = await repository.GetAsync(companyId, tracking: false, cancellationToken);
        if (row is not null) return await MapAsync(row, cancellationToken);

        var branding = await brandingProvider.GetAsync(companyId, cancellationToken);
        var content = QuotationEffectiveCompany.UsesBuiltInLayout(branding) ? BuiltInQuotationContent.Instance : QuotationCompanyContent.Empty;
        return new QuotationContentResponse(
            content.Terms.Select(term => new QuotationContentTerm(term.Title, term.Points, term.ShowBankAccounts)).ToArray(),
            content.BankAccounts.Select(account => new QuotationContentBankAccount(
                account.Beneficiary, account.Bank, account.Currency, account.AccountNumber, account.Iban, account.Swift)).ToArray(),
            new QuotationContentContacts(content.Contacts.Addresses, content.Contacts.Phones, content.Contacts.Emails, content.Contacts.Website),
            IsDefault: true,
            UpdatedAt: null,
            UpdatedBy: null);
    }

    public async Task<QuotationContentResponse> SaveAsync(
        Guid companyId, SaveQuotationContentRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        var row = await repository.GetAsync(companyId, tracking: true, cancellationToken);
        var isNew = row is null;
        row ??= CompanyQuotationContent.Create(companyId);

        // The domain checks every limit before it changes anything, so a bad request saves nothing.
        row.Replace(request.Terms, request.BankAccounts, request.Contacts, actorId);

        if (isNew) await repository.AddAsync(row, cancellationToken);
        else await repository.SaveAsync(cancellationToken);
        return await MapAsync(row, cancellationToken);
    }

    public async Task<QuotationContentSnapshot?> ForQuoteAsync(
        Quote quote, InspectionRequest request, AppUser? coordinator, CompanyBranding? branding, CancellationToken cancellationToken = default)
    {
        // Sent: exactly what it was sent with (null for one sent before this was kept: it prints as it always did). A draft prints what is true now.
        if (quote.IsSent) return QuotationContentSnapshot.FromJson(quote.CompanyContentJson);
        return await CaptureAsync(request, coordinator, branding, cancellationToken);
    }

    public async Task<QuotationContentSnapshot> CaptureAsync(
        InspectionRequest request, AppUser? coordinator, CompanyBranding? branding, CancellationToken cancellationToken = default)
    {
        var content = ResolveContacts(await LiveContentAsync(request.ManagerId, branding, cancellationToken), branding);
        return new QuotationContentSnapshot(
            content,
            coordinator?.FullName ?? string.Empty,
            string.IsNullOrWhiteSpace(coordinator?.JobTitle) ? QuotationDefaults.PreparedByTitle : coordinator.JobTitle,
            string.IsNullOrWhiteSpace(coordinator?.Phone) ? null : coordinator.Phone,
            // The coordinator's sign-in email, which is always there, not the mailbox they send from.
            string.IsNullOrWhiteSpace(coordinator?.Email) ? null : coordinator.Email);
    }

    // A company on the earlier layout prints its profile contacts for any contact it left empty. They are copied into what is kept with a sent
    // quotation, so it does not follow a later change of the profile. (The earlier layout printed only the first email, so only that is copied.)
    private static QuotationCompanyContent ResolveContacts(QuotationCompanyContent content, CompanyBranding? branding)
    {
        if (branding is null || QuotationEffectiveCompany.UsesBuiltInLayout(branding)) return content;
        var contacts = content.Contacts;
        var email = branding.Emails.FirstOrDefault();
        return content with
        {
            Contacts = new QuotationContacts(
                contacts.Addresses.Count > 0 ? contacts.Addresses : string.IsNullOrWhiteSpace(branding.Address) ? [] : [branding.Address],
                contacts.Phones.Count > 0 ? contacts.Phones : branding.Phones,
                contacts.Emails.Count > 0 ? contacts.Emails : string.IsNullOrWhiteSpace(email) ? [] : [email],
                string.IsNullOrWhiteSpace(contacts.Website) ? branding.Website : contacts.Website)
        };
    }

    private async Task<QuotationCompanyContent> LiveContentAsync(Guid? companyId, CompanyBranding? branding, CancellationToken cancellationToken)
    {
        var row = companyId is Guid id ? await repository.GetAsync(id, tracking: false, cancellationToken) : null;
        if (row is not null)
        {
            var contacts = row.Contacts;
            return new QuotationCompanyContent(
                row.Terms.Select(term => new QuotationTermsSection(term.Title ?? string.Empty, term.Points ?? [], term.ShowBankAccounts)).ToArray(),
                row.BankAccounts.Select(account => new QuotationBankAccount(
                    account.Beneficiary ?? string.Empty, account.Bank ?? string.Empty, account.Currency ?? string.Empty,
                    account.AccountNumber ?? string.Empty, account.Iban ?? string.Empty, account.Swift ?? string.Empty)).ToArray(),
                new QuotationContacts(contacts.Addresses ?? [], contacts.Phones ?? [], contacts.Emails ?? [], contacts.Website ?? string.Empty));
        }

        return QuotationEffectiveCompany.UsesBuiltInLayout(branding) ? BuiltInQuotationContent.Instance : QuotationCompanyContent.Empty;
    }

    private async Task EnsureCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (companies is not null && await companies.GetByIdAsync(companyId, tracking: false, cancellationToken) is null)
            throw new KeyNotFoundException("Company was not found.");
    }

    private async Task<QuotationContentResponse> MapAsync(CompanyQuotationContent row, CancellationToken cancellationToken)
    {
        QuotationContentAuthor? author = null;
        if (row.UpdatedById is Guid by)
        {
            var names = await userRepository.GetFullNamesAsync([by], cancellationToken);
            if (names.TryGetValue(by, out var name)) author = new QuotationContentAuthor(by, name);
        }

        var contacts = row.Contacts;
        return new QuotationContentResponse(
            row.Terms.Select(term => new QuotationContentTerm(term.Title, term.Points ?? [], term.ShowBankAccounts)).ToArray(),
            row.BankAccounts,
            new QuotationContentContacts(contacts.Addresses ?? [], contacts.Phones ?? [], contacts.Emails ?? [], contacts.Website ?? string.Empty),
            IsDefault: false,
            row.UpdatedAt,
            author);
    }
}
