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
    public async Task ARequestThatAsksForAManDayGetsAPricedManDayLineBesideTheMachines()
    {
        var fixture = new Fixture();
        fixture.Request.SetManDay(3);

        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        Assert.Equal(3, quote.Items!.Count);
        var manDay = quote.Items.Last();
        Assert.Null(manDay.RequestItemId); // an inspector's day is not a machine
        Assert.Equal("Man day", manDay.Type);
        Assert.Equal(3, manDay.Quantity);
        Assert.Equal("Per Day", manDay.PricingUnit);
        Assert.Null(manDay.UnitPricePiastres);
        Assert.Equal(5, quote.TotalUnits); // the days are not counted as machines

        // It must be priced before the quotation goes out, like every machine.
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        var unpriced = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator"));
        Assert.Contains("Man day", unpriced.Message, StringComparison.Ordinal);

        var priced = await fixture.Service.UpdateAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000) with { ManDayPricePiastres = 500_000 });
        Assert.Equal(1_200_000 + 4 * 850_000 + 3 * 500_000, priced.AmountPiastres);
        Assert.Equal(1_500_000, priced.Items!.Last().TotalPiastres);
        var sent = await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
        Assert.Equal(QuoteStatus.Open, sent.Status);
        Assert.NotNull(sent.SentAt);
    }

    [Fact]
    public async Task TheManDayLineFollowsTheRequestAndKeepsItsPrice()
    {
        var fixture = new Fixture();
        fixture.Request.SetManDay(2);
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1, 2) with { ManDayPricePiastres = 700_000 });

        // Asking for no man day removes the line from the next save; the request itself refuses 366 days and a negative number.
        fixture.Request.SetManDay(0);
        Assert.Null(fixture.Request.ManDayDays);
        var without = await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1, 2) with { ManDayPricePiastres = 700_000 });
        Assert.Equal(2, without.Items!.Count);
        Assert.Equal(1 + 4 * 2, without.AmountPiastres);
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Request.SetManDay(366));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Request.SetManDay(-1));
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
        Assert.Equal(JobStatus.Unassigned, accepted.Fixture.Request.Status);

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

        Assert.Equal(JobStatus.Unassigned, fixture.Request.Status);
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
    public async Task WithEmailingSwitchedOffAcceptAndSendStillChangeStateButSendNothing()
    {
        var fixture = new Fixture(configured: false, delivery: new QuoteDeliveryOptions(Enabled: false));
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));

        var sent = await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
        Assert.NotNull(sent.SentAt);
        Assert.Equal(JobStatus.QuoteSent, fixture.Request.Status);

        var accepted = await fixture.Service.AcceptAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.Equal(QuoteStatus.Accepted, accepted.Status);
        Assert.Equal(JobStatus.Unassigned, fixture.Request.Status);
        Assert.Empty(fixture.Sender.Sent);
        Assert.Empty(accepted.Emails!);
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
        Assert.StartsWith("Quotation ", email.Subject);
        Assert.Equal("<id@alnoor.com>", email.InReplyTo);
        Assert.Contains("<id@alnoor.com>", email.References, StringComparison.Ordinal);
        Assert.Equal("quotation.pdf", email.AttachmentFileName);
        Assert.Equal([1, 2, 3], email.AttachmentContent);
        Assert.Contains(sent.ProposalNumber, email.BodyText, StringComparison.Ordinal);
        Assert.Contains("Dears,", email.BodyText, StringComparison.Ordinal);
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
    public async Task FilesAddedByTheCoordinatorGoWithTheQuotationAndOthersAreRefused()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));

        var terms = new Application.Contracts.Mail.EmailAttachmentUpload("terms.pdf", Convert.ToBase64String("%PDF-1.4 terms"u8.ToArray()));
        var program = new Application.Contracts.Mail.EmailAttachmentUpload("run.pdf", Convert.ToBase64String("MZ program"u8.ToArray()));

        // A file that is not what its name says is refused and nothing is sent.
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SendAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", new SendQuoteEmailRequest { Attachments = [program] }));
        Assert.Empty(fixture.Sender.Sent);

        var preview = await fixture.Service.PreviewEmailAsync(
            fixture.Request.Id, quote.Id, "send", CoordinatorId, "coordinator", new SendQuoteEmailRequest { Attachments = [terms] });
        Assert.Equal(["terms.pdf"], preview.ExtraAttachmentNames);

        await fixture.Service.SendAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", new SendQuoteEmailRequest { Attachments = [terms] });

        var email = Assert.Single(fixture.Sender.Sent);
        Assert.Equal("quotation.pdf", email.AttachmentFileName);
        var extra = Assert.Single(email.MoreAttachments!);
        Assert.Equal("terms.pdf", extra.FileName);
        Assert.Equal("application/pdf", extra.ContentType);
        Assert.Equal("%PDF-1.4 terms"u8.ToArray(), extra.Content);
    }

    [Fact]
    public void TheExtraFilesAreLimitedInNumberSizeAndType()
    {
        string Pdf(int bytes) => Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("%PDF" + new string('a', bytes)));
        Application.Contracts.Mail.EmailAttachmentUpload File(string name, string content) => new(name, content);

        Assert.Empty(ExtraEmailAttachments.Decode(null));
        Assert.Equal("a.pdf", Assert.Single(ExtraEmailAttachments.Decode([File("C:\\docs\\a.pdf", Pdf(10))])).FileName);

        Assert.Throws<ArgumentException>(() => ExtraEmailAttachments.Decode([File("run.exe", Pdf(10))]));
        Assert.Throws<ArgumentException>(() => ExtraEmailAttachments.Decode([File("noextension", Pdf(10))]));
        Assert.Throws<ArgumentException>(() => ExtraEmailAttachments.Decode([File("a.pdf", "not base64!")]));
        Assert.Throws<ArgumentException>(() => ExtraEmailAttachments.Decode([File("a.pdf", "")]));
        Assert.Throws<ArgumentException>(() => ExtraEmailAttachments.Decode(
            Enumerable.Range(0, ExtraEmailAttachments.MaxFiles + 1).Select(index => File($"{index}.pdf", Pdf(10))).ToArray()));
        Assert.Throws<ArgumentException>(() => ExtraEmailAttachments.Decode(
            [File("big1.pdf", Pdf(9 * 1024 * 1024)), File("big2.pdf", Pdf(9 * 1024 * 1024))]));
    }

    [Fact]
    public async Task ThePreviewShowsTheReadyEmailAndSendsNothing()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, null));

        var unpriced = await fixture.Service.PreviewEmailAsync(fixture.Request.Id, quote.Id, "send", CoordinatorId, "coordinator");
        Assert.False(unpriced.CanSend);
        Assert.Contains("Air receiver", Assert.Single(unpriced.Blockers), StringComparison.Ordinal);

        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        var offer = await fixture.Service.PreviewEmailAsync(fixture.Request.Id, quote.Id, "send", CoordinatorId, "coordinator");

        Assert.True(offer.CanSend);
        Assert.Empty(offer.Blockers);
        Assert.Equal("buyer@alnoor.com", offer.To);
        Assert.Equal("coordinator@example.com", offer.From);
        Assert.StartsWith("Quotation ", offer.Subject);
        Assert.Equal("quotation.pdf", offer.AttachmentName);
        Assert.Equal($"/api/v1/requests/{fixture.Request.Id}/quotes/{quote.Id}/pdf", offer.PdfUrl);
        Assert.True(offer.RepliesToOriginalEmail);
        Assert.Contains("Dears,", offer.Body, StringComparison.Ordinal);
        Assert.Contains("technical and commercial offer", offer.Body, StringComparison.Ordinal);

        var accept = await fixture.Service.PreviewEmailAsync(fixture.Request.Id, quote.Id, "accept", CoordinatorId, "coordinator");
        Assert.Contains("we accept your request", accept.Body, StringComparison.Ordinal);
        Assert.StartsWith("Order Confirmation - Quotation ", accept.Subject);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.PreviewEmailAsync(fixture.Request.Id, quote.Id, "other", CoordinatorId, "coordinator"));

        // Nothing was sent and nothing changed.
        Assert.Empty(fixture.Sender.Sent);
        Assert.Empty(fixture.Outbox.Rows);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
        Assert.True((await fixture.Service.GetAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator")).CanEdit);
    }

    [Fact]
    public async Task TheCoordinatorCanEditTheEmailButTheAttachedPdfStaysTheSame()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));

        var sent = await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", new SendQuoteEmailRequest
        {
            SentTo = "quality@alnoor.com",
            Cc = ["boss@alnoor.com", "QUALITY@alnoor.com"],
            Subject = "Our offer for the crane",
            Message = "Dear team, our offer is attached."
        });

        var email = Assert.Single(fixture.Sender.Sent);
        Assert.Equal("quality@alnoor.com", email.ToAddress);
        Assert.Equal(["boss@alnoor.com"], email.Cc);
        Assert.Equal("Our offer for the crane", email.Subject);
        Assert.Equal("Dear team, our offer is attached.", email.BodyText);
        Assert.Contains("Dear team", email.BodyHtml, StringComparison.Ordinal);
        Assert.Equal("quotation.pdf", email.AttachmentFileName);
        Assert.Equal([1, 2, 3], email.AttachmentContent);
        Assert.Equal("<id@alnoor.com>", email.InReplyTo);
        var recorded = Assert.Single(sent.Emails!);
        Assert.Equal(["boss@alnoor.com"], recorded.Cc);
        Assert.Equal("Our offer for the crane", recorded.Subject);
        Assert.Equal("quality@alnoor.com", recorded.ToAddress);
        Assert.Equal(JobStatus.QuoteSent, fixture.Request.Status);
    }

    [Fact]
    public async Task WrongAddressesAreRefusedBeforeAnythingIsSentOrChanged()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));

        foreach (var request in new[]
                 {
                     new SendQuoteEmailRequest { SentTo = "not an email" },
                     new SendQuoteEmailRequest { SentTo = "a@b.com, c@d.com" },
                     new SendQuoteEmailRequest { Cc = ["nobody@localhost"] },
                     new SendQuoteEmailRequest { Cc = Enumerable.Range(1, 11).Select(index => $"person{index}@alnoor.com").ToArray() }
                 })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", request));
        }

        Assert.Empty(fixture.Sender.Sent);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
    }

    [Fact]
    public async Task AfterAFailureTheRetrySendsTheLastEdit()
    {
        var fixture = new Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        fixture.Sender.Failure = new EmailDeliveryException("The mail server could not be reached.");
        await Assert.ThrowsAsync<EmailDeliveryException>(() => fixture.Service.SendAsync(
            fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", new SendQuoteEmailRequest { Message = "First text" }));

        fixture.Sender.Failure = null;
        await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator",
            new SendQuoteEmailRequest { SentTo = "right@alnoor.com", Cc = ["copy@alnoor.com"], Message = "Second text" });

        var email = Assert.Single(fixture.Sender.Sent);
        Assert.Equal(("right@alnoor.com", "Second text"), (email.ToAddress, email.BodyText));
        var row = Assert.Single(fixture.Outbox.Rows);
        Assert.Equal(("copy@alnoor.com", 2), (row.CcAddresses, row.Attempts));
    }

    [Fact]
    public async Task ARequestWithoutAnEmailIsOnlyMarkedUnlessTheCoordinatorTypesAnAddress()
    {
        IEnumerable<RequestItem> Items() => [RequestItem.Create("Mobile crane - Liebherr LTM 1100", 1, type: "Mobile crane"), RequestItem.Create("Air receiver - 3000 L", 4, type: "Air receiver")];

        var plain = new Fixture(InspectionRequest.Create("Al Noor", "Cairo", null, Items(), CoordinatorId, managerId: ManagerId));
        var plainQuote = await plain.Service.CreateAsync(plain.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await plain.Service.UpdateAsync(plain.Request.Id, plainQuote.Id, CoordinatorId, "coordinator", Update(plain, 100, 200));
        var preview = await plain.Service.PreviewEmailAsync(plain.Request.Id, plainQuote.Id, "send", CoordinatorId, "coordinator");
        Assert.False(preview.CanSend);
        Assert.Contains("no address", Assert.Single(preview.Blockers), StringComparison.Ordinal);
        await plain.Service.SendAsync(plain.Request.Id, plainQuote.Id, CoordinatorId, "coordinator");
        Assert.Empty(plain.Sender.Sent);
        Assert.Equal(JobStatus.QuoteSent, plain.Request.Status);

        var typed = new Fixture(InspectionRequest.Create("Al Noor", "Cairo", null, Items(), CoordinatorId, managerId: ManagerId));
        var typedQuote = await typed.Service.CreateAsync(typed.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await typed.Service.UpdateAsync(typed.Request.Id, typedQuote.Id, CoordinatorId, "coordinator", Update(typed, 100, 200));
        await typed.Service.SendAsync(typed.Request.Id, typedQuote.Id, CoordinatorId, "coordinator", new SendQuoteEmailRequest { SentTo = "client@alnoor.com" });
        var email = Assert.Single(typed.Sender.Sent);
        Assert.Equal("client@alnoor.com", email.ToAddress);
        Assert.Null(email.InReplyTo);
        Assert.Equal(JobStatus.QuoteSent, typed.Request.Status);
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
        Assert.Equal(JobStatus.Unassigned, fixture.Request.Status);
        var email = Assert.Single(fixture.Sender.Sent);
        Assert.Contains("accept your request", email.BodyText, StringComparison.Ordinal);
        Assert.Equal(OutboundEmailKind.Accept, Assert.Single(accepted.Emails!).Kind);
        Assert.Contains(fixture.Request.History, entry => entry.ToStatus == JobStatus.QuoteSent);
        Assert.Contains(fixture.Request.History, entry => entry.ToStatus == JobStatus.Unassigned);
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
        Assert.Equal(JobStatus.Unassigned, sent.Fixture.Request.Status);
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
        Assert.Equal(JobStatus.QuoteSent, fixture.Request.Status);

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
        // A revision that was sent is a counter offer, not the first offer.
        Assert.Equal(JobStatus.CounterOfferSent, fixture.Request.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RecordDecisionAsync(
            fixture.Request.Id, first.Id, CoordinatorId, "coordinator", new QuoteDecisionRequest { Decision = QuoteStatus.Accepted }));
    }

    private static async Task<QuoteResponse> SentOfferAsync(Fixture fixture)
    {
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Update(fixture, 1_200_000, 850_000));
        return await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");
    }

    private static async Task<QuoteResponse> SentCounterOfferAsync(Fixture fixture)
    {
        await SentOfferAsync(fixture);
        var second = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, second.Id, CoordinatorId, "coordinator", Update(fixture, 1_100_000, 800_000));
        return await fixture.Service.SendAsync(fixture.Request.Id, second.Id, CoordinatorId, "coordinator");
    }

    [Fact]
    public async Task ACounterOfferThatWasSentCanBeAcceptedOrRejectedByTheCoordinator()
    {
        var accepted = new Fixture();
        var counter = await SentCounterOfferAsync(accepted);
        Assert.Equal(JobStatus.CounterOfferSent, accepted.Request.Status);

        var done = await accepted.Service.AcceptAsync(accepted.Request.Id, counter.Id, CoordinatorId, "coordinator");

        Assert.Equal(QuoteStatus.Accepted, done.Status);
        Assert.Equal(JobStatus.Unassigned, accepted.Request.Status);

        var rejected = new Fixture();
        await SentCounterOfferAsync(rejected);
        await rejected.Service.RejectRequestAsync(
            rejected.Request.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "The client went elsewhere" });
        Assert.Equal(JobStatus.QuoteRejected, rejected.Request.Status);
    }

    [Fact]
    public async Task AQuotationThatWasSentCanBeTakenBackByHandAndGoesOutAgainAsACounterOffer()
    {
        var fixture = new Fixture();
        var first = await SentOfferAsync(fixture);
        Assert.Equal(JobStatus.QuoteSent, fixture.Request.Status);

        // The price was talked over by phone: take it back, change it, send it again.
        var reopened = await fixture.Service.ReopenAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");

        Assert.Equal(2, reopened.Version);
        Assert.True(reopened.CanEdit);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
        Assert.Equal(850_000, reopened.Items!.Single(item => item.RequestItemId == fixture.Receiver.Id).UnitPricePiastres);
        var before = await fixture.Service.GetAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");
        Assert.NotNull(before.SupersededAt);
        Assert.NotNull(before.SentAt);

        // It cannot be taken back twice, and the replaced one is out of reach.
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ReopenAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ReopenAsync(fixture.Request.Id, reopened.Id, CoordinatorId, "coordinator"));

        await fixture.Service.UpdateAsync(fixture.Request.Id, reopened.Id, CoordinatorId, "coordinator", Update(fixture, 1_000_000, 700_000));
        await fixture.Service.SendAsync(fixture.Request.Id, reopened.Id, CoordinatorId, "coordinator");
        Assert.Equal(JobStatus.CounterOfferSent, fixture.Request.Status);

        // And again from the counter offer.
        var again = await fixture.Service.ReopenAsync(fixture.Request.Id, reopened.Id, CoordinatorId, "coordinator");
        Assert.Equal(3, again.Version);
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
        Assert.Equal(700_000, again.Items!.Single(item => item.RequestItemId == fixture.Receiver.Id).UnitPricePiastres);
    }

    [Fact]
    public async Task AQuotationCanBeTakenBackAfterTheClientAnsweredAndNotWhenItWasNeverSentOrAlreadyAccepted()
    {
        var fixture = new Fixture();
        var first = await SentOfferAsync(fixture);
        fixture.Request.TransitionTo(JobStatus.ClientCountered, CoordinatorId);

        var reopened = await fixture.Service.ReopenAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");
        Assert.Equal(JobStatus.QuoteDraft, fixture.Request.Status);
        Assert.Equal(2, reopened.Version);

        // A draft was never sent.
        var neverSent = new Fixture();
        var draft = await neverSent.Service.CreateAsync(neverSent.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await Assert.ThrowsAsync<InvalidOperationException>(() => neverSent.Service.ReopenAsync(neverSent.Request.Id, draft.Id, CoordinatorId, "coordinator"));

        // An accepted one is closed.
        var accepted = new Fixture();
        var offer = await SentOfferAsync(accepted);
        await accepted.Service.AcceptAsync(accepted.Request.Id, offer.Id, CoordinatorId, "coordinator");
        await Assert.ThrowsAsync<InvalidOperationException>(() => accepted.Service.ReopenAsync(accepted.Request.Id, offer.Id, CoordinatorId, "coordinator"));
    }

    [Fact]
    public async Task AnAnswerOfTheClientShowsOnTheQuotationAndTheNextOfferClearsIt()
    {
        var fixture = new Fixture();
        var first = await SentOfferAsync(fixture);
        Assert.Empty((await fixture.Service.GetAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator")).ClientReplies!);

        fixture.ClientAnswers("We need a better price.\n\nOn Monday, Eng. Ahmed wrote:\n> Dears, our offer is attached.", first.Id);

        Assert.Equal(JobStatus.ClientCountered, fixture.Request.Status);
        Assert.True(fixture.Request.HasUnreadClientReply);
        var shown = await fixture.Service.GetAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");
        var reply = Assert.Single(shown.ClientReplies!);
        Assert.Equal("buyer@alnoor.com", reply.FromAddress);
        Assert.Equal("We need a better price.", reply.Text);

        // The coordinator takes the quotation back: the answer stays in front of them while they change it.
        var draft = await fixture.Service.ReopenAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");
        Assert.Single(draft.ClientReplies!);
        Assert.Empty((await fixture.Service.GetAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator")).ClientReplies!);

        // The next offer goes out: the old answer is cleared.
        await fixture.Service.UpdateAsync(fixture.Request.Id, draft.Id, CoordinatorId, "coordinator", Update(fixture, 1_000_000, 700_000));
        var sent = await fixture.Service.SendAsync(fixture.Request.Id, draft.Id, CoordinatorId, "coordinator");
        Assert.Equal(JobStatus.CounterOfferSent, fixture.Request.Status);
        Assert.False(fixture.Request.HasUnreadClientReply);
        Assert.Empty(sent.ClientReplies!);
        Assert.Empty(fixture.Replies.Items);

        // And the cycle starts again with the answer to the counter offer.
        fixture.ClientAnswers("Better, but not yet.", draft.Id);
        Assert.Equal(JobStatus.ClientCountered, fixture.Request.Status);
        Assert.Equal("Better, but not yet.", Assert.Single((await fixture.Service.GetAsync(fixture.Request.Id, draft.Id, CoordinatorId, "coordinator")).ClientReplies!).Text);
    }

    [Fact]
    public async Task ADismissedAnswerGoesAwayAndTheRequestKeepsWaitingForTheClient()
    {
        var fixture = new Fixture();
        var first = await SentOfferAsync(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.DismissClientReplyAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator"));

        fixture.ClientAnswers("Thank you, we will look at it.", first.Id);
        var dismissed = await fixture.Service.DismissClientReplyAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");

        Assert.Equal(JobStatus.QuoteSent, fixture.Request.Status);
        Assert.False(fixture.Request.HasUnreadClientReply);
        Assert.Empty(dismissed.ClientReplies!);
        Assert.Empty(fixture.Replies.Items);

        // A counter offer goes back to counter_offer_sent.
        var revised = new Fixture();
        var offer = await SentOfferAsync(revised);
        var second = await revised.Service.ReopenAsync(revised.Request.Id, offer.Id, CoordinatorId, "coordinator");
        await revised.Service.UpdateAsync(revised.Request.Id, second.Id, CoordinatorId, "coordinator", Update(revised, 1_000_000, 700_000));
        await revised.Service.SendAsync(revised.Request.Id, second.Id, CoordinatorId, "coordinator");
        revised.ClientAnswers("Ok.", second.Id);
        await revised.Service.DismissClientReplyAsync(revised.Request.Id, second.Id, CoordinatorId, "coordinator");
        Assert.Equal(JobStatus.CounterOfferSent, revised.Request.Status);
    }

    [Fact]
    public async Task AnAnswerIsClearedWhenTheOfferIsAcceptedOrRejected()
    {
        var accepted = new Fixture();
        var offer = await SentOfferAsync(accepted);
        accepted.ClientAnswers("Fine, we accept.", offer.Id);
        await accepted.Service.AcceptAsync(accepted.Request.Id, offer.Id, CoordinatorId, "coordinator");
        Assert.Empty(accepted.Replies.Items);
        Assert.False(accepted.Request.HasUnreadClientReply);

        var rejected = new Fixture();
        await SentOfferAsync(rejected);
        rejected.ClientAnswers("No thanks.");
        await rejected.Service.RejectRequestAsync(rejected.Request.Id, CoordinatorId, "coordinator", new RejectRequestRequest { Reason = "Declined" });
        Assert.Empty(rejected.Replies.Items);
        Assert.False(rejected.Request.HasUnreadClientReply);
    }

    [Fact]
    public async Task ARequestWhoseClientAnsweredCanBeAcceptedOrRevised()
    {
        var fixture = new Fixture();
        var offer = await SentOfferAsync(fixture);
        fixture.Request.TransitionTo(JobStatus.ClientCountered, CoordinatorId);

        var accepted = await fixture.Service.AcceptAsync(fixture.Request.Id, offer.Id, CoordinatorId, "coordinator");

        Assert.Equal(QuoteStatus.Accepted, accepted.Status);
        Assert.Equal(JobStatus.Unassigned, fixture.Request.Status);
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
        public Fixture(InspectionRequest? request = null, bool configured = true, IRequestEquipmentRegistrar? registrar = null, QuoteDeliveryOptions? delivery = null, params AppUser[] extraUsers)
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
                new IncomingEmailRequestTests.FakeUserRepository([Coordinator, .. extraUsers]),
                Outbox,
                new FakeProtector(),
                Pdf,
                Sender,
                delivery);
            Replies = new FakeReplies();
            Service = new QuoteService(
                Quotes, Requests, Pdf, Delivery, Outbox, new IncomingEmailRequestTests.FakeUserRepository([Coordinator, .. extraUsers]), registrar,
                clientReplies: Replies);
        }

        public FakeReplies Replies { get; }

        /// <summary>The client answers the quotation email: the reply is kept and the request reacts, as the mailbox import does.</summary>
        public void ClientAnswers(string text, Guid? quoteId = null)
        {
            Replies.Items.Add(ClientReply.Create(
                ManagerId, Request.Id, quoteId,
                new IncomingEmail("1", Replies.Items.Count + 1, text, null, "buyer@alnoor.com", "Al Noor", "Re: Quotation", [], [], [], [], [],
                    $"<reply{Replies.Items.Count}@alnoor.com>", "offer@example.com", null, DateTimeOffset.UtcNow),
                DateTimeOffset.UtcNow));
            Request.ReceiveClientReply(CoordinatorId);
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

    internal sealed class FakeReplies : IClientReplyRepository
    {
        public List<ClientReply> Items { get; } = [];

        public Task<ReplyTarget?> FindTargetAsync(Guid managerId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReplyTarget?>(null);

        public Task<bool> IsOwnEmailAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ExistsAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<InspectionRequest?> GetRequestAsync(Guid managerId, Guid requestId, CancellationToken cancellationToken = default) => Task.FromResult<InspectionRequest?>(null);
        public Task AddAsync(ClientReply reply, InspectionRequest request, CancellationToken cancellationToken = default) { Items.Add(reply); return Task.CompletedTask; }

        public Task<IReadOnlyList<ClientReply>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ClientReply>>(Items.Where(reply => reply.RequestId == requestId).ToArray());

        public Task RemoveForRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(reply => reply.RequestId == requestId);
            return Task.CompletedTask;
        }
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
                    row.QuoteId, row.Kind, row.Status, row.ToAddress, row.Attempts, row.SentAt, row.LastError, row.CreatedAt, row.CcAddresses, row.Subject))
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
                .Where(quote => filter.InspectorId is not Guid inspectorId || quote.Request!.InspectorId == inspectorId)
                .Where(quote => filter.Assigned is not bool assigned || (quote.Request!.InspectorId is not null) == assigned)
                .Where(quote => filter.RequestStatuses is not { Count: > 0 } statuses || statuses.Contains(quote.Request!.Status))
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
        public Task<IReadOnlyDictionary<Guid, int>> CountActiveByInspectorAsync(IReadOnlyCollection<Guid> inspectorIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(requests
                .Where(request => request.InspectorId is Guid id && inspectorIds.Contains(id) && request.Status is >= JobStatus.Assigned and <= JobStatus.CertificateReturned)
                .GroupBy(request => request.InspectorId!.Value)
                .ToDictionary(group => group.Key, group => group.Count()));
        public Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
    }
}
