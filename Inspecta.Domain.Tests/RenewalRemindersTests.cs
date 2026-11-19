using System.Security.Claims;
using Inspecta.Application.Contracts.Renewals;
using Inspecta.Application.Exceptions;
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

/// <summary>
/// The reminder a coordinator emails to a client about machines that are due: sent only on request, from the coordinator's own mailbox, once per click,
/// and the client's answer is kept with the reminder and never becomes a request.
/// </summary>
public sealed class RenewalRemindersTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly AppUser manager = AppUser.Create("tm@integrity-intl.com", "Technical Manager", UserRole.TechnicalManager);
    private readonly AppUser coordinator;
    private readonly Company company;
    private readonly Client client;
    private readonly Item crane;
    private readonly Item hoist;
    private readonly Item forklift;
    private readonly QuotationTests.FakeSender sender = new();

    public RenewalRemindersTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = Context();
        db.Database.EnsureCreated();
        company = Company.Create("Integrity", id: manager.Id);
        company.KeepBuiltInIdentity();
        coordinator = AppUser.Create("hager.samy@integrity-intl.com", "Hager Samy", UserRole.Coordinator, managerId: manager.Id);
        client = Client.Create("Delta Steel Co.", managerId: manager.Id, location: "10th of Ramadan", certificatesEmail: "certs@deltasteel.com");
        crane = Machine("Overhead crane 10 t", "OC-1021", 13);
        hoist = Machine("Chain hoist 2 t", "CH-2204", -6);
        forklift = Machine("Forklift 3 t", "FK-3310", 25);
        db.AddRange(manager, coordinator, company, client, crane, hoist, forklift);
        db.ClientContacts.Add(ClientContact.Create(client.Id, "Ahmed Fawzy", "ahmed.fawzy@deltasteel.com", role: "Maintenance manager"));
        db.CoordinatorEmailConfigs.Add(CoordinatorEmailConfig.Create(
            coordinator.Id, manager.Id, "imap.integrity-intl.com", 993, "hager.samy@integrity-intl.com", "secret", true));
        db.SaveChanges();
    }

    private Item Machine(string name, string serial, int dueInDays)
    {
        var item = Item.Create(client.Id, name);
        item.UpdateDetails(name, null, null, null, serial, null);
        var due = DateTimeOffset.UtcNow.AddDays(dueInDays);
        item.RecordInspection(due.AddYears(-1), due);
        return item;
    }

    private InspectaDbContext Context()
    {
        var admin = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
        };
        return new InspectaDbContext(options, new CurrentTenant(admin));
    }

    private RenewalsService Service(InspectaDbContext db, QuoteDeliveryOptions? delivery = null) => new(
        new EfRenewalsRepository(db), new EfCoordinatorEmailConfigRepository(db), new EfUserRepository(db), new QuotationTests.FakeProtector(), sender, delivery);

    private SendRenewalReminderRequest Send(Guid? key = null, string to = "certs@deltasteel.com", params Item[] machines) => new(
        key ?? Guid.NewGuid(), client.Id, (machines.Length == 0 ? [crane, hoist] : machines).Select(item => item.Id).ToList(), to,
        "Dear Delta Steel Co. team, these machines are due. Please tell us which to inspect.", null);

    [Fact]
    public async Task SendingAReminderUsesTheCallersMailboxAndListsTheMachines()
    {
        await using var db = Context();

        var result = await Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send());

        Assert.True(result.Created);
        var email = Assert.Single(sender.Sent);
        Assert.Equal("hager.samy@integrity-intl.com", email.FromAddress);
        Assert.Equal("certs@deltasteel.com", email.ToAddress);
        Assert.Null(email.AttachmentFileName);
        Assert.Null(email.AttachmentContent);
        Assert.Contains("Periodic inspection due: 2 machines at 10th of Ramadan", email.Subject);
        Assert.Contains("Dear Delta Steel Co. team", email.BodyText);
        foreach (var machine in new[] { "Overhead crane 10 t", "OC-1021", "Chain hoist 2 t", "CH-2204" })
        {
            Assert.Contains(machine, email.BodyText);
            Assert.Contains(machine, email.BodyHtml);
        }
        Assert.DoesNotContain("Forklift 3 t", email.BodyText);
        Assert.Equal("imap.integrity-intl.com".Replace("imap", "smtp"), sender.LastSmtp!.Server);
        Assert.Equal("sent", result.Reminder.State);
        Assert.Equal(["Chain hoist 2 t", "Overhead crane 10 t"], result.Reminder.Machines.Select(item => item.Name).Order().ToArray());
        Assert.Equal("Hager Samy", result.Reminder.SentBy.Name);
        Assert.Equal(1, result.Reminder.Attempts);
    }

    [Fact]
    public async Task ThePreviewShowsTheEmailAndSendsAndSavesNothing()
    {
        await using var db = Context();

        var preview = await Service(db).PreviewReminderAsync(manager.Id, coordinator.Id, Send());

        Assert.Empty(preview.Blockers);
        Assert.Contains("OC-1021", preview.Html);
        Assert.Contains("Dear Delta Steel Co. team", preview.Text);
        Assert.Equal("certs@deltasteel.com", preview.ToAddress);
        Assert.Empty(sender.Sent);
        Assert.Empty(await db.Set<RenewalReminder>().ToListAsync());
    }

    [Fact]
    public async Task ADoubleClickSendsOnce()
    {
        await using var db = Context();
        var key = Guid.NewGuid();

        var first = await Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(key));
        var second = await Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(key));

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Reminder.Id, second.Reminder.Id);
        Assert.Single(sender.Sent);
        Assert.Single(await db.Set<RenewalReminder>().ToListAsync());
    }

    [Fact]
    public async Task AFailedReminderIsKeptAndCanBeRetriedWithTheSameKey()
    {
        await using var db = Context();
        var key = Guid.NewGuid();
        sender.Failure = new EmailDeliveryException("The server refused the message.");

        var failure = await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(key)));
        Assert.Equal("email_delivery_failed", failure.Code);
        Assert.Equal(502, failure.StatusCode);
        var kept = Assert.Single(await db.Set<RenewalReminder>().ToListAsync());
        Assert.Equal(OutboundEmailStatus.Failed, kept.Status);
        Assert.Equal("failed", kept.State);

        sender.Failure = null;
        var retry = await Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(key));

        Assert.Equal("sent", retry.Reminder.State);
        Assert.Equal(2, retry.Reminder.Attempts);
        Assert.Single(sender.Sent);
        Assert.Single(await db.Set<RenewalReminder>().ToListAsync());
    }

    [Fact]
    public async Task ARecipientThatIsNotTheClientsOwnAddressIsRefusedAndNothingIsSaved()
    {
        await using var db = Context();

        var error = await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(to: "someone@elsewhere.com")));

        Assert.Equal("recipient_not_of_client", error.Code);
        Assert.Empty(sender.Sent);
        Assert.Empty(await db.Set<RenewalReminder>().ToListAsync());

        // A contact's address and the certificates email, in any letter case, are the client's own.
        await Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(to: "Ahmed.Fawzy@DeltaSteel.com"));
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task AMachineOfAnotherClientOrOutOfServiceOrWithoutADateIsRefused()
    {
        var stranger = Client.Create("Other Co.", managerId: manager.Id);
        var theirs = Item.Create(stranger.Id, "Their crane");
        var undated = Item.Create(client.Id, "Undated");
        await using (var seed = Context())
        {
            seed.AddRange(stranger, theirs, undated);
            seed.Items.Single(item => item.Id == forklift.Id).Retire();
            seed.SaveChanges();
        }
        await using var db = Context();

        foreach (var machine in new[] { theirs, undated, forklift })
        {
            var error = await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(machines: machine)));
            Assert.Equal("machine_not_eligible", error.Code);
        }
        Assert.Empty(sender.Sent);
        var none = await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send() with { EquipmentIds = [] }));
        Assert.Equal("machines_required", none.Code);
    }

    [Fact]
    public async Task WithoutAMailboxTheReminderIsRefusedAndNoRowIsWritten()
    {
        await using var db = Context();

        // The technical manager cannot connect a mailbox.
        var error = await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(manager.Id, manager.Id, Send()));

        Assert.Equal("mailbox_not_configured", error.Code);
        Assert.Equal(409, error.StatusCode);
        Assert.Empty(sender.Sent);
        Assert.Empty(await db.Set<RenewalReminder>().ToListAsync());
    }

    [Fact]
    public async Task EmailingSwitchedOffSendsNothing()
    {
        await using var db = Context();

        var error = await Assert.ThrowsAsync<AuthException>(() => Service(db, new QuoteDeliveryOptions(Enabled: false)).SendReminderAsync(manager.Id, coordinator.Id, Send()));

        Assert.Equal("email_disabled", error.Code);
        Assert.Empty(sender.Sent);
        Assert.Empty(await db.Set<RenewalReminder>().ToListAsync());
    }

    [Fact]
    public async Task AnotherCompanysClientIsNotFound()
    {
        await using var db = Context();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).SendReminderAsync(Guid.NewGuid(), coordinator.Id, Send()));
    }

    [Fact]
    public async Task TheListShowsTheLatestReminderOfEachClientAndItsState()
    {
        await using var db = Context();
        var service = Service(db);
        await service.SendReminderAsync(manager.Id, coordinator.Id, Send());

        var listed = await service.ListAsync(manager.Id, "30", null, 1, 50);

        var summary = Assert.Single(listed.Clients).LatestReminder!;
        Assert.Equal("sent", summary.State);
        Assert.Equal("certs@deltasteel.com", summary.ToAddress);
        Assert.Equal(2, summary.MachineCount);
    }

    // ------------------------------------------------------------- the client's answer

    private IncomingEmail Reply(string? inReplyTo, string? references = null, bool automatic = false, string? from = "mahmoud@deltasteel.com", string? messageId = null) => new(
        "1", 7, "Thanks. We can inspect on 2 December.", null, from, "Mahmoud Salem", "Re: Periodic inspection due", [], [], [], [], [],
        messageId ?? $"<{Guid.NewGuid():N}@deltasteel.com>", inReplyTo, references, DateTimeOffset.UtcNow, automatic);

    private ClientReplyService Replies(InspectaDbContext db) => new(new EfClientReplyRepository(db), renewals: new EfRenewalsRepository(db));

    private async Task<string> SentMessageId(InspectaDbContext db)
    {
        await Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send());
        return "<" + sender.Sent.Single().MessageId + ">";
    }

    [Fact]
    public async Task AReplyToAReminderIsLinkedAndCreatesNoRequest()
    {
        await using var db = Context();
        var messageId = await SentMessageId(db);
        var requestsBefore = await db.Requests.CountAsync();

        var outcome = await Replies(db).TryAttachAsync(Reply(messageId), manager.Id, coordinator.Id, ["hager.samy@integrity-intl.com"]);

        Assert.Equal(ClientReplyOutcome.Attached, outcome);
        Assert.Equal(requestsBefore, await db.Requests.CountAsync());
        var reminder = await Service(db).ListRemindersAsync(manager.Id, client.Id, 1, 10);
        var shown = Assert.Single(reminder.Items);
        Assert.Equal("replied", shown.State);
        var reply = Assert.Single(shown.Replies);
        Assert.Equal("mahmoud@deltasteel.com", reply.FromAddress);
        Assert.Contains("2 December", reply.Text);
    }

    [Fact]
    public async Task AReplyFoundOnlyByTheReferencesHeaderIsLinkedToo()
    {
        await using var db = Context();
        var messageId = await SentMessageId(db);

        var outcome = await Replies(db).TryAttachAsync(Reply("<something-else@x.com>", references: $"<older@x.com> {messageId}"), manager.Id, coordinator.Id, []);

        Assert.Equal(ClientReplyOutcome.Attached, outcome);
    }

    [Fact]
    public async Task AnAutomaticReplyOrOurOwnCopyIsDroppedAndKeptNowhere()
    {
        await using var db = Context();
        var messageId = await SentMessageId(db);

        var automatic = await Replies(db).TryAttachAsync(Reply(messageId, automatic: true), manager.Id, coordinator.Id, []);
        var fromTheMailbox = await Replies(db).TryAttachAsync(Reply(messageId, from: "hager.samy@integrity-intl.com"), manager.Id, coordinator.Id, ["hager.samy@integrity-intl.com"]);
        var ourOwn = await Replies(db).TryAttachAsync(Reply(messageId, messageId: messageId), manager.Id, coordinator.Id, []);

        Assert.Equal(ClientReplyOutcome.Ignored, automatic);
        Assert.Equal(ClientReplyOutcome.Ignored, fromTheMailbox);
        Assert.Equal(ClientReplyOutcome.Ignored, ourOwn);
        Assert.Empty(await db.Set<RenewalReminderReply>().ToListAsync());
    }

    [Fact]
    public async Task TheSameReplyImportedTwiceIsKeptOnce()
    {
        await using var db = Context();
        var messageId = await SentMessageId(db);
        var reply = Reply(messageId);

        await Replies(db).TryAttachAsync(reply, manager.Id, coordinator.Id, []);
        var again = await Replies(db).TryAttachAsync(reply, manager.Id, coordinator.Id, []);

        Assert.Equal(ClientReplyOutcome.Attached, again);
        Assert.Single(await db.Set<RenewalReminderReply>().ToListAsync());
    }

    [Fact]
    public async Task AReplyToAnotherCompanysReminderIsNotLinkedAndAFreshEmailIsNotAReply()
    {
        await using var db = Context();
        var messageId = await SentMessageId(db);

        var otherCompany = await Replies(db).TryAttachAsync(Reply(messageId), Guid.NewGuid(), Guid.NewGuid(), []);
        var fresh = await Replies(db).TryAttachAsync(Reply(null), manager.Id, coordinator.Id, []);
        var unknown = await Replies(db).TryAttachAsync(Reply("<nobody@x.com>"), manager.Id, coordinator.Id, []);

        Assert.Equal(ClientReplyOutcome.NotAReply, otherCompany);
        Assert.Equal(ClientReplyOutcome.NotAReply, fresh);
        Assert.Equal(ClientReplyOutcome.NotAReply, unknown);
        Assert.Empty(await db.Set<RenewalReminderReply>().ToListAsync());
    }

    [Fact]
    public async Task AReminderThatWasNeverSentDoesNotCatchReplies()
    {
        await using var db = Context();
        sender.Failure = new EmailDeliveryException("refused");
        await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send()));
        var failedId = (await db.Set<RenewalReminder>().SingleAsync()).MessageId;

        var outcome = await Replies(db).TryAttachAsync(Reply("<" + failedId + ">"), manager.Id, coordinator.Id, []);

        Assert.Equal(ClientReplyOutcome.NotAReply, outcome);
    }

    [Fact]
    public void TheEmailLayoutWithoutAnAppendixIsExactlyAsBefore()
    {
        var plain = EmailLayout.Build("Hello", null, null);
        var withNothing = EmailLayout.Build("Hello", null, null, appendix: null);
        var withTable = EmailLayout.Build("Hello", null, null, appendix: ("TABLE-TEXT", "<table>TABLE-HTML</table>"));

        Assert.Equal(plain.Text, withNothing.Text);
        Assert.Equal(plain.Html, withNothing.Html);
        Assert.Contains("TABLE-TEXT", withTable.Text);
        Assert.Contains("TABLE-HTML", withTable.Html);
        Assert.True(withTable.Text.IndexOf("Hello", StringComparison.Ordinal) < withTable.Text.IndexOf("TABLE-TEXT", StringComparison.Ordinal));
    }

    public void Dispose() => connection.Dispose();
}
