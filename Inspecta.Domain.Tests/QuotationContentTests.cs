using System.Text.Json;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Pdf.Templates;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Step 2 and 3 of the company quotation content: a company edits the terms, bank accounts and contacts of its quotations, a coordinator sets the
/// job title and phone of their signature, and a quotation that was sent keeps printing what it was sent with.
/// </summary>
public sealed class QuotationContentTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Guid CompanyId = QuotationTests.ManagerId;

    private static CompanyBranding NourBranding() => new(
        "Nour Inspection", "Nour Inspection", "Nour Inspection", "", "12 Nile St", ["+20 1"], ["info@nour.example"], "nour.example", "", null, null, "#112233", "#445566");

    private sealed class FakeContentRepository : ICompanyQuotationContentRepository
    {
        public Dictionary<Guid, CompanyQuotationContent> Rows { get; } = [];

        public Task<CompanyQuotationContent?> GetAsync(Guid companyId, bool tracking, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.GetValueOrDefault(companyId));

        public Task AddAsync(CompanyQuotationContent content, CancellationToken cancellationToken = default)
        {
            Rows[content.CompanyId] = content;
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeBranding(CompanyBranding? branding) : ICompanyBrandingProvider
    {
        public Task<CompanyBranding?> GetAsync(Guid? managerId, CancellationToken cancellationToken = default) => Task.FromResult(branding);
    }

    private sealed class FakeCompanies(params Guid[] ids) : ICompanyRepository
    {
        public Task<Company?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(ids.Contains(id) ? Company.Create("Test company", id: id) : null);

        public Task<IReadOnlyDictionary<Guid, Company>> GetByIdsAsync(IReadOnlyCollection<Guid> ids2, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Company>>(new Dictionary<Guid, Company>());

        public Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Company>>([]);
        public Task AddAsync(Company company, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Setup
    {
        public FakeContentRepository Repository { get; } = new();
        public QuotationContentService Content { get; }
        public QuotationTests.Fixture Fixture { get; }

        public Setup(CompanyBranding? branding = null)
        {
            var users = new IncomingEmailRequestTests.FakeUserRepository([QuotationTests.Coordinator]);
            var provider = new FakeBranding(branding);
            Content = new QuotationContentService(Repository, provider, users);
            Fixture = QuotationTests.Fixture.WithContent(Content, companyBranding: provider);
        }

        public Task<QuotationContentResponse> Save(string term, string account = "123") => Content.SaveAsync(
            CompanyId,
            new SaveQuotationContentRequest
            {
                Terms = [new QuotationContentTerm(term, ["Payment within {days} days."]), new QuotationContentTerm("Bank", [], ShowBankAccounts: true)],
                BankAccounts = [new QuotationContentBankAccount("Integrity International", "CIB", "EGP", account)],
                Contacts = new QuotationContentContacts(["Addr " + term], ["+20 100"], ["offer@" + term.ToLowerInvariant() + ".example"], "site.example")
            },
            QuotationTests.CoordinatorId);

        /// <summary>A draft with both machines priced.</summary>
        public async Task<QuoteResponse> PricedDraft()
        {
            var request = Fixture.Request;
            var quote = await Fixture.Service.CreateAsync(request.Id, QuotationTests.CoordinatorId, "coordinator", new CreateQuoteRequest());
            return await Fixture.Service.UpdateAsync(request.Id, quote.Id, QuotationTests.CoordinatorId, "coordinator", new UpdateQuoteRequest
            {
                Items =
                [
                    new UpdateQuoteItem { RequestItemId = Fixture.Crane.Id, UnitPricePiastres = 1_200_000 },
                    new UpdateQuoteItem { RequestItemId = Fixture.Receiver.Id, UnitPricePiastres = 850_000 }
                ]
            });
        }

        public async Task<QuotationPdfModel> PdfModel(Guid quoteId)
        {
            await Fixture.Service.GetPdfAsync(Fixture.Request.Id, quoteId, QuotationTests.CoordinatorId, "coordinator");
            return Fixture.Pdf.LastModel!;
        }

        public async Task<Quote> Entity(Guid quoteId) => (await Fixture.Quotes.GetByIdAsync(quoteId))!;
    }

    private static string Of(object? value) => JsonSerializer.Serialize(value, Json);

    // ---- What a company prints ---------------------------------------------------------------------------------------

    [Fact]
    public async Task ACompanyThatSavesTermsAndBankAccountsGetsTheTermsPage()
    {
        var setup = new Setup(NourBranding());
        var draft = await setup.PricedDraft();
        Assert.Empty((await setup.PdfModel(draft.Id)).Content!.Terms);

        await setup.Save("Nour");
        var model = await setup.PdfModel(draft.Id);

        Assert.Equal(["Nour", "Bank"], model.Content!.Terms.Select(term => term.Title));
        Assert.Equal("123", Assert.Single(model.Content.BankAccounts).AccountNumber);
        var company = QuotationEffectiveCompany.Of(model, DefaultCompanyProfile.Instance);
        Assert.Equal(2, company.Terms.Count);
        Assert.Equal("Addr Nour", company.Address);
    }

    [Fact]
    public async Task ACompanyWithoutSavedContentHasNoTermsAndIntegrityHasItsBuiltInContent()
    {
        var other = new Setup(NourBranding());
        var otherModel = await other.PdfModel((await other.PricedDraft()).Id);
        Assert.Equal(Of(QuotationCompanyContent.Empty), Of(otherModel.Content));

        var builtIn = new Setup();
        var builtInModel = await builtIn.PdfModel((await builtIn.PricedDraft()).Id);
        Assert.Equal(Of(BuiltInQuotationContent.Instance), Of(builtInModel.Content));
    }

    [Fact]
    public async Task IntegritysSavedContentReplacesTheBuiltInContent()
    {
        var setup = new Setup();
        var draft = await setup.PricedDraft();

        await setup.Save("Integrity");
        var model = await setup.PdfModel(draft.Id);

        Assert.Equal(["Integrity", "Bank"], model.Content!.Terms.Select(term => term.Title));
        var company = QuotationEffectiveCompany.Of(model, DefaultCompanyProfile.Instance);
        Assert.Equal(["Addr Integrity"], company.Addresses);
        Assert.Equal(["offer@integrity.example"], company.Emails);
        Assert.Equal("site.example", company.Website);
        Assert.DoesNotContain(company.Terms, term => term.Title == "Offer Validity");
    }

    [Fact]
    public async Task TheContentOfAnUnsavedCompanyIsTheDefaultAndOfASavedOneIsMarkedAsSaved()
    {
        var setup = new Setup();

        var before = await setup.Content.GetAsync(CompanyId);
        Assert.True(before.IsDefault);
        Assert.Equal(Of(BuiltInQuotationContent.Instance.Terms.Select(term => new QuotationContentTerm(term.Title, term.Points, term.ShowBankAccounts))), Of(before.Terms));
        Assert.Null(before.UpdatedAt);

        var after = await setup.Save("Saved");
        Assert.False(after.IsDefault);
        Assert.Equal(["Saved", "Bank"], after.Terms.Select(term => term.Title));
        Assert.False((await setup.Content.GetAsync(CompanyId)).IsDefault);

        var other = new Setup(NourBranding());
        var nothing = await other.Content.GetAsync(CompanyId);
        Assert.True(nothing.IsDefault);
        Assert.Empty(nothing.Terms);
        Assert.Empty(nothing.BankAccounts);
    }

    [Fact]
    public async Task SavingRecordsWhoChangedTheContentAndWhen()
    {
        var setup = new Setup();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var saved = await setup.Save("Mine");

        Assert.InRange(saved.UpdatedAt!.Value, before, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(QuotationTests.CoordinatorId, saved.UpdatedBy!.Id);
        Assert.Equal("Eng. Ahmed", saved.UpdatedBy.FullName);
        Assert.Equal(QuotationTests.CoordinatorId, setup.Repository.Rows[CompanyId].UpdatedById);

        var later = await setup.Save("Again");
        Assert.True(later.UpdatedAt >= saved.UpdatedAt);
        Assert.Single(setup.Repository.Rows);
    }

    [Fact]
    public async Task AnUnknownCompanyIsNotFoundAndSavesNothing()
    {
        var content = new QuotationContentService(
            new FakeContentRepository(), new FakeBranding(null), new IncomingEmailRequestTests.FakeUserRepository(), new FakeCompanies(CompanyId));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => content.GetAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => content.SaveAsync(Guid.NewGuid(), new SaveQuotationContentRequest(), CompanyId));
        Assert.True((await content.GetAsync(CompanyId)).IsDefault);
    }

    // ---- The limits ---------------------------------------------------------------------------------------------------

    private static QuotationContentTerm Term(string title = "Payment", int points = 1, bool bank = false, int pointLength = 10) =>
        new(title, Enumerable.Range(0, points).Select(_ => new string('p', pointLength)).ToArray(), bank);

    private static QuotationContentBankAccount Account(
        string? beneficiary = "Nour", string? bank = "NBE", string? currency = "EGP", string? number = "123", string? iban = null, string? swift = null) =>
        new(beneficiary, bank, currency, number, iban, swift);

    public static TheoryData<string, SaveQuotationContentRequest> Breaking => new()
    {
        { "13 sections", new SaveQuotationContentRequest { Terms = Enumerable.Range(0, 13).Select(index => Term("S" + index)).ToArray() } },
        { "title too long", new SaveQuotationContentRequest { Terms = [Term(new string('t', 121))] } },
        { "empty title", new SaveQuotationContentRequest { Terms = [Term("  ")] } },
        { "16 points", new SaveQuotationContentRequest { Terms = [Term(points: 16)] } },
        { "point too long", new SaveQuotationContentRequest { Terms = [Term(pointLength: 601)] } },
        { "two bank sections", new SaveQuotationContentRequest { Terms = [Term("A", bank: true), Term("B", bank: true)] } },
        { "6 accounts", new SaveQuotationContentRequest { BankAccounts = Enumerable.Range(0, 6).Select(_ => Account()).ToArray() } },
        { "no beneficiary", new SaveQuotationContentRequest { BankAccounts = [Account(beneficiary: " ")] } },
        { "no bank", new SaveQuotationContentRequest { BankAccounts = [Account(bank: null)] } },
        { "bank name too long", new SaveQuotationContentRequest { BankAccounts = [Account(bank: new string('b', 201))] } },
        { "currency too long", new SaveQuotationContentRequest { BankAccounts = [Account(currency: "EGPEGPEGPEGP")] } },
        { "no number and no iban", new SaveQuotationContentRequest { BankAccounts = [Account(number: null)] } },
        { "number too long", new SaveQuotationContentRequest { BankAccounts = [Account(number: new string('1', 41))] } },
        { "iban too long", new SaveQuotationContentRequest { BankAccounts = [Account(number: null, iban: new string('A', 35))] } },
        { "iban with symbols", new SaveQuotationContentRequest { BankAccounts = [Account(iban: "EG64-0010")] } },
        { "short swift", new SaveQuotationContentRequest { BankAccounts = [Account(swift: "CIBE")] } },
        { "swift of 9", new SaveQuotationContentRequest { BankAccounts = [Account(swift: "CIBEEGCX0")] } },
        { "4 addresses", new SaveQuotationContentRequest { Contacts = new QuotationContentContacts(Addresses: ["a", "b", "c", "d"]) } },
        { "address too long", new SaveQuotationContentRequest { Contacts = new QuotationContentContacts(Addresses: [new string('a', 301)]) } },
        { "4 phones", new SaveQuotationContentRequest { Contacts = new QuotationContentContacts(Phones: ["1", "2", "3", "4"]) } },
        { "phone too long", new SaveQuotationContentRequest { Contacts = new QuotationContentContacts(Phones: [new string('1', 41)]) } },
        { "4 emails", new SaveQuotationContentRequest { Contacts = new QuotationContentContacts(Emails: ["a@x", "b@x", "c@x", "d@x"]) } },
        { "email without @", new SaveQuotationContentRequest { Contacts = new QuotationContentContacts(Emails: ["nobody.example"]) } },
        { "website too long", new SaveQuotationContentRequest { Contacts = new QuotationContentContacts(Website: new string('w', 201)) } }
    };

    [Theory]
    [MemberData(nameof(Breaking))]
    public async Task QuotationContentRejectsWhatBreaksTheLimits(string what, SaveQuotationContentRequest request)
    {
        var setup = new Setup();
        await setup.Save("Kept");

        var failure = await Assert.ThrowsAsync<ArgumentException>(() => setup.Content.SaveAsync(CompanyId, request, QuotationTests.CoordinatorId));

        Assert.False(string.IsNullOrWhiteSpace(failure.Message), what);
        // Nothing was changed: the content that was saved before is still there.
        Assert.Equal("Kept", (await setup.Content.GetAsync(CompanyId)).Terms[0].Title);
    }

    [Fact]
    public async Task TheLimitsThemselvesAreAccepted()
    {
        var setup = new Setup();

        var saved = await setup.Content.SaveAsync(CompanyId, new SaveQuotationContentRequest
        {
            Terms = Enumerable.Range(0, 12).Select(index => Term(new string('t', 120), points: 15, bank: index == 0, pointLength: 600)).Select((term, index) => term with { ShowBankAccounts = index == 0 }).ToArray(),
            BankAccounts = Enumerable.Range(0, 5).Select(index => Account(number: new string('1', 40), iban: "EG64 0010 0089 0000 0100 0728 5319 3".Replace(" ", string.Empty)[..29], swift: index % 2 == 0 ? "CIBEEGCX" : "CIBEEGCX089")).ToArray(),
            Contacts = new QuotationContentContacts(["a", "b", "c"], ["1", "2", "3"], ["a@x.example", "b@x.example", "c@x.example"], new string('w', 200))
        }, QuotationTests.CoordinatorId);

        Assert.Equal(12, saved.Terms.Count);
        Assert.Equal(5, saved.BankAccounts.Count);
        Assert.Equal(3, saved.Contacts.Emails!.Count);
    }

    [Fact]
    public async Task TextIsTrimmedAndStrippedOfControlCharactersAndBlankEntriesAreDropped()
    {
        var setup = new Setup();

        var saved = await setup.Content.SaveAsync(CompanyId, new SaveQuotationContentRequest
        {
            Terms = [new QuotationContentTerm("  Pay\u0007ment ", ["  one\n ", "   ", "two"])],
            Contacts = new QuotationContentContacts(Phones: ["  +20 1 ", " ", "+20 1"])
        }, QuotationTests.CoordinatorId);

        Assert.Equal("Payment", saved.Terms[0].Title);
        Assert.Equal(["one", "two"], saved.Terms[0].Points);
        Assert.Equal(["+20 1"], saved.Contacts.Phones);
    }

    // ---- A sent quotation keeps what it was sent with ---------------------------------------------------------------

    [Fact]
    public async Task ASentQuotationKeepsTheContentItWasSentWith()
    {
        var setup = new Setup();
        QuotationTests.Coordinator.SetSignatureDetails("Senior Coordinator", "+20 100 111 2222");
        try
        {
            await setup.Save("First");
            var draft = await setup.PricedDraft();
            await setup.Fixture.Service.SendAsync(setup.Fixture.Request.Id, draft.Id, QuotationTests.CoordinatorId, "coordinator");

            // The email that went out was drawn from the same values that are now kept with the quotation.
            var emailed = setup.Fixture.Pdf.LastModel!;
            Assert.Single(setup.Fixture.Sender.Sent);
            Assert.Equal(["First", "Bank"], emailed.Content!.Terms.Select(term => term.Title));
            Assert.Equal("Senior Coordinator", emailed.PreparedByTitle);
            Assert.Equal("+20 100 111 2222", emailed.PreparedByPhone);
            Assert.Equal("coordinator@example.com", emailed.PreparedByEmail);

            // The company changes its terms and the coordinator her title and phone afterwards.
            await setup.Save("Second", account: "999");
            QuotationTests.Coordinator.SetSignatureDetails("Manager", "+20 999");

            var model = await setup.PdfModel(draft.Id);
            Assert.Equal(["First", "Bank"], model.Content!.Terms.Select(term => term.Title));
            Assert.Equal("123", model.Content.BankAccounts[0].AccountNumber);
            Assert.Equal(["Addr First"], model.Content.Contacts.Addresses);
            Assert.Equal("Senior Coordinator", model.PreparedByTitle);
            Assert.Equal("+20 100 111 2222", model.PreparedByPhone);
            Assert.Equal(Of(emailed.Content), Of(model.Content));
            Assert.NotNull((await setup.Entity(draft.Id)).CompanyContentJson);
        }
        finally
        {
            QuotationTests.Coordinator.SetSignatureDetails(null, null);
        }
    }

    [Fact]
    public async Task AQuotationSentBeforeThisChangePrintsAsBefore()
    {
        var setup = new Setup();
        var draft = await setup.PricedDraft();
        // Sent the way it was before snapshots existed: nothing was kept with it.
        (await setup.Entity(draft.Id)).Send();
        await setup.Save("Edited");
        QuotationTests.Coordinator.SetSignatureDetails("Manager", "+20 999");
        try
        {
            var model = await setup.PdfModel(draft.Id);

            Assert.Null(model.Content);
            Assert.Equal("Coordinator", model.PreparedByTitle);
            Assert.Null(model.PreparedByPhone);
            Assert.Null(model.PreparedByEmail);
            // Without content the proposal prints the built-in terms, bank accounts and contacts, as it always did.
            var company = QuotationEffectiveCompany.Of(model, DefaultCompanyProfile.Instance);
            Assert.Equal(Of(BuiltInQuotationContent.Instance.Terms), Of(company.Terms));
        }
        finally
        {
            QuotationTests.Coordinator.SetSignatureDetails(null, null);
        }
    }

    [Fact]
    public async Task AnotherCompanysQuotationSentBeforeThisChangeStillHasNoTermsPage()
    {
        var setup = new Setup(NourBranding());
        var draft = await setup.PricedDraft();
        (await setup.Entity(draft.Id)).Send();
        await setup.Save("Edited");

        var model = await setup.PdfModel(draft.Id);

        Assert.Null(model.Content);
        Assert.Empty(QuotationEffectiveCompany.Of(model, DefaultCompanyProfile.Instance).Terms);
    }

    [Fact]
    public async Task ADraftAlwaysShowsTheLiveContent()
    {
        var setup = new Setup();
        var draft = await setup.PricedDraft();

        await setup.Save("One");
        Assert.Equal("One", (await setup.PdfModel(draft.Id)).Content!.Terms[0].Title);

        await setup.Save("Two");
        QuotationTests.Coordinator.SetSignatureDetails("Lead", null);
        try
        {
            var model = await setup.PdfModel(draft.Id);
            Assert.Equal("Two", model.Content!.Terms[0].Title);
            Assert.Equal("Lead", model.PreparedByTitle);
        }
        finally
        {
            QuotationTests.Coordinator.SetSignatureDetails(null, null);
        }

        Assert.Null((await setup.Entity(draft.Id)).CompanyContentJson);
    }

    [Fact]
    public async Task AFailedSendStoresNoSnapshotAndTheRetryTakesTheCurrentContent()
    {
        var setup = new Setup();
        await setup.Save("Before");
        var draft = await setup.PricedDraft();
        setup.Fixture.Sender.Failure = new Application.Exceptions.EmailDeliveryException("The mail server said no.");

        await Assert.ThrowsAsync<Application.Exceptions.EmailDeliveryException>(() =>
            setup.Fixture.Service.SendAsync(setup.Fixture.Request.Id, draft.Id, QuotationTests.CoordinatorId, "coordinator"));
        var entity = await setup.Entity(draft.Id);
        Assert.False(entity.IsSent);
        Assert.Null(entity.CompanyContentJson);

        await setup.Save("After");
        setup.Fixture.Sender.Failure = null;
        await setup.Fixture.Service.SendAsync(setup.Fixture.Request.Id, draft.Id, QuotationTests.CoordinatorId, "coordinator");

        var snapshot = QuotationContentSnapshot.FromJson((await setup.Entity(draft.Id)).CompanyContentJson);
        Assert.Equal("After", snapshot!.Content.Terms[0].Title);
        Assert.Equal("After", setup.Fixture.Pdf.LastModel!.Content!.Terms[0].Title);
    }

    [Fact]
    public async Task AcceptingAnUnsentQuotationStoresTheSnapshot()
    {
        var setup = new Setup();
        await setup.Save("Accepted");
        var draft = await setup.PricedDraft();

        await setup.Fixture.Service.AcceptAsync(setup.Fixture.Request.Id, draft.Id, QuotationTests.CoordinatorId, "coordinator");

        var snapshot = QuotationContentSnapshot.FromJson((await setup.Entity(draft.Id)).CompanyContentJson);
        Assert.Equal("Accepted", snapshot!.Content.Terms[0].Title);
        Assert.Equal("Coordinator", snapshot.PreparedByTitle);
        Assert.Equal("Accepted", setup.Fixture.Pdf.LastModel!.Content!.Terms[0].Title);

        // Later edits do not reach it.
        await setup.Save("Later");
        Assert.Equal("Accepted", (await setup.PdfModel(draft.Id)).Content!.Terms[0].Title);
    }

    [Fact]
    public async Task AcceptingAQuotationThatWasAlreadySentDoesNotReplaceItsSnapshot()
    {
        var setup = new Setup();
        await setup.Save("Sent");
        var draft = await setup.PricedDraft();
        await setup.Fixture.Service.SendAsync(setup.Fixture.Request.Id, draft.Id, QuotationTests.CoordinatorId, "coordinator");
        var kept = (await setup.Entity(draft.Id)).CompanyContentJson;

        await setup.Save("Edited");
        await setup.Fixture.Service.AcceptAsync(setup.Fixture.Request.Id, draft.Id, QuotationTests.CoordinatorId, "coordinator");

        Assert.Equal(kept, (await setup.Entity(draft.Id)).CompanyContentJson);
        Assert.Equal("Sent", setup.Fixture.Pdf.LastModel!.Content!.Terms[0].Title);
    }

    // ---- The signature (step 3) ----------------------------------------------------------------------------------------

    [Fact]
    public void TheSignatureShowsTheCoordinatorsJobTitlePhoneAndEmail()
    {
        var model = QuotationCompanyContentTests.Model();

        Assert.Equal(["Coordinator"], QuotationPdfTemplate.SignatureLines(model with { PreparedByTitle = "" }));
        Assert.Equal(["Coordinator"], QuotationPdfTemplate.SignatureLines(model with { PreparedByTitle = "  " }));
        Assert.Equal(["Senior Coordinator", "+20 100", "a@example.com"],
            QuotationPdfTemplate.SignatureLines(model with { PreparedByTitle = "Senior Coordinator", PreparedByPhone = "+20 100", PreparedByEmail = "a@example.com" }));
        Assert.Equal(["Coordinator", "a@example.com"],
            QuotationPdfTemplate.SignatureLines(model with { PreparedByTitle = "Coordinator", PreparedByPhone = " ", PreparedByEmail = "a@example.com" }));
    }

    [Fact]
    public async Task TheSnapshotOfADraftUsesTheTitleOrCoordinatorAndOnlyThePhoneThereIs()
    {
        var setup = new Setup();
        var draft = await setup.PricedDraft();

        var plain = await setup.PdfModel(draft.Id);
        Assert.Equal("Coordinator", plain.PreparedByTitle);
        Assert.Null(plain.PreparedByPhone);
        Assert.Equal("coordinator@example.com", plain.PreparedByEmail);

        QuotationTests.Coordinator.SetSignatureDetails("Senior Coordinator", "+20 100");
        try
        {
            var set = await setup.PdfModel(draft.Id);
            Assert.Equal("Senior Coordinator", set.PreparedByTitle);
            Assert.Equal("+20 100", set.PreparedByPhone);
        }
        finally
        {
            QuotationTests.Coordinator.SetSignatureDetails(null, null);
        }
    }

    [Fact]
    public void AUserSetsAJobTitleAndPhoneWithinTheLimitsAndEmptyClearsThem()
    {
        var user = AppUser.Create("someone@example.com", "Someone", Domain.Enums.UserRole.Coordinator, phone: "+20 1");

        user.SetSignatureDetails("  Senior\u0007 Coordinator ", " +20 100 ");
        Assert.Equal("Senior Coordinator", user.JobTitle);
        Assert.Equal("+20 100", user.Phone);

        Assert.Throws<ArgumentException>(() => user.SetSignatureDetails(new string('t', 101), null));
        Assert.Throws<ArgumentException>(() => user.SetSignatureDetails(null, new string('1', 41)));
        Assert.Equal("Senior Coordinator", user.JobTitle);

        user.SetSignatureDetails("", " ");
        Assert.Null(user.JobTitle);
        Assert.Null(user.Phone);
    }

    // ---- Who may edit -----------------------------------------------------------------------------------------------------

    [Fact]
    public void OnlyTechnicalManagersCompanyAdminsAndThePlatformOwnerMayEditQuotationContent()
    {
        Assert.Equal(
            new[] { RoleNames.Admin, RoleNames.CompanyAdmin, RoleNames.TechnicalManager }.Order(),
            RolePermissions.RolesWith(Permissions.CompanyEditQuotationContent).Order());
        foreach (var role in new[] { RoleNames.Coordinator, RoleNames.Inspector, RoleNames.FinancialManager, RoleNames.GeneralManager })
            Assert.False(RolePermissions.Has(role, Permissions.CompanyEditQuotationContent), role);
    }
}
