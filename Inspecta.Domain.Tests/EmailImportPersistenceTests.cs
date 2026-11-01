using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>The import of a mailbox message against a real database, the way the background service runs it (no signed-in user).</summary>
public sealed class EmailImportPersistenceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly AppUser manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
    private readonly AppUser coordinator = AppUser.Create("coordinator@example.com", "Coordinator", UserRole.Coordinator);
    private readonly Client client;

    public EmailImportPersistenceTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = Background();
        db.Database.EnsureCreated();
        client = Client.Create("Al Noor", location: "Borg El Arab", managerId: manager.Id, certificatesEmail: "certs@alnoor.com");
        client.ReplaceContacts([ClientContact.Create(client.Id, "Mohammed", "mohammed@alnoor.com", isPrimary: true)]);
        var receiver = Item.Create(client.Id, "Air Receiver");
        receiver.UpdateDetails("Air Receiver", null, null, null, "AR-1", "10 bar");
        db.AddRange(manager, coordinator, client, receiver);
        db.SaveChanges();
    }

    private InspectaDbContext Background() =>
        new(options, new CurrentTenant(new HttpContextAccessor()));

    private static IncomingEmail Email(string from, string body, long uid) => new(
        "1", uid, body, null, from, "Sender", "Quote request", [], [], [], [], [], $"<{uid}@mail>", null, null, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("anyone@alnoor.com", true)]
    [InlineData("mohammed@alnoor.com", true)]
    [InlineData("certs@alnoor.com", true)]
    [InlineData("stranger@elsewhere.com", false)]
    public async Task NewMailIsSavedAsARequestOrAMaybeAndNeverLost(string from, bool known)
    {
        await using var db = Background();
        if (from == "anyone@alnoor.com")
        {
            db.ClientMailRules.Add(ClientMailRule.Create(manager.Id, "alnoor.com", client.Id, coordinator.Id));
            await db.SaveChangesAsync();
        }

        var repository = new EfEmailSyncRepository(db);
        var rules = await repository.ListSenderRulesAsync(manager.Id);
        var rule = ClientMailRule.FindMatch(rules.Select(item => item.Rule), from);
        var sender = rule is null ? null : rules.First(item => item.Rule.Id == rule.Id);
        var incoming = Email(from, "Please inspect 4 air receivers.", 7);
        var request = InspectionRequest.CreateFromEmail(
            manager.Id, coordinator.Id, incoming,
            triage: sender?.ClientName is null ? RequestTriage.Maybe : RequestTriage.Request,
            clientId: sender?.ClientName is null ? null : rule!.ClientId,
            clientName: sender?.ClientName,
            location: sender?.ClientName is null ? null : sender.ClientLocation);
        if (sender?.ClientName is not null)
            await new MachineSuggestionService(new EfEquipmentCatalog(db)).AutofillAsync(request, incoming.TextBody, incoming.HtmlBody);

        Assert.True(await repository.SaveRequestIfNewAsync(request));

        await using var check = Background();
        var saved = await check.Requests.IgnoreQueryFilters().Include(item => item.Items).SingleAsync(item => item.Id == request.Id);
        Assert.Equal(known ? RequestTriage.Request : RequestTriage.Maybe, saved.Triage);
        Assert.Equal(known ? "Borg El Arab" : string.Empty, saved.Location);
        Assert.Equal(known ? 1 : 0, saved.Items.Count);
        if (known) Assert.Equal(4, saved.Items.Single().Quantity);
    }

    [Fact]
    public async Task WaitingMaybesOfASenderTheClientNowOwnsAreMovedOnTheNextCycle()
    {
        await using var db = Background();
        var fromCertificatesEmail = InspectionRequest.CreateFromEmail(
            manager.Id, coordinator.Id, Email("certs@alnoor.com", "Please inspect 3 air receivers.", 1), triage: RequestTriage.Maybe);
        var fromContact = InspectionRequest.CreateFromEmail(
            manager.Id, coordinator.Id, Email("MOHAMMED@alnoor.com", "Hello", 2), triage: RequestTriage.Maybe);
        var stranger = InspectionRequest.CreateFromEmail(
            manager.Id, coordinator.Id, Email("stranger@elsewhere.com", "Hello", 3), triage: RequestTriage.Maybe);
        var repository = new EfEmailSyncRepository(db);
        foreach (var request in new[] { fromCertificatesEmail, fromContact, stranger })
            Assert.True(await repository.SaveRequestIfNewAsync(request));

        var senders = await repository.ListSenderRulesAsync(manager.Id);
        var moved = await repository.PromoteMatchingMaybesAsync(
            manager.Id, senders, request => new MachineSuggestionService(new EfEquipmentCatalog(db)).AutofillAsync(request, request.Email?.TextBody, request.Email?.HtmlBody));

        Assert.Equal(2, moved);
        await using var check = Background();
        var saved = await check.Requests.IgnoreQueryFilters().Include(item => item.Items).ToDictionaryAsync(item => item.Id);
        Assert.Equal(RequestTriage.Request, saved[fromCertificatesEmail.Id].Triage);
        Assert.Equal(client.Id, saved[fromCertificatesEmail.Id].ClientId);
        Assert.Equal("Borg El Arab", saved[fromCertificatesEmail.Id].Location);
        Assert.Equal(3, Assert.Single(saved[fromCertificatesEmail.Id].Items).Quantity);
        Assert.Equal(RequestTriage.Request, saved[fromContact.Id].Triage);
        Assert.Equal(RequestTriage.Maybe, saved[stranger.Id].Triage);
        Assert.Equal(0, await repository.PromoteMatchingMaybesAsync(manager.Id, senders));
    }

    public void Dispose() => connection.Dispose();
}
