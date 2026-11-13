using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The Money screen over HTTP: the figures, who may ask, and that one company never sees another's.</summary>
public sealed class ExecutiveMoneyApiTests(ApiHttpTests.Api api) : ExecutiveApiTestBase(api), IClassFixture<ApiHttpTests.Api>
{
    [Fact]
    public async Task The_figures_follow_the_definitions_for_a_period()
    {
        var world = SeedCompany();
        SeedInvoice(world, 1_000_000, ageDays: 5, payments: [(3, 400_000)]);
        SeedInvoice(world, 500_000, ageDays: 28, payments: [(20, 500_000)]);
        var gm = await SignedInAs(world.General.Email);

        var money = await Json(await gm.GetAsync($"/api/v1/executive/money?{Range(30)}"));

        var kpis = money.GetProperty("kpis");
        Assert.Equal(1_500_000, kpis.GetProperty("invoiced").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(900_000, kpis.GetProperty("collected").GetProperty("collectedPiastres").GetInt64());
        Assert.Equal(60, kpis.GetProperty("collected").GetProperty("ratePercent").GetInt32());
        Assert.Equal(85, kpis.GetProperty("collected").GetProperty("targetPercent").GetInt32());
        Assert.False(kpis.GetProperty("collected").GetProperty("met").GetBoolean());
        Assert.Equal(600_000, kpis.GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(8, kpis.GetProperty("daysToCollect").GetProperty("days").GetInt32());
        Assert.Equal(30, kpis.GetProperty("daysToCollect").GetProperty("targetDays").GetInt32());
        Assert.True(kpis.GetProperty("daysToCollect").GetProperty("met").GetBoolean());
        Assert.Equal(12, money.GetProperty("monthly").GetArrayLength());
        Assert.Equal(Today.AddDays(-30).ToString("yyyy-MM-dd"), money.GetProperty("period").GetProperty("from").GetString());
    }

    [Fact]
    public async Task An_invoice_70_days_old_is_in_the_61_to_90_bucket_and_leaves_it_when_paid()
    {
        var world = SeedCompany();
        var (invoice, _) = SeedInvoice(world, 800_000, ageDays: 70, clientName: "Slow Payer");
        var gm = await SignedInAs(world.General.Email);

        var before = await Json(await gm.GetAsync("/api/v1/executive/money"));
        Api.SeedAs(db => db.Invoices.Include(item => item.Payments).Single(item => item.Id == invoice.Id)
            .RecordPayment(800_000, Today, null, world.Coordinator.Id, Today));
        var after = await Json(await gm.GetAsync("/api/v1/executive/money"));

        var bucket = before.GetProperty("aging").EnumerateArray().Single(item => item.GetProperty("bucket").GetString() == "61_90");
        Assert.Equal(800_000, bucket.GetProperty("amountPiastres").GetInt64());
        Assert.Equal(800_000, before.GetProperty("kpis").GetProperty("outstanding").GetProperty("olderThan60Piastres").GetInt64());
        Assert.Equal("Slow Payer", before.GetProperty("whoOwesMost")[0].GetProperty("clientName").GetString());
        Assert.Equal(0, after.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(0, after.GetProperty("whoOwesMost").GetArrayLength());
    }

    [Fact]
    public async Task An_invoice_closed_by_hand_is_out_of_outstanding_and_shows_only_as_its_own_figure()
    {
        var world = SeedCompany();
        SeedInvoice(world, 300_000, ageDays: 10, payments: [(9, 100_000)], closeByHand: true);
        SeedInvoice(world, 200_000, ageDays: 10);
        var gm = await SignedInAs(world.General.Email);

        var outstanding = (await Json(await gm.GetAsync("/api/v1/executive/money"))).GetProperty("kpis").GetProperty("outstanding");

        Assert.Equal(200_000, outstanding.GetProperty("totalPiastres").GetInt64());
        Assert.Equal(200_000, outstanding.GetProperty("closedByHandPiastres").GetInt64());
    }

    [Fact]
    public async Task Only_the_general_manager_and_the_platform_owner_may_ask()
    {
        var world = SeedCompany();
        foreach (var email in new[] { world.Coordinator.Email, world.Finance.Email, world.CompanyAdmin.Email, world.Manager.Email, world.Inspector.Email })
        {
            var user = await SignedInAs(email);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/executive/money")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await (await SignedInAs(world.General.Email)).GetAsync("/api/v1/executive/money")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await SignedInAs(world.Owner.Email)).GetAsync($"/api/v1/executive/money?managerId={world.Company.Id}")).StatusCode);
    }

    [Fact]
    public async Task One_company_never_sees_the_money_of_another_and_the_owner_can_narrow_to_one()
    {
        var first = SeedCompany();
        var second = SeedCompany();
        SeedInvoice(first, 700_000, ageDays: 3);
        SeedInvoice(second, 90_000, ageDays: 3);
        var gm = await SignedInAs(first.General.Email);
        var owner = await SignedInAs(first.Owner.Email);

        var own = await Json(await gm.GetAsync("/api/v1/executive/money"));
        var asked = await Json(await gm.GetAsync($"/api/v1/executive/money?managerId={second.Company.Id}"));
        var narrowed = await Json(await owner.GetAsync($"/api/v1/executive/money?managerId={second.Company.Id}"));

        Assert.Equal(700_000, own.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(700_000, asked.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(90_000, narrowed.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
    }

    [Fact]
    public async Task The_reviewer_filter_narrows_money_and_the_invoice_list_the_same_way()
    {
        var world = SeedCompany();
        SeedInvoice(world, 400_000, ageDays: 4, reviewer: world.Manager);
        SeedInvoice(world, 100_000, ageDays: 4, reviewer: world.SecondManager);
        var gm = await SignedInAs(world.General.Email);

        var money = await Json(await gm.GetAsync($"/api/v1/executive/money?reviewerId={world.Manager.Id}"));
        var list = await Json(await gm.GetAsync($"/api/v1/invoices?reviewerId={world.Manager.Id}"));
        var everything = await Json(await gm.GetAsync("/api/v1/invoices"));

        Assert.Equal(400_000, money.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(2, everything.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task An_empty_company_answers_zeros_and_empty_lists_and_a_wrong_period_is_a_400()
    {
        var world = SeedCompany();
        var gm = await SignedInAs(world.General.Email);

        var money = await Json(await gm.GetAsync("/api/v1/executive/money"));
        var backwards = await gm.GetAsync($"/api/v1/executive/money?from={Today:yyyy-MM-dd}&to={Today.AddDays(-5):yyyy-MM-dd}");
        var tooLong = await gm.GetAsync($"/api/v1/executive/money?from={Today.AddDays(-800):yyyy-MM-dd}");

        Assert.Equal(0, money.GetProperty("kpis").GetProperty("invoiced").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(0, money.GetProperty("kpis").GetProperty("daysToCollect").GetProperty("days").GetInt32());
        Assert.Equal(4, money.GetProperty("aging").GetArrayLength());
        Assert.Equal(0, money.GetProperty("whoOwesMost").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task The_target_the_company_set_changes_met()
    {
        var world = SeedCompany();
        SeedInvoice(world, 1_000_000, ageDays: 5, payments: [(3, 600_000)]);
        var admin = await SignedInAs(world.CompanyAdmin.Email);
        var gm = await SignedInAs(world.General.Email);

        var before = (await Json(await gm.GetAsync($"/api/v1/executive/money?{Range(10)}"))).GetProperty("kpis").GetProperty("collected");
        await admin.PutAsJsonAsync("/api/v1/executive/targets", new { collectionRatePercent = 50, daysToCollect = 30, requestToDeliveryDays = 28, reviewDaysPerStep = 2 });
        var after = (await Json(await gm.GetAsync($"/api/v1/executive/money?{Range(10)}"))).GetProperty("kpis").GetProperty("collected");

        Assert.False(before.GetProperty("met").GetBoolean());
        Assert.Equal(50, after.GetProperty("targetPercent").GetInt32());
        Assert.True(after.GetProperty("met").GetBoolean());
    }
}
