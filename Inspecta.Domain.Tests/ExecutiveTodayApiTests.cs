using System.Net;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The Today screen over HTTP: the five numbers, the decisions, and the same scoping as the other screens.</summary>
public sealed class ExecutiveTodayApiTests(ApiHttpTests.Api api) : ExecutiveApiTestBase(api), IClassFixture<ApiHttpTests.Api>
{
    [Fact]
    public async Task The_five_numbers_carry_their_targets_and_whether_they_are_met()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Steel");
        SeedInvoice(world, 1_000_000, ageDays: 5, payments: [(3, 900_000)], clientId: client, clientName: "Steel");
        SeedMachine(client, -3);
        SeedMachine(client, 12);
        var gm = await SignedInAs(world.General.Email);

        var today = await Json(await gm.GetAsync($"/api/v1/executive/today?{Range(10)}"));

        var kpis = today.GetProperty("kpis");
        Assert.Equal(90, kpis.GetProperty("collected").GetProperty("ratePercent").GetInt32());
        Assert.True(kpis.GetProperty("collected").GetProperty("met").GetBoolean());
        Assert.Equal(100_000, kpis.GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(30, kpis.GetProperty("daysToCollect").GetProperty("targetDays").GetInt32());
        Assert.Equal(28, kpis.GetProperty("requestToDelivery").GetProperty("targetDays").GetInt32());
        Assert.Equal((1, 1, false), (kpis.GetProperty("machinesOverdue").GetProperty("count").GetInt32(), kpis.GetProperty("machinesOverdue").GetProperty("dueIn30").GetInt32(),
            kpis.GetProperty("machinesOverdue").GetProperty("met").GetBoolean()));
        Assert.Equal(12, today.GetProperty("monthly").GetArrayLength());
        Assert.Equal(4, today.GetProperty("pipeline").GetArrayLength());
        Assert.Equal("inbox", today.GetProperty("pipeline")[0].GetProperty("group").GetString());
        Assert.Equal(0, today.GetProperty("certificatesIssued").GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task An_old_unpaid_invoice_and_overdue_machines_become_ranked_decisions_with_where_open_goes()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Slow Steel");
        var (invoice, _) = SeedInvoice(world, 800_000, ageDays: 70, clientId: client, clientName: "Slow Steel");
        SeedMachine(client, -9);
        SeedMachine(client, -2);
        var gm = await SignedInAs(world.General.Email);

        var decisions = (await Json(await gm.GetAsync("/api/v1/executive/today"))).GetProperty("decisions").EnumerateArray().ToArray();

        Assert.Equal(2, decisions.Length);
        Assert.Equal(new[] { "machines_overdue", "invoice_unpaid_old" }.Order(), decisions.Select(item => item.GetProperty("titleCode").GetString()).Order());
        var money = decisions.Single(item => item.GetProperty("kind").GetString() == "money");
        Assert.Equal("red", money.GetProperty("severity").GetString());
        Assert.Equal("money", money.GetProperty("screen").GetString());
        Assert.Equal(invoice.Id, money.GetProperty("targetId").GetGuid());
        Assert.Equal(800_000, money.GetProperty("amountPiastres").GetInt64());
        Assert.Equal(70, money.GetProperty("waitingDays").GetInt32());
        Assert.Equal("Slow Steel", money.GetProperty("clientName").GetString());
        var renewals = decisions.Single(item => item.GetProperty("kind").GetString() == "renewals");
        Assert.Equal(client, renewals.GetProperty("targetId").GetGuid());
        Assert.Equal(9, renewals.GetProperty("waitingDays").GetInt32());
    }

    [Fact]
    public async Task A_settled_invoice_and_one_closed_by_hand_raise_no_decision()
    {
        var world = SeedCompany();
        SeedInvoice(world, 500_000, ageDays: 80, payments: [(70, 500_000)]);
        SeedInvoice(world, 500_000, ageDays: 90, closeByHand: true);
        var gm = await SignedInAs(world.General.Email);

        var today = await Json(await gm.GetAsync("/api/v1/executive/today"));

        Assert.Equal(0, today.GetProperty("decisions").GetArrayLength());
    }

    [Fact]
    public async Task The_reviewer_filter_narrows_the_decisions_and_the_pipeline_but_not_the_machines()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Steel");
        SeedInvoice(world, 800_000, ageDays: 70, reviewer: world.Manager, clientId: client, clientName: "Steel");
        SeedInvoice(world, 300_000, ageDays: 75, reviewer: world.SecondManager, clientId: client, clientName: "Steel");
        SeedMachine(client, -4);
        var gm = await SignedInAs(world.General.Email);

        var all = await Json(await gm.GetAsync("/api/v1/executive/today"));
        var mine = await Json(await gm.GetAsync($"/api/v1/executive/today?reviewerId={world.Manager.Id}"));

        Assert.Equal(1, all.GetProperty("kpis").GetProperty("machinesOverdue").GetProperty("count").GetInt32());
        Assert.Equal(1, mine.GetProperty("kpis").GetProperty("machinesOverdue").GetProperty("count").GetInt32());
        Assert.Equal(800_000 + 300_000, all.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(800_000, mine.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(3, all.GetProperty("decisions").GetArrayLength());
        Assert.Equal(2, mine.GetProperty("decisions").GetArrayLength());
    }

    [Fact]
    public async Task One_company_never_sees_another_and_an_empty_company_answers_zeros_and_the_rules_of_access_hold()
    {
        var first = SeedCompany();
        var second = SeedCompany();
        SeedInvoice(second, 700_000, ageDays: 90);
        var gm = await SignedInAs(first.General.Email);

        var today = await Json(await gm.GetAsync("/api/v1/executive/today"));

        Assert.Equal(0, today.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(0, today.GetProperty("decisions").GetArrayLength());
        Assert.Equal(0, today.GetProperty("kpis").GetProperty("machinesOverdue").GetProperty("count").GetInt32());
        Assert.True(today.GetProperty("kpis").GetProperty("machinesOverdue").GetProperty("met").GetBoolean());
        foreach (var email in new[] { first.Coordinator.Email, first.Finance.Email, first.CompanyAdmin.Email, first.Manager.Email })
            Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(email)).GetAsync("/api/v1/executive/today")).StatusCode);
        var owner = await SignedInAs(first.Owner.Email);
        var narrowed = await Json(await owner.GetAsync($"/api/v1/executive/today?managerId={second.Company.Id}"));
        Assert.Equal(700_000, narrowed.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
    }
}
