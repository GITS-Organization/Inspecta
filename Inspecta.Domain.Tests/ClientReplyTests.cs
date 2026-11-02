using System.Security.Claims;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>Emails that answer a quotation we sent: how they are recognised, kept with the request, and what they do to it.</summary>
public sealed class ClientReplyTests : IDisposable
{
    private static readonly Guid Manager = Guid.NewGuid();
    private static readonly Guid Coordinator = Guid.NewGuid();

    private static IncomingEmail Email(
        string? messageId, string? inReplyTo, string? references = null, string from = "buyer@alnoor.com", string text = "We need a better price.", bool automatic = false) =>
        new("1", 1, text, null, from, "Al Noor", "Re: Quotation", [], [], [], [], [], messageId, inReplyTo, references, DateTimeOffset.UtcNow, automatic);

    private static InspectionRequest Request(JobStatus status)
    {
        var request = InspectionRequest.Create(
            "Al Noor", "Site", Guid.NewGuid(), [RequestItem.Create("Crane", 1)], Coordinator, managerId: Manager);
        request.TransitionTo(JobStatus.QuoteDraft, Coordinator);
        if (status == JobStatus.QuoteDraft) return request;
        request.TransitionTo(status is JobStatus.CounterOfferSent ? JobStatus.CounterOfferSent : JobStatus.QuoteSent, Coordinator);
        if (status is JobStatus.QuoteSent or JobStatus.CounterOfferSent) return request;
        request.TransitionTo(status == JobStatus.Unassigned ? JobStatus.Unassigned : JobStatus.ClientCountered, Coordinator);
        return request;
    }

    // ---- what a reply does to the request ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(JobStatus.QuoteSent, JobStatus.ClientCountered)]
    [InlineData(JobStatus.CounterOfferSent, JobStatus.ClientCountered)]
    [InlineData(JobStatus.ClientCountered, JobStatus.ClientCountered)]
    [InlineData(JobStatus.QuoteDraft, JobStatus.QuoteDraft)]
    [InlineData(JobStatus.Unassigned, JobStatus.Unassigned)]
    public void AReplyBringsAWaitingRequestBackToTheCoordinatorAndOnlyFlagsTheOthers(JobStatus before, JobStatus after)
    {
        var request = Request(before);
        Assert.False(request.HasUnreadClientReply);

        request.ReceiveClientReply(Coordinator);

        Assert.Equal(after, request.Status);
        Assert.True(request.HasUnreadClientReply);
        if (before is JobStatus.QuoteSent or JobStatus.CounterOfferSent)
            Assert.Equal("The client replied by email.", request.History.Last().Note);
    }

    [Theory]
    [InlineData(JobStatus.QuoteSent)]
    [InlineData(JobStatus.CounterOfferSent)]
    public void ADismissedReplyPutsTheRequestBackToWaiting(JobStatus waiting)
    {
        var request = Request(JobStatus.ClientCountered);
        request.ReceiveClientReply(Coordinator);

        request.DismissClientReply(Coordinator, waiting);

        Assert.Equal(waiting, request.Status);
        Assert.False(request.HasUnreadClientReply);
        Assert.Throws<ArgumentOutOfRangeException>(() => request.DismissClientReply(Coordinator, JobStatus.Unassigned));
    }

    // ---- recognising a reply ----------------------------------------------------------------------------------------------

    private sealed class FakeRepository : IClientReplyRepository
    {
        public ReplyTarget? Target { get; set; }
        public bool Own { get; set; }
        public bool Exists { get; set; }
        public InspectionRequest? Found { get; set; }
        public List<ClientReply> Added { get; } = [];
        public IReadOnlyList<string> LastIds { get; private set; } = [];

        public Task<ReplyTarget?> FindTargetAsync(Guid managerId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default)
        {
            LastIds = messageIds;
            return Task.FromResult(Target);
        }

