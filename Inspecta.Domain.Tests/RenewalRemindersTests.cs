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
    public async Task AReplyToAReminderThatWentOutButWasMarkedFailedIsStillKeptWithItAndNeverBecomesARequest()
    {
        // The mail server accepted the message but the connection dropped before it said so: the row says failed, the client got the email and answers.
        await using var db = Context();
        sender.Failure = new EmailDeliveryException("timed out");
        await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send()));
        var failed = await db.Set<RenewalReminder>().SingleAsync();
        var requestsBefore = await db.Requests.CountAsync();

        var outcome = await Replies(db).TryAttachAsync(Reply("<" + failed.MessageId + ">"), manager.Id, coordinator.Id, []);

        Assert.Equal(ClientReplyOutcome.Attached, outcome);
        Assert.Equal(requestsBefore, await db.Requests.CountAsync());
        var shown = Assert.Single((await Service(db).ListRemindersAsync(manager.Id, client.Id, 1, 10)).Items);
        Assert.Equal("replied", shown.State);
        Assert.Single(shown.Replies);
    }

    [Fact]
    public async Task AReminderThatWasNeverSentFromHereDoesNotCatchTheRepliesOfStrangers()
    {
        await using var db = Context();

        var outcome = await Replies(db).TryAttachAsync(Reply("<" + Guid.NewGuid().ToString("N") + "@integrity-intl.com>"), manager.Id, coordinator.Id, []);

        Assert.Equal(ClientReplyOutcome.NotAReply, outcome);
    }

    [Fact]
    public void TheEmailLayoutWithoutAnAppendixIsExactlyAsBefore()
    {
        // What is expected is written out; the HTML of a plain text is the one the layout already makes for it, and the appendix follows it directly.
        var nl = Environment.NewLine;
        var build = EmailLayout.Build("Hello", null, null);
        Assert.Equal("Hello" + nl, build.Text);
        Assert.Contains("Hello", build.Html);
        Assert.DoesNotContain("TABLE", build.Html);

        var noBranding = EmailLayout.ForCoordinator("Hello", null, null, null, null);
        Assert.Equal("Hello", noBranding.Text);
        Assert.Equal(build.Html, noBranding.Html);
        Assert.Empty(noBranding.InlineImages);

        var withTable = EmailLayout.Build("Hello", null, null, appendix: ("TABLE-TEXT", "<table>TABLE-HTML</table>"));
        Assert.Equal("Hello" + nl + nl + "TABLE-TEXT" + nl, withTable.Text);
        Assert.Equal(build.Html + "<table>TABLE-HTML</table>", withTable.Html);

        var noBrandingWithTable = EmailLayout.ForCoordinator("Hello", null, null, null, null, appendix: ("TABLE-TEXT", "<table>TABLE-HTML</table>"));
        Assert.Equal("Hello" + nl + nl + "TABLE-TEXT", noBrandingWithTable.Text);
        Assert.Equal(build.Html + "<table>TABLE-HTML</table>", noBrandingWithTable.Html);

        var company = new EmailBranding("Integrity", "Alexandria", "www.integrity-intl.com", null);
        var signed = EmailLayout.ForCoordinator("Hello", coordinator, "hager.samy@integrity-intl.com", null, new Fixed(company), appendix: ("TABLE-TEXT", "<table>TABLE-HTML</table>"));
        Assert.True(signed.Text.IndexOf("TABLE-TEXT", StringComparison.Ordinal) < signed.Text.IndexOf("Hager Samy", StringComparison.Ordinal));
        Assert.True(signed.Text.IndexOf("Hello", StringComparison.Ordinal) < signed.Text.IndexOf("TABLE-TEXT", StringComparison.Ordinal));
    }

    private sealed class Fixed(EmailBranding value) : IEmailBranding
    {
        public EmailBranding Get() => value;
    }

    // ------------------------------------------------------------- the hard cases

    [Fact]
    public async Task EveryValueOfAMachineIsEncodedInTheHtmlOfTheEmail()
    {
        var odd = Machine("<b>Crane & \"Co\"</b>", "<i>SN-1</i>", 10);
        await using (var seed = Context()) { seed.Add(odd); seed.SaveChanges(); }
        await using var db = Context();

        await Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(machines: odd));

        var html = sender.Sent.Single().BodyHtml!;
        Assert.DoesNotContain("<b>Crane", html);
        Assert.DoesNotContain("<i>SN-1</i>", html);
        Assert.Contains("&lt;b&gt;Crane &amp; &quot;Co&quot;&lt;/b&gt;", html);
        Assert.Contains("&lt;i&gt;SN-1&lt;/i&gt;", html);
    }

    [Fact]
    public async Task TheSubjectIsCleanedWhateverTheClientLocationHolds()
    {
        var tricky = Client.Create("Tricky Co.", managerId: manager.Id, location: "Cairo\r\nBcc: spy@example.com", certificatesEmail: "certs@tricky.com");
        var machine = Item.Create(tricky.Id, "Crane");
        machine.RecordInspection(DateTimeOffset.UtcNow.AddMonths(-11), DateTimeOffset.UtcNow.AddDays(5));
        await using (var seed = Context()) { seed.AddRange(tricky, machine); seed.SaveChanges(); }
        await using var db = Context();

        await Service(db).SendReminderAsync(manager.Id, coordinator.Id, new SendRenewalReminderRequest(Guid.NewGuid(), tricky.Id, [machine.Id], "certs@tricky.com", "Dear client", null));

        var subject = sender.Sent.Single().Subject;
        Assert.DoesNotContain('\r', subject);
        Assert.DoesNotContain('\n', subject);
        // A subject made only of control characters falls back to the default one.
        await Service(db).SendReminderAsync(manager.Id, coordinator.Id, new SendRenewalReminderRequest(Guid.NewGuid(), tricky.Id, [machine.Id], "certs@tricky.com", "Dear client", "\r\n\t"));
        Assert.StartsWith("Periodic inspection due:", sender.Sent.Last().Subject);
    }

    [Fact]
    public async Task TheMailServersWordsAreNeverShownOrKeptOnTheReminder()
    {
        await using var db = Context();
        sender.Failure = new EmailDeliveryException("550 5.7.1 relay denied for 10.1.2.3 (internal-host.corp)");

        var error = await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send()));

        Assert.Equal("email_delivery_failed", error.Code);
        Assert.DoesNotContain("550", error.Message);
        Assert.DoesNotContain("internal-host", error.Message);
        var kept = await db.Set<RenewalReminder>().SingleAsync();
        Assert.DoesNotContain("internal-host", kept.LastError);
        Assert.DoesNotContain("550", kept.LastError);
    }

    [Fact]
    public async Task ANeedlessReconnectLeavesNoRowAndSendsNothing()
    {
        await using var db = Context();
        var service = new RenewalsService(
            new EfRenewalsRepository(db), new EfCoordinatorEmailConfigRepository(db), new EfUserRepository(db), new QuotationTests.FakeProtector(), sender,
            mailboxAuthenticator: new MustReconnect());

        var error = await Assert.ThrowsAsync<AuthException>(() => service.SendReminderAsync(manager.Id, coordinator.Id, Send()));

        Assert.Equal("mailbox_reconnect_required", error.Code);
        Assert.Empty(sender.Sent);
        Assert.Empty(await db.Set<RenewalReminder>().ToListAsync());
    }

    private sealed class MustReconnect : IMailboxAuthenticator
    {
        public Task<MailboxCredential> GetAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default) =>
            throw new AuthException("mailbox_reconnect_required", "Connect the mailbox again.", 409);
    }

    [Fact]
    public async Task TheSameKeyAnswersWithTheSentReminderEvenIfThingsChangedSince()
    {
        await using var db = Context();
        var key = Guid.NewGuid();
        var first = await Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(key));
        await using (var change = Context())
        {
            change.Items.Single(item => item.Id == crane.Id).Retire();
            change.SaveChanges();
        }

        // A machine was retired and emailing is switched off since; the second click is still answered, and nothing is sent again.
        var second = await Service(db, new QuoteDeliveryOptions(Enabled: false)).SendReminderAsync(manager.Id, coordinator.Id, Send(key));

        Assert.False(second.Created);
        Assert.Equal(first.Reminder.Id, second.Reminder.Id);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task ASendKeyBelongsToOneReminderOnly()
    {
        await using var db = Context();
        var key = Guid.NewGuid();
        await Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(key));
        var other = Client.Create("Other Co.", managerId: manager.Id, certificatesEmail: "certs@other.com");
        await using (var seed = Context()) { seed.Add(other); seed.SaveChanges(); }

        var error = await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(
            manager.Id, coordinator.Id, new SendRenewalReminderRequest(key, other.Id, [crane.Id], "certs@other.com", "Hello", null)));

        Assert.Equal("send_key_used", error.Code);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task TheDuplicateKeyRefusedByTheDatabaseIsReportedNotThrown()
    {
        var key = Guid.NewGuid();
        await using (var first = Context())
        {
            var winner = RenewalReminder.Create(manager.Id, client.Id, coordinator.Id, key, "integrity-intl.com");
            winner.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);
            Assert.True(await new EfRenewalsRepository(first).TryAddReminderAsync(winner));
        }
        await using var second = Context();
        var loser = RenewalReminder.Create(manager.Id, client.Id, coordinator.Id, key, "integrity-intl.com");
        loser.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);

        var added = await new EfRenewalsRepository(second).TryAddReminderAsync(loser);

        Assert.False(added);
        Assert.Single(await Context().Set<RenewalReminder>().ToListAsync());
    }

    [Fact]
    public async Task TwoClicksOnRetryOfAFailedReminderLetOnlyOneAttemptThrough()
    {
        var failed = RenewalReminder.Create(manager.Id, client.Id, coordinator.Id, Guid.NewGuid(), "integrity-intl.com");
        failed.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);
        failed.MarkFailed("refused");
        await using (var seed = Context()) { seed.Add(failed); seed.SaveChanges(); }

        await using var one = Context();
        await using var two = Context();
        var repositoryOne = new EfRenewalsRepository(one);
        var repositoryTwo = new EfRenewalsRepository(two);
        var rowOne = (await repositoryOne.GetReminderAsync(manager.Id, failed.Id))!;
        var rowTwo = (await repositoryTwo.GetReminderAsync(manager.Id, failed.Id))!;
        rowOne.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);
        rowTwo.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);

        var firstWon = await repositoryOne.TrySaveAsync();
        var secondWon = await repositoryTwo.TrySaveAsync();

        Assert.True(firstWon);
        Assert.False(secondWon);
        Assert.Equal(2, (await Context().Set<RenewalReminder>().SingleAsync()).Attempts);
    }

    [Fact]
    public void AReplyThatProvesDeliveryChangesTheRowSoARetryInProgressLosesAndALateFailureCannotUndoIt()
    {
        var reminder = RenewalReminder.Create(manager.Id, client.Id, coordinator.Id, Guid.NewGuid(), "integrity-intl.com");
        reminder.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);
        reminder.MarkFailed("refused");
        var before = reminder.UpdatedAt;

        reminder.RecordReply(before.AddSeconds(5));

        Assert.Equal(OutboundEmailStatus.Sent, reminder.Status);
        Assert.True(reminder.UpdatedAt > before, "the change of status must change the token");
        // A send that was still in flight reports its failure after the answer: the answer wins.
        reminder.MarkFailed("timed out");
        Assert.Equal(OutboundEmailStatus.Sent, reminder.Status);
        Assert.Equal("replied", reminder.State);
        // Times kept to the microsecond, as the database keeps them, so the token compares equal.
        Assert.Equal(0, reminder.UpdatedAt.Ticks % 10);
    }

    [Fact]
    public async Task AClosedReminderThatNeverWentOutIsNotSentByARetry()
    {
        await using var db = Context();
        var key = Guid.NewGuid();
        sender.Failure = new EmailDeliveryException("refused");
        await Assert.ThrowsAsync<AuthException>(() => Service(db).SendReminderAsync(manager.Id, coordinator.Id, Send(key)));
        var failed = await db.Set<RenewalReminder>().SingleAsync();
        Assert.True(await new EfRenewalsRepository(db).CloseReminderAsync(manager.Id, failed.Id, coordinator.Id, DateTimeOffset.UtcNow));
        sender.Failure = null;

        // The retry is another request: it reads the reminder fresh.
        await using var retry = Context();
        var error = await Assert.ThrowsAsync<AuthException>(() => Service(retry).SendReminderAsync(manager.Id, coordinator.Id, Send(key)));

        Assert.Equal("reminder_closed", error.Code);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task AReplyThatCannotBeSavedDoesNotLeaveBrokenStateForTheNextSavesOfTheSameRun()
    {
        var sent = RenewalReminder.Create(manager.Id, client.Id, coordinator.Id, Guid.NewGuid(), "integrity-intl.com");
        sent.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);
        sent.MarkSent();
        await using (var seed = Context()) { seed.Add(sent); seed.SaveChanges(); }
        var mail = Reply("<" + sent.MessageId + ">", messageId: "<same@deltasteel.com>");
        await using (var first = Context())
        {
            var repository = new EfRenewalsRepository(first);
            var reminder = (await repository.GetReminderAsync(manager.Id, sent.Id))!;
            await repository.AddReplyAsync(RenewalReminderReply.Create(manager.Id, sent.Id, mail, DateTimeOffset.UtcNow), reminder);
        }

        // The same reply arrives again from another mailbox at the same moment: the unique index refuses it.
        await using var db = Context();
        var again = new EfRenewalsRepository(db);
        var tracked = (await again.GetReminderAsync(manager.Id, sent.Id))!;
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => again.AddReplyAsync(RenewalReminderReply.Create(manager.Id, sent.Id, mail, DateTimeOffset.UtcNow), tracked));

        // The run goes on: an ordinary save in the same scope still works.
        db.Clients.Add(Client.Create("Another Co.", managerId: manager.Id));
        await db.SaveChangesAsync();
        Assert.Single(await Context().Set<RenewalReminderReply>().ToListAsync());
    }

    [Fact]
    public async Task ARequestAndACloseCannotBothWinTheSameReminder()
    {
        var sent = RenewalReminder.Create(manager.Id, client.Id, coordinator.Id, Guid.NewGuid(), "integrity-intl.com");
        sent.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);
        sent.MarkSent();
        await using (var seed = Context()) { seed.Add(sent); seed.SaveChanges(); }
        await using var db = Context();
        var repository = new EfRenewalsRepository(db);
        var now = DateTimeOffset.UtcNow;
        var requests = Enumerable.Range(0, 3).Select(_ => InspectionRequest.Create("Delta Steel Co.", "Site", client.Id, [RequestItem.Create("Crane", 1)], coordinator.Id, managerId: manager.Id)).ToList();
        await using (var seed = Context()) { seed.Requests.AddRange(requests); seed.SaveChanges(); }

        Assert.True(await repository.ClaimReminderAsync(manager.Id, sent.Id, requests[0].Id, coordinator.Id, now));
        Assert.False(await repository.ClaimReminderAsync(manager.Id, sent.Id, requests[1].Id, coordinator.Id, now));
        Assert.False(await repository.CloseReminderAsync(manager.Id, sent.Id, coordinator.Id, now));

        var other = RenewalReminder.Create(manager.Id, client.Id, coordinator.Id, Guid.NewGuid(), "integrity-intl.com");
        other.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);
        other.MarkSent();
        await using (var seed = Context()) { seed.Add(other); seed.SaveChanges(); }
        Assert.True(await repository.CloseReminderAsync(manager.Id, other.Id, coordinator.Id, now));
        Assert.False(await repository.ClaimReminderAsync(manager.Id, other.Id, requests[2].Id, coordinator.Id, now));
    }

    [Fact]
    public async Task ARequestCannotBeMadeFromAReminderWithoutATransactionToUndoIt()
    {
        var sent = RenewalReminder.Create(manager.Id, client.Id, coordinator.Id, Guid.NewGuid(), "integrity-intl.com");
        sent.BeginAttempt("certs@deltasteel.com", null, "hager.samy@integrity-intl.com", "Hager Samy", "Subject", "Message", []);
        sent.MarkSent();
        await using (var seed = Context()) { seed.Add(sent); seed.SaveChanges(); }
        await using var db = Context();
        var service = new RenewalsService(
            new EfRenewalsRepository(db), new EfCoordinatorEmailConfigRepository(db), new EfUserRepository(db), new QuotationTests.FakeProtector(), sender,
            inspectionRequests: System.Reflection.DispatchProxy.Create<IInspectionRequestService, NoRequests>(), transactions: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(manager.Id, coordinator.Id, new CreateRenewalRequest(client.Id, sent.Id, [crane.Id])));
    }

    private class NoRequests : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException("Not used by this test.");
    }


    public void Dispose() => connection.Dispose();
}
