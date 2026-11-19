using System.Net;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>A company that never uses Renewals sees no difference: nothing is written, sent or changed behind its back.</summary>
public sealed class RenewalsUnchangedTests(ApiHttpTests.Api api) : ExecutiveApiTestBase(api), IClassFixture<ApiHttpTests.Api>
{
    [Fact]
    public async Task ACompanyThatNeverUsesRenewalsHasNoReminderRowsAndItsMachinesAndFiguresAreAsBefore()
    {
        var world = SeedCompany();
        Api.SeedAs(db => db.Companies.Single(item => item.Id == world.Company.Id).KeepBuiltInIdentity());
        var client = SeedClient(world, "Steel", location: "Cairo");
        var overdue = SeedMachine(client, -4);
        var soon = SeedMachine(client, 9);
        SeedMachine(client, 200);
        var generalManager = await SignedInAs(world.General.Email);
        var coordinator = await SignedInAs(world.Coordinator.Email);

        var before = await Json(await generalManager.GetAsync("/api/v1/executive/renewals?filter=all&pageSize=100"));
        // Looking at the renewals and the history changes nothing and sends nothing.
        Assert.Equal(HttpStatusCode.OK, (await coordinator.GetAsync("/api/v1/renewals?window=90")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coordinator.GetAsync("/api/v1/renewals/reminders")).StatusCode);
        var after = await Json(await generalManager.GetAsync("/api/v1/executive/renewals?filter=all&pageSize=100"));

        Assert.Equal(before.GetRawText(), after.GetRawText());
        Api.SeedAs(db =>
        {
            Assert.Empty(db.RenewalReminders.IgnoreQueryFilters().Where(item => item.ManagerId == world.Company.Id));
            Assert.Empty(db.RenewalReminderReplies.IgnoreQueryFilters().Where(item => item.ManagerId == world.Company.Id));
            Assert.All(db.Items.Where(item => item.ClientId == client), item =>
            {
                Assert.Equal(EquipmentStatus.Active, item.Status);
                Assert.Null(item.RetiredAt);
            });
        });
        Assert.NotEqual(overdue, soon);
    }

    [Fact]
    public async Task TheApplicationsOwnReplyServiceKeepsAReplyToAReminderAndLeavesOtherMailAlone()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Steel", location: "Cairo");
        var reminder = RenewalReminder.Create(world.Company.Id, client, world.Coordinator.Id, Guid.NewGuid(), "integrity-intl.com");
        reminder.BeginAttempt("certs@steel.com", null, "hager@integrity-intl.com", "Hager Samy", "Periodic inspection due", "Dear client", []);
        reminder.MarkSent();
        Api.SeedAs(db => db.RenewalReminders.Add(reminder));

        // The real service, as the application builds it: if it stopped being given the renewals repository, a reply would become a request again.
        using var scope = Api.Services.CreateScope();
        var replies = scope.ServiceProvider.GetRequiredService<IClientReplyService>();
        IncomingEmail Mail(string? inReplyTo) => new(
            "1", 1, "We can do 2 December.", null, "someone@steel.com", "Someone", "Re: Periodic inspection due", [], [], [], [], [],
            $"<{Guid.NewGuid():N}@steel.com>", inReplyTo, null, DateTimeOffset.UtcNow);

        Assert.Equal(ClientReplyOutcome.Attached, await replies.TryAttachAsync(Mail("<" + reminder.MessageId + ">"), world.Company.Id, world.Coordinator.Id, []));
        Assert.Equal(ClientReplyOutcome.NotAReply, await replies.TryAttachAsync(Mail(null), world.Company.Id, world.Coordinator.Id, []));
        Assert.Equal(ClientReplyOutcome.NotAReply, await replies.TryAttachAsync(Mail("<unknown@x.com>"), world.Company.Id, world.Coordinator.Id, []));
    }

    [Fact]
    public async Task MarkingAMachineOutOfServiceTakesItOffEveryDueListAndUndoBringsItBack()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Steel", location: "Cairo");
        var machine = SeedMachine(client, 6);
        var coordinator = await SignedInAs(world.Coordinator.Email);
        var generalManager = await SignedInAs(world.General.Email);

        var retired = await coordinator.PostAsync($"/api/v1/equipment/{machine}/retire", null);
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var json = await Json(retired);
        Assert.Equal("retired", json.GetProperty("status").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, json.GetProperty("retiredAt").ValueKind);

        var list = await Json(await coordinator.GetAsync("/api/v1/renewals?window=30"));
        Assert.Equal(0, list.GetProperty("clients").GetArrayLength());
        var executive = await Json(await generalManager.GetAsync("/api/v1/executive/renewals?filter=all"));
        Assert.Equal(0, executive.GetProperty("total").GetInt32());

        var back = await Json(await coordinator.PostAsync($"/api/v1/equipment/{machine}/reactivate", null));
        Assert.Equal("active", back.GetProperty("status").GetString());
        Assert.Equal(1, (await Json(await coordinator.GetAsync("/api/v1/renewals?window=30"))).GetProperty("clients").GetArrayLength());

        // Not another company's machine, and not for an inspector.
        var stranger = SeedCompany();
        var strangerClient = SeedClient(stranger, "Theirs");
        var theirs = SeedMachine(strangerClient, 5);
        Assert.Equal(HttpStatusCode.NotFound, (await coordinator.PostAsync($"/api/v1/equipment/{theirs}/retire", null)).StatusCode);
        var inspector = await SignedInAs(world.Inspector.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await inspector.PostAsync($"/api/v1/equipment/{machine}/retire", null)).StatusCode);
    }
}