        public Task<bool> IsOwnEmailAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) => Task.FromResult(Own);
        public Task<bool> ExistsAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) => Task.FromResult(Exists);
        public Task<InspectionRequest?> GetRequestAsync(Guid managerId, Guid requestId, CancellationToken cancellationToken = default) => Task.FromResult(Found);
        public Task AddAsync(ClientReply reply, InspectionRequest request, CancellationToken cancellationToken = default) { Added.Add(reply); return Task.CompletedTask; }
        public Task<IReadOnlyList<ClientReply>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ClientReply>>(Added);
        public Task RemoveForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static Task<ClientReplyOutcome> Attach(FakeRepository repository, IncomingEmail email, params string[] own) =>
        new ClientReplyService(repository).TryAttachAsync(email, Manager, Coordinator, own);

    [Fact]
    public async Task AnEmailThatAnswersNothingIsImportedAsUsual()
    {
        var repository = new FakeRepository();

        // No In-Reply-To and no References: a new request.
        Assert.Equal(ClientReplyOutcome.NotAReply, await Attach(repository, Email("<new@alnoor.com>", null)));
        Assert.Empty(repository.LastIds);

        // A reply to something that is not one of our quotation emails.
        Assert.Equal(ClientReplyOutcome.NotAReply, await Attach(repository, Email("<x@alnoor.com>", "<somebody-else@mail.com>")));
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task AnAnswerToAQuotationIsKeptWithTheRequestAndBringsItBack()
    {
        var request = Request(JobStatus.CounterOfferSent);
        var quoteId = Guid.NewGuid();
        var repository = new FakeRepository { Target = new ReplyTarget(request.Id, quoteId), Found = request };

        var outcome = await Attach(repository, Email("<reply@alnoor.com>", "<ABC123@integrity-intl.com>", "<orig@alnoor.com> <abc123@integrity-intl.com>"));

        Assert.Equal(ClientReplyOutcome.Attached, outcome);
        // The ids are looked up lowercase, without the angle brackets, once each.
        Assert.Equal(["abc123@integrity-intl.com", "orig@alnoor.com"], repository.LastIds);
        var reply = Assert.Single(repository.Added);
        Assert.Equal(request.Id, reply.RequestId);
        Assert.Equal(quoteId, reply.QuoteId);
        Assert.Equal("We need a better price.", reply.TextBody);
        Assert.Equal(JobStatus.ClientCountered, request.Status);
        Assert.True(request.HasUnreadClientReply);
    }

    [Theory]
    [InlineData("automatic")]
    [InlineData("own address")]
    [InlineData("own email")]
    public async Task AnAutomaticReplyOrOurOwnEmailIsDroppedAndNeverBecomesARequest(string kind)
    {
        var request = Request(JobStatus.QuoteSent);
        var repository = new FakeRepository { Target = new ReplyTarget(request.Id, null), Found = request, Own = kind == "own email" };
        var email = Email("<r@alnoor.com>", "<offer@integrity-intl.com>", automatic: kind == "automatic", from: kind == "own address" ? "Coordinator@Integrity-Intl.com" : "buyer@alnoor.com");

        var outcome = await Attach(repository, email, "coordinator@integrity-intl.com");

        Assert.Equal(ClientReplyOutcome.Ignored, outcome);
        Assert.Empty(repository.Added);
        Assert.Equal(JobStatus.QuoteSent, request.Status);
        Assert.False(request.HasUnreadClientReply);
    }

    [Fact]
    public async Task AReplyThatWasKeptAlreadyIsNotKeptTwice()
    {
        var request = Request(JobStatus.QuoteSent);
        var repository = new FakeRepository { Target = new ReplyTarget(request.Id, null), Found = request, Exists = true };

        Assert.Equal(ClientReplyOutcome.Attached, await Attach(repository, Email("<r@alnoor.com>", "<offer@integrity-intl.com>")));

        Assert.Empty(repository.Added);
        Assert.Equal(JobStatus.QuoteSent, request.Status);
    }

    [Fact]
    public async Task ARequestThatIsGoneLeavesTheEmailToTheNormalImport()
    {
        var repository = new FakeRepository { Target = new ReplyTarget(Guid.NewGuid(), null), Found = null };

        Assert.Equal(ClientReplyOutcome.NotAReply, await Attach(repository, Email("<r@alnoor.com>", "<offer@integrity-intl.com>")));
    }

    // ---- against a real database -------------------------------------------------------------------------------------------

    private sealed class Database : IDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private readonly DbContextOptions<InspectaDbContext> options;
        public AppUser TeamManager = AppUser.Create("manager@acme.com", "Manager", UserRole.TechnicalManager);
        public AppUser OtherManager = AppUser.Create("other@acme.com", "Other", UserRole.TechnicalManager);
        public AppUser Mailbox = AppUser.Create("coordinator@acme.com", "Coordinator", UserRole.Coordinator);
        public Guid RequestId;
        public Guid QuoteId;
        public string OfferMessageId = string.Empty;

        public Database()
        {
            connection.Open();
            options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
            using var db = Background();
            db.Database.EnsureCreated();
            var client = Client.Create("Al Noor", managerId: TeamManager.Id);
            var original = new IncomingEmail(
                "1", 1, "Please quote", null, "buyer@alnoor.com", "Al Noor", "Quote", [], [], [], [], [], "orig@alnoor.com", null, null, DateTimeOffset.UtcNow);
            var request = InspectionRequest.CreateFromEmail(
                TeamManager.Id, Mailbox.Id, original, triage: RequestTriage.Request, clientId: client.Id, clientName: client.Name);
            typeof(InspectionRequest).GetProperty(nameof(InspectionRequest.Status))!.SetValue(request, JobStatus.QuoteSent);
            var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(14), Mailbox.Id);
            var offer = OutboundEmail.Create(TeamManager.Id, request.Id, quote.Id, OutboundEmailKind.Offer, "integrity-intl.com");
            (RequestId, QuoteId, OfferMessageId) = (request.Id, quote.Id, offer.MessageId);
            db.AddRange(TeamManager, OtherManager, Mailbox, client, request, quote, offer);
            db.SaveChanges();
        }

        public InspectaDbContext Background() => new(options, new CurrentTenant(new HttpContextAccessor()));

        public InspectaDbContext Signed(Guid? team = null) => new(options, new CurrentTenant(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "technical_manager"), new Claim("tenant_id", (team ?? TeamManager.Id).ToString())], "test"))
            }
        }));

        public void Dispose() => connection.Dispose();
    }

    [Fact]
    public async Task TheMailboxImportFindsTheQuotationAnEmailAnswers()
    {
        using var database = new Database();
        await using var db = database.Background();
        var repository = new EfClientReplyRepository(db);
        var offer = database.OfferMessageId.ToLowerInvariant();

        // By the email we sent.
        var target = await repository.FindTargetAsync(database.TeamManager.Id, [offer]);
        Assert.Equal(new ReplyTarget(database.RequestId, database.QuoteId), target);
        Assert.True(await repository.IsOwnEmailAsync(database.TeamManager.Id, offer));
        Assert.False(await repository.IsOwnEmailAsync(database.TeamManager.Id, "someone-else@mail.com"));

        // Never across teams.
        Assert.Null(await repository.FindTargetAsync(database.OtherManager.Id, [offer]));
        Assert.Null(await repository.FindTargetAsync(database.TeamManager.Id, ["unknown@mail.com"]));
        Assert.Null(await repository.FindTargetAsync(database.TeamManager.Id, []));

        // By the original email: only once a quotation email was sent.
        Assert.Null(await repository.FindTargetAsync(database.TeamManager.Id, ["orig@alnoor.com"]));
        var sent = await db.OutboundEmails.IgnoreQueryFilters().SingleAsync();
        typeof(OutboundEmail).GetProperty(nameof(OutboundEmail.Status))!.SetValue(sent, OutboundEmailStatus.Sent);
        await db.SaveChangesAsync();
        Assert.Equal(new ReplyTarget(database.RequestId, null), await repository.FindTargetAsync(database.TeamManager.Id, ["orig@alnoor.com"]));
    }

    [Fact]
    public async Task AReplyIsStoredWithTheRequestChangeAndClearedWithTheNextOffer()
    {
        using var database = new Database();
        var email = Email("<reply1@alnoor.com>", $"<{database.OfferMessageId}>", $"<orig@alnoor.com> <{database.OfferMessageId}>", text: "Please lower the price.");

        // The import has no signed-in user.
        await using (var import = database.Background())
        {
            var service = new ClientReplyService(new EfClientReplyRepository(import));
            Assert.Equal(ClientReplyOutcome.Attached, await service.TryAttachAsync(email, database.TeamManager.Id, database.Mailbox.Id, ["coordinator@acme.com"]));
        }

        // Importing it again (a retry) keeps one.
        await using (var again = database.Background())
            Assert.Equal(ClientReplyOutcome.Attached, await new ClientReplyService(new EfClientReplyRepository(again))
                .TryAttachAsync(email, database.TeamManager.Id, database.Mailbox.Id, []));

        await using (var manager = database.Signed())
        {
            var request = await manager.Requests.Include(item => item.History).SingleAsync(item => item.Id == database.RequestId);
            Assert.Equal(JobStatus.ClientCountered, request.Status);
            Assert.True(request.HasUnreadClientReply);
            Assert.Contains(request.History, entry => entry.ToStatus == JobStatus.ClientCountered);

            var repository = new EfClientReplyRepository(manager);
            var reply = Assert.Single(await repository.ListForRequestAsync(database.RequestId));
            Assert.Equal("Please lower the price.", reply.TextBody);
            Assert.Equal(database.QuoteId, reply.QuoteId);

            // The next offer clears it.
            await repository.RemoveForRequestAsync(database.RequestId);
            await manager.SaveChangesAsync();
        }

        await using var check = database.Signed();
        Assert.Empty(await new EfClientReplyRepository(check).ListForRequestAsync(database.RequestId));
    }

    [Fact]
    public async Task TheRepliesOfATeamAreNotSeenByAnotherTeam()
    {
        using var database = new Database();
        await using (var import = database.Background())
            await new ClientReplyService(new EfClientReplyRepository(import)).TryAttachAsync(
                Email("<reply1@alnoor.com>", $"<{database.OfferMessageId}>"), database.TeamManager.Id, database.Mailbox.Id, []);

        // Someone signed in to another team sees nothing, whatever the request id; the team itself sees it.
        await using var foreign = database.Signed(database.OtherManager.Id);
        Assert.Empty(await new EfClientReplyRepository(foreign).ListForRequestAsync(database.RequestId));
        await using var own = database.Signed();
        Assert.Single(await new EfClientReplyRepository(own).ListForRequestAsync(database.RequestId));
    }

    public void Dispose() { }
}
