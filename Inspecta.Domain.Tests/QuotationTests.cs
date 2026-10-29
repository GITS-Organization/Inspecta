using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class QuotationTests
{
    internal static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly AppUser Coordinator = AppUser.Create("coordinator@example.com", "Eng. Ahmed", UserRole.Coordinator, managerId: ManagerId);
    internal static readonly Guid CoordinatorId = Coordinator.Id;

    [Fact]
    public async Task CreatingAQuoteCopiesEveryRequestItemWithoutPricesAndMovesTheRequestToDraft()
    {
        var fixture = new Fixture();

        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        Assert.Equal(QuoteStatus.Open, quote.Status);
        Assert.Equal(1, quote.Version);
        Assert.Equal(14, quote.ValidForDays);
        Assert.StartsWith("QT-", quote.ProposalNumber, StringComparison.Ordinal);
        Assert.EndsWith("rev01", quote.ProposalNumber, StringComparison.Ordinal);
        Assert.Equal(2, quote.Items!.Count);
        Assert.All(quote.Items, item => Assert.Null(item.UnitPricePiastres));
        Assert.Equal("13351021", quote.Items.Single(item => item.RequestItemId == fixture.Crane.Id).PrNumber);
        Assert.Equal(fixture.Request.Items.Select(item => item.Id).Order(), quote.MissingPrices!.Order());
        Assert.Equal(5, quote.TotalUnits);
        Assert.True(quote.CanEdit);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
    }

    [Fact]
    public async Task OnlyOneQuoteCanBeInPreparationAtATime()
    {
        var fixture = new Fixture();
        await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest()));
    }

    [Fact]
    public async Task AnIncompleteRequestCannotBeQuoted()
    {
        var incomplete = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());
        var fixture = new Fixture(incomplete);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateAsync(incomplete.Id, CoordinatorId, "coordinator", new CreateQuoteRequest()));
        Assert.Equal(JobStatus.RequestReceived, incomplete.Status);
    }

    [Fact]
    public async Task SavingTheDraftStoresPricesTotalsTextsAndValidity()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        var saved = await fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000) with
            {
                ValidForDays = 30,
                ScopeOfWork = ["Load test", "  ", "Visual inspection"],
                Notes = ["Pricing excludes VAT."],
                Message = "  Please find our offer.  "
            });

        Assert.Equal(1_200_000 + 4 * 850_000, saved.AmountPiastres);
        Assert.Empty(saved.MissingPrices!);
        Assert.Equal(30, saved.ValidForDays);
        Assert.Equal(["Load test", "Visual inspection"], saved.ScopeOfWork);
        Assert.Equal(["Pricing excludes VAT."], saved.Notes);
        Assert.Equal("Please find our offer.", saved.Message);
        Assert.Equal(850_000, saved.Items!.Single(item => item.RequestItemId == fixture.Receiver.Id).UnitPricePiastres);
        Assert.Equal(3_400_000, saved.Items!.Single(item => item.RequestItemId == fixture.Receiver.Id).TotalPiastres);
    }

    [Fact]
    public async Task ADraftMayKeepSomePricesEmptyButEveryItemMustBePresent()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        var partial = await fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, null));
        Assert.Equal([fixture.Receiver.Id], partial.MissingPrices!);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator",
            new UpdateQuoteRequest { Items = [new UpdateQuoteItem { RequestItemId = fixture.Crane.Id, UnitPricePiastres = 1 }] }));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator",
            new UpdateQuoteRequest
            {
                Items =
                [
                    new UpdateQuoteItem { RequestItemId = fixture.Crane.Id, UnitPricePiastres = 1 },
                    new UpdateQuoteItem { RequestItemId = fixture.Crane.Id, UnitPricePiastres = 2 },
                    new UpdateQuoteItem { RequestItemId = fixture.Receiver.Id, UnitPricePiastres = 3 }
                ]
            }));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator",
            Update(fixture, 1, 2) with
            {
                Items = [.. Update(fixture, 1, 2).Items, new UpdateQuoteItem { RequestItemId = Guid.NewGuid(), UnitPricePiastres = 5 }]
            }));
    }

    [Fact]
    public async Task AnItemAddedToTheRequestAfterwardsMustBePricedToo()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        fixture.Request.AddItem(RequestItem.Create("Safety valve DN40", 6));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1, 2)));

        Assert.Contains("Safety valve DN40", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendingNeedsEveryPriceAndAClientAndThenFreezesTheDraft()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, null));

        var unpriced = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator"));
        Assert.Contains("Air receiver", unpriced.Message, StringComparison.Ordinal);

        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        var sent = await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.Equal(QuoteStatus.Open, sent.Status);
        Assert.NotNull(sent.SentAt);
        Assert.False(sent.CanEdit);
        Assert.Equal(JobStatus.QuoteSent, fixture.Request.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1, 2)));
    }

    [Fact]
    public async Task ASentQuoteIsClosedAsAcceptedOrRejected()
    {
        var accepted = await SentFixtureAsync();
        var result = await accepted.Fixture.Service.RecordDecisionAsync(
            accepted.Fixture.Request.Id, accepted.QuoteId, CoordinatorId, "coordinator",
            new QuoteDecisionRequest { Decision = QuoteStatus.Accepted });
        Assert.Equal(QuoteStatus.Accepted, result.Status);
        Assert.Equal(JobStatus.QuoteAccepted, accepted.Fixture.Request.Status);

        var rejected = await SentFixtureAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => rejected.Fixture.Service.RecordDecisionAsync(
            rejected.Fixture.Request.Id, rejected.QuoteId, CoordinatorId, "coordinator",
            new QuoteDecisionRequest { Decision = QuoteStatus.Rejected }));
        await Assert.ThrowsAsync<ArgumentException>(() => rejected.Fixture.Service.RecordDecisionAsync(
            rejected.Fixture.Request.Id, rejected.QuoteId, CoordinatorId, "coordinator",
            new QuoteDecisionRequest { Decision = QuoteStatus.Open }));
        var closed = await rejected.Fixture.Service.RecordDecisionAsync(
            rejected.Fixture.Request.Id, rejected.QuoteId, CoordinatorId, "coordinator",
            new QuoteDecisionRequest { Decision = QuoteStatus.Rejected, Note = "Too expensive" });
        Assert.Equal(QuoteStatus.Rejected, closed.Status);
        Assert.Equal("Too expensive", closed.ClientReply);
        Assert.Equal(JobStatus.QuoteRejected, rejected.Fixture.Request.Status);
    }

    [Fact]
    public async Task OtherCoordinatorsCannotReadOrChangeTheQuotesOfAnEmailRequest()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        var other = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.ListAsync(fixture.Request.Id, other, "coordinator"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.GetAsync(fixture.Request.Id, quote.Id, other, "coordinator"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.GetPdfAsync(fixture.Request.Id, quote.Id, other, "coordinator"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.CreateAsync(fixture.Request.Id, other, "coordinator", new CreateQuoteRequest()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, other, "coordinator", Update(fixture, 1, 2)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.SendAsync(fixture.Request.Id, quote.Id, other, "coordinator"));
    }

    [Fact]
    public async Task ManagersCanReadTheQuotesOfTheirTeamButNotChangeThem()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        var listed = await fixture.Service.ListAsync(fixture.Request.Id, ManagerId, "technical_manager");

        Assert.Single(listed);
        Assert.False(listed[0].CanEdit);
        await Assert.ThrowsAsync<AuthException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, ManagerId, "technical_manager", Update(fixture, 1, 2)));
        await Assert.ThrowsAsync<AuthException>(() => fixture.Service.CreateAsync(
            fixture.Request.Id, ManagerId, "technical_manager", new CreateQuoteRequest()));
    }

    [Fact]
    public async Task QuoteFromAnotherRequestIsNotFound()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        var other = CompleteRequest(out _, out _, Guid.NewGuid());
        fixture.Requests.Add(other);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Service.GetAsync(other.Id, quote.Id, CoordinatorId, "coordinator"));
    }

    [Fact]
    public async Task ThePdfCarriesEveryItemWithItsPriceAndTheDraftTexts()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000) with
        {
            ScopeOfWork = ["Load test"],
            Notes = ["Pricing excludes VAT."],
            Message = "Offer"
        });

        var file = await fixture.Service.GetPdfAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.Equal("quotation.pdf", file.FileName);
        var model = fixture.Pdf.LastModel!;
        Assert.Equal(quote.ProposalNumber, model.ProposalNumber);
        Assert.Equal("Al Noor Heavy Industries Ltd.", model.ClientName);
        Assert.Equal(2, model.Items.Count);
        var receiver = model.Items.Single(item => item.PrNumber is null);
        Assert.Equal(850_000, receiver.UnitPricePiastres);
        Assert.Equal(3_400_000, receiver.TotalPiastres);
        Assert.Equal(["Load test"], model.ScopeOfWork);
        Assert.Equal(["Pricing excludes VAT."], model.Notes);
        Assert.Equal("Offer", model.Message);
        Assert.True(model.ValidUntil > model.IssuedAt);
    }

    [Fact]
    public async Task ANewRequestCanBeRejectedWithoutAnyQuotationAndNoEmailIsSent()
    {
        var fixture = new Fixture();

        var rejected = await fixture.Service.RejectRequestAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "  Outside our service area  " });

        Assert.Equal(JobStatus.QuoteRejected, rejected.Status);
        Assert.Equal("Outside our service area", fixture.Request.History.Last().Note);
        Assert.Equal("Outside our service area", fixture.Request.RejectReason);
        Assert.Empty(fixture.Sender.Sent);
        Assert.Empty(fixture.Outbox.Rows);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest()));
    }

    [Fact]
    public async Task RejectingWhileTheQuotationIsADraftClosesItWithoutSendingAnything()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));

        var rejected = await fixture.Service.RejectRequestAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "Cannot take it" });

        Assert.Equal(JobStatus.QuoteRejected, rejected.Status);
        var closed = await fixture.Service.GetAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
        Assert.Equal(QuoteStatus.Rejected, closed.Status);
        Assert.Equal("Cannot take it", closed.ClientReply);
        Assert.Null(closed.SentAt);
        Assert.False(closed.CanEdit);
        Assert.Empty(fixture.Sender.Sent);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1, 2)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.AcceptAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator"));
    }

    [Fact]
    public async Task ASentQuotationCanAlsoBeRejectedByUs()
    {
        var sent = await SentFixtureAsync();

        var rejected = await sent.Fixture.Service.RejectRequestAsync(
            sent.Fixture.Request.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "Withdrawn" });

        Assert.Equal(JobStatus.QuoteRejected, rejected.Status);
        var quote = await sent.Fixture.Service.GetAsync(sent.Fixture.Request.Id, sent.QuoteId, CoordinatorId, "coordinator");
        Assert.Equal(QuoteStatus.Rejected, quote.Status);
        Assert.Single(sent.Fixture.Sender.Sent);
    }

    [Fact]
    public async Task RejectingNeedsAReasonAnOwnerAndARequestThatIsNotClosedYet()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.RejectRequestAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "   " }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.RejectRequestAsync(
            fixture.Request.Id, Guid.NewGuid(), "coordinator", new RejectRequestRequest { Reason = "x" }));
        await Assert.ThrowsAsync<AuthException>(() => fixture.Service.RejectRequestAsync(
            fixture.Request.Id, ManagerId, "technical_manager", new RejectRequestRequest { Reason = "x" }));
        Assert.Equal(JobStatus.RequestReceived, fixture.Request.Status);

        await fixture.Service.RejectRequestAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "First" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RejectRequestAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "Again" }));
    }

    [Fact]
    public async Task AnAcceptedRequestCanNoLongerBeRejected()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        await fixture.Service.AcceptAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RejectRequestAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "Too late" }));

        Assert.Equal(JobStatus.QuoteAccepted, fixture.Request.Status);
        var current = await fixture.Service.GetAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
        Assert.Equal(QuoteStatus.Accepted, current.Status);
    }

    [Fact]
    public async Task AMaybeEmailMustBeConfirmedBeforeItCanBeRejected()
    {
        var maybe = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email(), triage: RequestTriage.Maybe);
        var fixture = new Fixture(maybe);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RejectRequestAsync(
            maybe.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "Spam" }));

        Assert.Equal(JobStatus.RequestReceived, maybe.Status);
    }

    [Fact]
    public async Task OneSaveCanCompleteTheRequestAndOpenTheQuotationInstead()
    {
        var incomplete = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());
        var fixture = new Fixture(incomplete);

        var saved = await fixture.Service.SaveRequestAndPrepareQuotationAsync(
            incomplete.Id, CoordinatorId, "coordinator", new UpdateRequestDetailsRequest
            {
                ClientName = "Al Noor Heavy Industries Ltd.",
                Location = "Industrial City",
                PrepareQuotation = true,
                Items =
                [
                    new SaveRequestItem { Name = "Mobile crane - Liebherr", Type = "Mobile crane", Quantity = 1 },
                    new SaveRequestItem { Name = "Air receiver - 3000 L", Type = "Air receiver", Quantity = 4 }
                ]
            });

        Assert.Equal(JobStatus.QuoteDraft, saved.Status);
        Assert.Equal(2, saved.Items.Count);
        var quotation = saved.Quotation!;
        Assert.Equal(QuoteStatus.Open, quotation.Status);
        Assert.Equal(1, quotation.Version);
        Assert.Equal(2, quotation.Items!.Count);
        Assert.Equal(saved.Items.Select(item => item.Id).Order(), quotation.Items.Select(item => item.RequestItemId!.Value).Order());
        Assert.All(quotation.Items, item => Assert.Null(item.UnitPricePiastres));
        Assert.Equal(quotation.Items.Select(item => item.RequestItemId!.Value).Order(), quotation.MissingPrices!.Order());
        Assert.Equal("Al Noor", quotation.ContactName);
        Assert.Equal(QuotationDefaults.StandardNotes, quotation.Notes);
        Assert.True(quotation.CanEdit);
        Assert.Single(await fixture.Quotes.ListForRequestAsync(incomplete.Id));
        Assert.Empty(fixture.Sender.Sent);
    }

    [Fact]
    public async Task AnIncompleteRequestIsNotSavedNorQuotedAndTheMissingFieldsAreNamed()
    {
        var incomplete = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());
        var fixture = new Fixture(incomplete);

        var conflict = await Assert.ThrowsAsync<AuthException>(() => fixture.Service.SaveRequestAndPrepareQuotationAsync(
            incomplete.Id, CoordinatorId, "coordinator", new UpdateRequestDetailsRequest
            {
                ClientName = "Al Noor",
                PrepareQuotation = true
            }));

        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal("request_incomplete", conflict.Code);
        Assert.Contains("location", conflict.Message, StringComparison.Ordinal);
        Assert.Contains("items", conflict.Message, StringComparison.Ordinal);
        Assert.Equal(JobStatus.RequestReceived, incomplete.Status);
        Assert.Empty(await fixture.Quotes.ListForRequestAsync(incomplete.Id));
    }

    [Fact]
    public async Task LeavingTheFlagOutOpensTheQuotationOnceTheRequestIsComplete()
    {
        var fixture = new Fixture();

        var saved = await fixture.Service.SaveRequestAndPrepareQuotationAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", FullSave(fixture) with { PrepareQuotation = null });

        Assert.Equal(JobStatus.QuoteDraft, saved.Status);
        Assert.Equal(QuoteStatus.Open, saved.Quotation!.Status);
        Assert.Single(await fixture.Quotes.ListForRequestAsync(fixture.Request.Id));
    }

    [Fact]
    public async Task LeavingTheFlagOutOnAnIncompleteRequestJustSavesIt()
    {
        var incomplete = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());
        var fixture = new Fixture(incomplete);

        var saved = await fixture.Service.SaveRequestAndPrepareQuotationAsync(
            incomplete.Id, CoordinatorId, "coordinator", new UpdateRequestDetailsRequest { ClientName = "Al Noor" });

        Assert.Equal("Al Noor", saved.ClientName);
        Assert.Equal(JobStatus.RequestReceived, saved.Status);
        Assert.Null(saved.Quotation);
        Assert.Empty(await fixture.Quotes.ListForRequestAsync(incomplete.Id));
    }


    [Fact]
    public async Task PressingTheButtonAgainRefreshesTheSameDraftAndKeepsItsPrices()
    {
        var fixture = new Fixture();
        var first = await fixture.Service.SaveRequestAndPrepareQuotationAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", FullSave(fixture));
        await fixture.Service.UpdateAsync(
            fixture.Request.Id, first.Quotation!.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));

        var again = await fixture.Service.SaveRequestAndPrepareQuotationAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", FullSave(fixture) with
            {
                Items =
                [
                    new SaveRequestItem { Id = fixture.Crane.Id, Name = "Mobile crane - Liebherr LTM 1100", Quantity = 1 },
                    new SaveRequestItem { Id = fixture.Receiver.Id, Name = "Air receiver - 3000 L", Quantity = 4 },
                    new SaveRequestItem { Name = "Safety valve DN40", Quantity = 6 }
                ]
            });

        Assert.Equal(first.Quotation.Id, again.Quotation!.Id);
        Assert.Equal(1, again.Quotation.Version);
        Assert.Single(await fixture.Quotes.ListForRequestAsync(fixture.Request.Id));
        Assert.Equal(3, again.Quotation.Items!.Count);
        Assert.Equal(1_200_000, again.Quotation.Items.Single(item => item.RequestItemId == fixture.Crane.Id).UnitPricePiastres);
        Assert.Equal(850_000, again.Quotation.Items.Single(item => item.RequestItemId == fixture.Receiver.Id).UnitPricePiastres);
        var added = again.Quotation.Items.Single(item => item.Description == "Safety valve DN40");
        Assert.Null(added.UnitPricePiastres);
        Assert.Equal([added.RequestItemId!.Value], again.Quotation.MissingPrices!);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
    }

    [Fact]
    public async Task PreparingTheQuotationAlsoRegistersNewMachines()
    {
        var registrar = new RecordingRegistrar();
        var fixture = new Fixture(registrar: registrar);

        await fixture.Service.SaveRequestAndPrepareQuotationAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", FullSave(fixture));

        Assert.Equal(1, registrar.Calls);
    }

    [Fact]
    public async Task OnlyTheOwnerCanSaveAndPrepareAndAStaleScreenGetsAConflict()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.SaveRequestAndPrepareQuotationAsync(
            fixture.Request.Id, Guid.NewGuid(), "coordinator", FullSave(fixture)));
        await Assert.ThrowsAsync<AuthException>(() => fixture.Service.SaveRequestAndPrepareQuotationAsync(
            fixture.Request.Id, ManagerId, "technical_manager", FullSave(fixture)));
        var stale = await Assert.ThrowsAsync<AuthException>(() => fixture.Service.SaveRequestAndPrepareQuotationAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", FullSave(fixture) with { ExpectedUpdatedAt = fixture.Request.UpdatedAt.AddMinutes(-3) }));

        Assert.Equal("request_changed", stale.Code);
        Assert.Empty(await fixture.Quotes.ListForRequestAsync(fixture.Request.Id));
        Assert.Equal(JobStatus.RequestReceived, fixture.Request.Status);
    }

    [Fact]
    public async Task ASentQuotationCannotBeReopenedBySavingTheRequest()
    {
        var sent = await SentFixtureAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sent.Fixture.Service.SaveRequestAndPrepareQuotationAsync(
            sent.Fixture.Request.Id, CoordinatorId, "coordinator", FullSave(sent.Fixture)));
    }

    private static UpdateRequestDetailsRequest FullSave(Fixture fixture) => new()
    {
        ClientName = "Al Noor Heavy Industries Ltd.",
        Location = "Industrial City",
        PrepareQuotation = true,
        Items =
        [
            new SaveRequestItem { Id = fixture.Crane.Id, Name = fixture.Crane.Name, Quantity = fixture.Crane.Quantity, Type = fixture.Crane.Type },
            new SaveRequestItem { Id = fixture.Receiver.Id, Name = fixture.Receiver.Name, Quantity = fixture.Receiver.Quantity, Type = fixture.Receiver.Type }
        ]
    };

    private sealed class RecordingRegistrar : IRequestEquipmentRegistrar
    {
        public int Calls { get; private set; }

        public Task RegisterAsync(InspectionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ANewQuoteStartsWithTheStandardNotesAndTheSenderAsContact()
    {
        var fixture = new Fixture();

        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        Assert.Equal(QuotationDefaults.StandardNotes, quote.Notes);
        Assert.Equal("Al Noor", quote.ContactName);
        Assert.Matches(@"^QT-\d{4}-[0-9A-F]{6}$", quote.QuotationNumber);
        Assert.Equal($"{quote.QuotationNumber} rev01", quote.ProposalNumber);
    }

    [Fact]
    public async Task TheContactNameIsEditableAndKeptByRevisions()
    {
        var fixture = new Fixture();
        var first = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        var saved = await fixture.Service.UpdateAsync(
            fixture.Request.Id, first.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000) with { ContactName = "  Eng. Reem " });
        Assert.Equal("Eng. Reem", saved.ContactName);
        await fixture.Service.SendAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");

        var revision = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        Assert.Equal("Eng. Reem", revision.ContactName);
        Assert.Equal(first.QuotationNumber, revision.QuotationNumber);
        Assert.EndsWith("rev02", revision.ProposalNumber, StringComparison.Ordinal);
        Assert.Equal(first.Notes, revision.Notes);
    }

    [Fact]
    public async Task ThePdfModelCarriesTheCoverDataSubjectAndPreparer()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000) with { ValidForDays = 21 });

        await fixture.Service.GetPdfAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        var model = fixture.Pdf.LastModel!;
        Assert.Equal("Mobile crane, Air receiver", model.Subject);
        Assert.Equal(quote.QuotationNumber, model.QuotationNumber);
        Assert.Equal("Al Noor", model.ContactName);
        Assert.Equal("Eng. Ahmed", model.PreparedBy);
        Assert.Equal("Coordinator", model.PreparedByTitle);
        Assert.Equal(21, model.ValidForDays);
        Assert.Equal(QuotationDefaults.StandardNotes, model.Notes);
        Assert.Equal(["Mobile crane", "Air receiver"], model.Items.Select(item => item.Type));
        Assert.Null(model.Company);
    }

    [Fact]
    public void QuotationStatusesAreOpenAcceptedAndRejected() =>
        Assert.Equal(["Open", "Accepted", "Rejected"], Enum.GetNames<QuoteStatus>());

    private static async Task<(Fixture Fixture, Guid QuoteId)> SentFixtureAsync()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
        return (fixture, quote.Id);
    }

    private static UpdateQuoteRequest Update(Fixture fixture, long? cranePrice, long? receiverPrice) => new()
    {
        Items =
        [
            new UpdateQuoteItem { RequestItemId = fixture.Crane.Id, UnitPricePiastres = cranePrice },
            new UpdateQuoteItem { RequestItemId = fixture.Receiver.Id, UnitPricePiastres = receiverPrice }
        ]
    };

    private static IncomingEmail Email() => new(
        "42", 1, "Kindly quote for our crane and air receivers.", null, "buyer@alnoor.com", "Al Noor", "Quote request",
        [], [], [], [], [], "<id@alnoor.com>", null, null, DateTimeOffset.UtcNow.AddMinutes(-5));

    private static InspectionRequest CompleteRequest(out RequestItem crane, out RequestItem receiver, Guid? clientId, IncomingEmail? email = null)
    {
        var request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, email ?? Email());
        request.UpdateDetails("Al Noor Heavy Industries Ltd.", "Industrial City", clientId, null, null);
        crane = request.AddItem(RequestItem.Create("Mobile crane - Liebherr LTM 1100", 1, type: "Mobile crane", prNumber: "13351021"));
        receiver = request.AddItem(RequestItem.Create("Air receiver - 3000 L", 4, type: "Air receiver"));
        return request;
    }

    [Fact]
    public async Task SendingEmailsThePdfAsAReplyToTheOriginalSenderAndThenMarksTheQuoteSent()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));

        var sent = await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        var email = Assert.Single(fixture.Sender.Sent);
        Assert.Equal("buyer@alnoor.com", email.ToAddress);
        Assert.Equal("Al Noor", email.ToName);
        Assert.Equal("coordinator@example.com", email.FromAddress);
        Assert.Equal("Eng. Ahmed", email.FromName);
        Assert.Equal("Re: Quote request", email.Subject);
        Assert.Equal("<id@alnoor.com>", email.InReplyTo);
        Assert.Contains("<id@alnoor.com>", email.References, StringComparison.Ordinal);
        Assert.Equal("quotation.pdf", email.AttachmentFileName);
        Assert.Equal([1, 2, 3], email.AttachmentContent);
        Assert.Contains(sent.ProposalNumber, email.BodyText, StringComparison.Ordinal);
        Assert.Contains("Dear Al Noor", email.BodyText, StringComparison.Ordinal);
        Assert.Contains("Mobile crane", email.BodyText, StringComparison.Ordinal);
        Assert.Equal("imap.example.com".Replace("imap", "smtp"), fixture.Sender.LastSmtp!.Server);
        Assert.Equal(587, fixture.Sender.LastSmtp.Port);
        Assert.False(fixture.Sender.LastSmtp.UseSsl);

        var delivery = Assert.Single(sent.Emails!);
        Assert.Equal(OutboundEmailKind.Offer, delivery.Kind);
        Assert.Equal(OutboundEmailStatus.Sent, delivery.Status);
        Assert.NotNull(sent.SentAt);
        Assert.Equal(JobStatus.QuoteSent, fixture.Request.Status);
    }

    [Fact]
    public async Task TheReplyToAddressOfTheOriginalEmailWinsOverTheFromAddress()
    {
        var request = CompleteRequest(out _, out _, Guid.NewGuid(), Email() with
        {
            ReplyTo = [new EmailAddressInfo("purchasing@alnoor.com", "Purchasing")]
        });
        var fixture = new Fixture(request);
        var quote = await fixture.Service.CreateAsync(request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 100, 200));

        await fixture.Service.SendAsync(request.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.Equal("purchasing@alnoor.com", Assert.Single(fixture.Sender.Sent).ToAddress);
    }

    [Fact]
    public async Task AFailedEmailChangesNothingAndTheRetryReusesTheSameMessage()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        fixture.Sender.Failure = new EmailDeliveryException("The mail server could not be reached.");

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator"));

        Assert.Contains("mail server", failure.Message, StringComparison.Ordinal);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
        var draft = await fixture.Service.GetAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
        Assert.Null(draft.SentAt);
        Assert.True(draft.CanEdit);
        var failed = Assert.Single(draft.Emails!);
        Assert.Equal(OutboundEmailStatus.Failed, failed.Status);
        Assert.Equal(1, failed.Attempts);
        Assert.Equal("The mail server could not be reached.", failed.LastError);
        var messageId = Assert.Single(fixture.Outbox.Rows).MessageId;

        fixture.Sender.Failure = null;
        var sent = await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.Equal(OutboundEmailStatus.Sent, Assert.Single(sent.Emails!).Status);
        Assert.Equal(2, Assert.Single(sent.Emails!).Attempts);
        Assert.Equal(messageId, Assert.Single(fixture.Sender.Sent).MessageId);
        Assert.Equal(JobStatus.QuoteSent, fixture.Request.Status);
    }

    [Fact]
    public async Task ASecondSendCannotDeliverTheEmailAgain()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator"));

        Assert.Single(fixture.Sender.Sent);
    }

    [Fact]
    public async Task AnEmailAlreadyDeliveredIsNotSentAgainWhenTheCallIsRepeated()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        var stored = await fixture.Quotes.GetByIdAsync(quote.Id);

        var first = await fixture.Delivery.DeliverAsync(fixture.Request, stored!, OutboundEmailKind.Offer);
        var second = await fixture.Delivery.DeliverAsync(fixture.Request, stored!, OutboundEmailKind.Offer);

        Assert.Same(first, second);
        Assert.Single(fixture.Sender.Sent);
    }

    [Fact]
    public async Task AFreshPendingEmailBlocksAParallelSend()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        var stored = await fixture.Quotes.GetByIdAsync(quote.Id);
        var pending = OutboundEmail.Create(ManagerId, fixture.Request.Id, quote.Id, OutboundEmailKind.Offer, "example.com");
        pending.BeginAttempt("buyer@alnoor.com", null, "coordinator@example.com", null, "Re: x", "body", null, null, "a.pdf", [1]);
        await fixture.Outbox.AddAsync(pending);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Delivery.DeliverAsync(fixture.Request, stored!, OutboundEmailKind.Offer));

        Assert.Empty(fixture.Sender.Sent);
    }

    [Fact]
    public async Task WithoutAConfiguredMailboxNothingIsSentOrChanged()
    {
        var fixture = new Fixture(configured: false);
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 100, 200));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator"));

        Assert.Contains("mailbox", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Sender.Sent);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
        Assert.Empty(fixture.Outbox.Rows);
    }

    [Fact]
    public async Task ARequestWithoutASourceEmailIsOnlyMarkedWithoutSendingAnything()
    {
        var manual = InspectionRequest.Create(
            "Al Noor", "Cairo", null, [RequestItem.Create("Crane", 1)], CoordinatorId, managerId: ManagerId);
        var fixture = new Fixture(manual);
        var quote = await fixture.Service.CreateAsync(manual.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(manual.Id, quote.Id, CoordinatorId, "coordinator", new UpdateQuoteRequest
        {
            Items = [new UpdateQuoteItem { RequestItemId = manual.Items[0].Id, UnitPricePiastres = 500 }]
        });

        var sent = await fixture.Service.SendAsync(manual.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.Empty(fixture.Sender.Sent);
        Assert.Empty(sent.Emails!);
        Assert.NotNull(sent.SentAt);
        Assert.Equal(JobStatus.QuoteSent, manual.Status);
    }

    [Fact]
    public async Task ARequestNotLinkedToAClientCanStillBeSent()
    {
        var request = CompleteRequest(out var crane, out var receiver, clientId: null);
        var fixture = new Fixture(request);
        var quote = await fixture.Service.CreateAsync(request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(request.Id, quote.Id, CoordinatorId, "coordinator", new UpdateQuoteRequest
        {
            Items =
            [
                new UpdateQuoteItem { RequestItemId = crane.Id, UnitPricePiastres = 100 },
                new UpdateQuoteItem { RequestItemId = receiver.Id, UnitPricePiastres = 200 }
            ]
        });

        var sent = await fixture.Service.SendAsync(request.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.NotNull(sent.SentAt);
        Assert.Null(request.ClientId);
        Assert.Equal(JobStatus.QuoteSent, request.Status);
    }

    [Fact]
    public async Task AcceptingADraftSendsTheAcceptanceEmailAndClosesTheQuote()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));

        var accepted = await fixture.Service.AcceptAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.Equal(QuoteStatus.Accepted, accepted.Status);
        Assert.NotNull(accepted.SentAt);
        Assert.Equal(JobStatus.QuoteAccepted, fixture.Request.Status);
        var email = Assert.Single(fixture.Sender.Sent);
        Assert.Contains("accept your request", email.BodyText, StringComparison.Ordinal);
        Assert.Equal(OutboundEmailKind.Accept, Assert.Single(accepted.Emails!).Kind);
        Assert.Contains(fixture.Request.History, entry => entry.ToStatus == JobStatus.QuoteSent);
        Assert.Contains(fixture.Request.History, entry => entry.ToStatus == JobStatus.QuoteAccepted);
    }

    [Fact]
    public async Task AcceptingNeedsEveryItemPricedWhenTheQuoteWasNotSentYet()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.AcceptAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator"));

        Assert.Empty(fixture.Sender.Sent);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
    }

    [Fact]
    public async Task AcceptingAFailedEmailLeavesTheQuoteOpen()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        fixture.Sender.Failure = new EmailDeliveryException("The mail server refused the recipient address.");

        await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            fixture.Service.AcceptAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator"));

        var current = await fixture.Service.GetAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
        Assert.Equal(QuoteStatus.Open, current.Status);
        Assert.Null(current.SentAt);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
    }

    [Fact]
    public async Task AcceptingASentQuoteEmailsAndClosesIt()
    {
        var sent = await SentFixtureAsync();

        var accepted = await sent.Fixture.Service.AcceptAsync(sent.Fixture.Request.Id, sent.QuoteId, CoordinatorId, "coordinator");

        Assert.Equal(QuoteStatus.Accepted, accepted.Status);
        Assert.Equal(2, sent.Fixture.Sender.Sent.Count);
        Assert.Equal(JobStatus.QuoteAccepted, sent.Fixture.Request.Status);
    }

    [Fact]
    public async Task CounterOfferRevisionKeepsHistoryAndCanBeSentAgain()
    {
        var fixture = new Fixture();
        var first = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000) with
        {
            ProposalNumber = "INT/417/2026 rev01",
            ScopeOfWork = ["Load test"],
            Notes = ["Pricing excludes VAT."],
            Message = "First offer"
        });
        await fixture.Service.SendAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");

        var second = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        Assert.Equal(2, second.Version);
        Assert.Equal("INT/417/2026 rev02", second.ProposalNumber);
        Assert.Equal(1_200_000, second.Items!.Single(item => item.RequestItemId == fixture.Crane.Id).UnitPricePiastres);
        Assert.Equal(850_000, second.Items!.Single(item => item.RequestItemId == fixture.Receiver.Id).UnitPricePiastres);
        Assert.Equal(["Load test"], second.ScopeOfWork);
        Assert.Equal(["Pricing excludes VAT."], second.Notes);
        Assert.Equal("First offer", second.Message);
        Assert.True(second.CanEdit);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);

        var previous = await fixture.Service.GetAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");
        Assert.NotNull(previous.SupersededAt);
        Assert.Equal(QuoteStatus.Open, previous.Status);
        Assert.Equal(850_000, previous.Items!.Single(item => item.RequestItemId == fixture.Receiver.Id).UnitPricePiastres);

        await fixture.Service.UpdateAsync(fixture.Request.Id, second.Id, CoordinatorId, "coordinator", Update(fixture, 1_100_000, 800_000));
        var resent = await fixture.Service.SendAsync(fixture.Request.Id, second.Id, CoordinatorId, "coordinator");

        Assert.NotNull(resent.SentAt);
        Assert.Equal(2, fixture.Sender.Sent.Count);
        Assert.Equal(JobStatus.QuoteSent, fixture.Request.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RecordDecisionAsync(
            fixture.Request.Id, first.Id, CoordinatorId, "coordinator", new QuoteDecisionRequest { Decision = QuoteStatus.Accepted }));
    }

    [Fact]
    public async Task OnlyOneRevisionCanBeInPreparation()
    {
        var sent = await SentFixtureAsync();
        await sent.Fixture.Service.CreateAsync(sent.Fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sent.Fixture.Service.CreateAsync(sent.Fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest()));
    }

    [Fact]
    public async Task TheProposalNumberIsEditableWhileTheQuoteIsADraft()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 100, 200));

        var untouched = await fixture.Service.GetAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
        Assert.Matches(@"^QT-\d{4}-[0-9A-F]{6} rev01$", untouched.ProposalNumber);

        var renamed = await fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 100, 200) with { ProposalNumber = "  INT/417/2026 rev01 " });
        Assert.Equal("INT/417/2026 rev01", renamed.ProposalNumber);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 100, 200) with { ProposalNumber = "   " }));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 100, 200) with { ProposalNumber = new string('x', 65) }));

        await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 100, 200) with { ProposalNumber = "OTHER" }));
        Assert.Contains("INT/417/2026 rev01", Assert.Single(fixture.Sender.Sent).Subject + Assert.Single(fixture.Sender.Sent).BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public void RevisionNumbersKeepTheReferenceAndBumpTheRevision()
    {
        Assert.Equal("INT/417/2026 rev02", Quote.NextRevisionNumber("INT/417/2026 rev01", 2));
        Assert.Equal("QT-2026-ABC123 rev03", Quote.NextRevisionNumber("QT-2026-ABC123 rev02", 3));
        Assert.Equal("CUSTOM rev02", Quote.NextRevisionNumber("CUSTOM", 2));
        Assert.Equal("rev02", Quote.NextRevisionNumber("", 2));
    }

    [Fact]
    public void OnlyTheLatestSentOpenRevisionCanBeSupersededAndNoDecisionFollows()
    {
        var quote = Quote.Create(Guid.NewGuid(), 1, 0, DateTimeOffset.UtcNow.AddDays(14), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(quote.Supersede);

        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 100, "Per Visit")], null, null);
        quote.Send();
        quote.Supersede();

        Assert.False(quote.IsCurrent);
        Assert.Throws<InvalidOperationException>(quote.Supersede);
        Assert.Throws<InvalidOperationException>(() => quote.RecordClientDecision(QuoteStatus.Accepted));
    }

    [Fact]
    public void TheMailboxDerivesTheOutgoingServerFromTheImapServerUnlessConfigured()
    {
        var config = CoordinatorEmailConfig.Create(CoordinatorId, ManagerId, "imap.gmail.com", 993, "a@gmail.com", "enc", true);
        Assert.Equal(new SmtpEndpoint("smtp.gmail.com", 587, false), config.ResolveSmtp());

        var outlook = CoordinatorEmailConfig.Create(CoordinatorId, ManagerId, "imap-mail.outlook.com", 993, "a@outlook.com", "enc", true);
        Assert.Equal("smtp-mail.outlook.com", outlook.ResolveSmtp().Server);

        var plain = CoordinatorEmailConfig.Create(CoordinatorId, ManagerId, "mail.example.com", 993, "a@example.com", "enc", true);
        Assert.Equal("mail.example.com", plain.ResolveSmtp().Server);

        var custom = CoordinatorEmailConfig.Create(
            CoordinatorId, ManagerId, "imap.example.com", 993, "a@example.com", "enc", true,
            smtpServer: "relay.example.com", smtpPort: 465);
        Assert.Equal(new SmtpEndpoint("relay.example.com", 465, true), custom.ResolveSmtp());

        custom.Update("imap.example.com", 993, "a@example.com", "enc", true, smtpServer: "relay.example.com", smtpPort: 2525, smtpUseSsl: false);
        Assert.Equal(new SmtpEndpoint("relay.example.com", 2525, false), custom.ResolveSmtp());
        Assert.Throws<ArgumentOutOfRangeException>(() => CoordinatorEmailConfig.Create(
            CoordinatorId, ManagerId, "imap.example.com", 993, "a@example.com", "enc", true, smtpPort: 70000));
    }


    internal sealed class Fixture
    {
        public Fixture(InspectionRequest? request = null, bool configured = true, IRequestEquipmentRegistrar? registrar = null)
        {
            Request = request ?? CompleteRequest(out _, out _, Guid.NewGuid());
            Crane = Request.Items.ElementAtOrDefault(0)!;
            Receiver = Request.Items.ElementAtOrDefault(1)!;
            Requests = new FakeRequests(Request);
            Pdf = new FakePdfGenerator();
            Quotes = new FakeQuotes();
            Outbox = new FakeOutbox();
            Sender = new FakeSender();
            Delivery = new QuoteDeliveryService(
                Requests,
                new FakeConfigs(configured ? CoordinatorEmailConfig.Create(
                    CoordinatorId, ManagerId, "imap.example.com", 993, "coordinator@example.com", "encrypted", true) : null),
                new IncomingEmailRequestTests.FakeUserRepository(Coordinator),
                Outbox,
                new FakeProtector(),
                Pdf,
                Sender);
            Service = new QuoteService(Quotes, Requests, Pdf, Delivery, Outbox, new IncomingEmailRequestTests.FakeUserRepository(Coordinator), registrar);
        }

        public InspectionRequest Request { get; }
        public RequestItem Crane { get; }
        public RequestItem Receiver { get; }
        public FakeRequests Requests { get; }
        public FakePdfGenerator Pdf { get; }
        public FakeQuotes Quotes { get; }
        public FakeOutbox Outbox { get; }
        public FakeSender Sender { get; }
        public QuoteDeliveryService Delivery { get; }
        public QuoteService Service { get; }
    }

    internal sealed class FakeSender : IEmailSender
    {
        public List<OutgoingEmail> Sent { get; } = [];
        public SmtpSettings? LastSmtp { get; private set; }
        public Exception? Failure { get; set; }

        public Task SendAsync(SmtpSettings smtp, OutgoingEmail email, CancellationToken cancellationToken = default)
        {
            LastSmtp = smtp;
            if (Failure is not null) throw Failure;
            Sent.Add(email);
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string protectedValue) => protectedValue;
    }

    internal sealed class FakeConfigs(CoordinatorEmailConfig? config) : ICoordinatorEmailConfigRepository
    {
        public Task<CoordinatorEmailConfig?> GetByCoordinatorAsync(Guid coordinatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(config);

        public Task SaveAsync(CoordinatorEmailConfig value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class FakeOutbox : IOutboundEmailRepository
    {
        public List<OutboundEmail> Rows { get; } = [];

        public Task<OutboundEmail?> GetAsync(Guid quoteId, OutboundEmailKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.SingleOrDefault(row => row.QuoteId == quoteId && row.Kind == kind));

        public Task<IReadOnlyList<OutboundEmailSummary>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OutboundEmailSummary>>(Rows
                .Where(row => row.RequestId == requestId)
                .Select(row => new OutboundEmailSummary(
                    row.QuoteId, row.Kind, row.Status, row.ToAddress, row.Attempts, row.SentAt, row.LastError, row.CreatedAt))
                .ToArray());

        public Task AddAsync(OutboundEmail email, CancellationToken cancellationToken = default)
        {
            if (Rows.Any(row => row.QuoteId == email.QuoteId && row.Kind == email.Kind))
                throw new InvalidOperationException("This email is already being sent.");
            Rows.Add(email);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class FakePdfGenerator : IPdfGenerator
    {
        public QuotationPdfModel? LastModel { get; private set; }

        public PdfFile Generate<TModel>(TModel model)
        {
            LastModel = model as QuotationPdfModel;
            return new PdfFile([1, 2, 3], "quotation.pdf");
        }
    }

    internal sealed class FakeQuotes : IQuoteRepository
    {
        private readonly List<Quote> quotes = [];

        public Func<Guid, InspectionRequest?> RequestOf { get; set; } = _ => null;
        public QuoteListFilter? LastFilter { get; private set; }

        public Task AddAsync(Quote quote, CancellationToken cancellationToken = default)
        {
            quotes.Add(quote);
            return Task.CompletedTask;
        }

        public Task<Quote?> GetByIdAsync(Guid quoteId, CancellationToken cancellationToken = default) =>
            Task.FromResult(quotes.SingleOrDefault(quote => quote.Id == quoteId));

        public Task<IReadOnlyList<Quote>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Quote>>(quotes.Where(quote => quote.RequestId == requestId).OrderBy(quote => quote.Version).ToArray());

        public Task<IReadOnlyList<Quote>> ListAsync(QuoteListFilter filter, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            var requestProperty = typeof(Quote).GetProperty(nameof(Quote.Request))!;
            foreach (var quote in quotes) requestProperty.SetValue(quote, RequestOf(quote.RequestId));
            return Task.FromResult<IReadOnlyList<Quote>>(quotes
                .Where(quote => quote.Request is not null)
                .Where(quote => filter.CoordinatorId is not Guid coordinatorId ||
                    quote.Request!.Source != RequestSource.Email || quote.Request.CoordinatorId == coordinatorId)
                .Where(quote => !filter.CurrentOnly || quote.SupersededAt is null)
                .Where(quote => filter.Open is not bool open || (quote.Status == QuoteStatus.Open) == open)
                .Where(quote => filter.Sent is not bool sent || quote.IsSent == sent)
                .Where(quote => filter.Accepted is not bool accepted || quote.Status == (accepted ? QuoteStatus.Accepted : QuoteStatus.Rejected))
                .OrderByDescending(quote => quote.UpdatedAt).ToArray());
        }

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    internal sealed class FakeRequests(params InspectionRequest[] initial) : IRequestRepository
    {
        private readonly List<InspectionRequest> requests = [.. initial];

        public void Add(InspectionRequest request) => requests.Add(request);

        public Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default) =>
            Task.FromResult(requests.SingleOrDefault(request => request.Id == requestId));

        public Task AddAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RequestEmail?> GetEmailAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(requests.SingleOrDefault(request => request.Id == requestId)?.Email);
        public Task<IReadOnlyList<InspectionRequest>> ListAsync(RequestListFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InspectionRequest>>(requests);
        public Task<IReadOnlyList<RequestCount>> CountAsync(RequestListFilter scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestCount>>([]);
        public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
    }
}
