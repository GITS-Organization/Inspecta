using System.Net;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>The financial manager's Overview over HTTP.</summary>
public sealed class FinanceOverviewApiTests(ApiHttpTests.Api api) : ExecutiveApiTestBase(api), IClassFixture<ApiHttpTests.Api>
{
    private void HandOver(Invoice invoice, World world) =>
        Api.SeedAs(db => db.Invoices.Include(item => item.Payments).Include(item => item.Events).Single(item => item.Id == invoice.Id).HandOver(world.Coordinator.Id));

    [Fact]
    public async Task The_financial_manager_gets_the_money_figures_what_is_ready_to_close_and_the_latest_payments()
    {
        var world = SeedCompany();
        var (ready, _) = SeedInvoice(world, 500_000, ageDays: 20, payments: [(3, 500_000)], clientName: "Nile");
        var (old, _) = SeedInvoice(world, 300_000, ageDays: 70, clientName: "Slow Co");
        var (waiting, _) = SeedInvoice(world, 110_000, ageDays: 2, clientName: "New Co");
        HandOver(ready, world);
        HandOver(old, world);
        var fm = await SignedInAs(world.Finance.Email);

        var overview = await Json(await fm.GetAsync("/api/v1/finance/overview"));

        Assert.Equal(1, overview.GetProperty("readyToClose").GetProperty("count").GetInt32());
        Assert.Equal(500_000, overview.GetProperty("readyToClose").GetProperty("totalPiastres").GetInt64());
        Assert.Equal("Nile", overview.GetProperty("readyToClose").GetProperty("items")[0].GetProperty("clientName").GetString());
        Assert.Equal(1, overview.GetProperty("notHandedOver").GetProperty("count").GetInt32());
        Assert.Equal(110_000, overview.GetProperty("notHandedOver").GetProperty("balancePiastres").GetInt64());
        Assert.Equal("Slow Co", overview.GetProperty("oldUnpaid")[0].GetProperty("clientName").GetString());
        Assert.Equal(70, overview.GetProperty("oldUnpaid")[0].GetProperty("ageDays").GetInt32());
        var latest = overview.GetProperty("latestPayments");
        Assert.Equal(1, latest.GetArrayLength());
        Assert.Equal(500_000, latest[0].GetProperty("amountPiastres").GetInt64());
        Assert.Equal(world.Coordinator.FullName, latest[0].GetProperty("recordedByName").GetString());
        var money = overview.GetProperty("money");
        Assert.Equal(410_000, money.GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(12, money.GetProperty("monthly").GetArrayLength());
        Assert.Equal(4, money.GetProperty("aging").GetArrayLength());
        _ = waiting;
    }

    [Fact]
    public async Task Only_the_financial_manager_and_the_platform_owner_may_ask_and_a_company_never_sees_another()
    {
        var first = SeedCompany();
        var second = SeedCompany();
        SeedInvoice(second, 900_000, ageDays: 80, clientName: "Their Client");
        foreach (var email in new[] { first.General.Email, first.Coordinator.Email, first.CompanyAdmin.Email, first.Manager.Email })
            Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(email)).GetAsync("/api/v1/finance/overview")).StatusCode);
        var fm = await SignedInAs(first.Finance.Email);
        var owner = await SignedInAs(first.Owner.Email);

        var own = await Json(await fm.GetAsync("/api/v1/finance/overview"));
        var asked = await Json(await fm.GetAsync($"/api/v1/finance/overview?managerId={second.Company.Id}"));
        var narrowed = await Json(await owner.GetAsync($"/api/v1/finance/overview?managerId={second.Company.Id}"));

        Assert.Equal(0, own.GetProperty("money").GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(0, asked.GetProperty("money").GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(900_000, narrowed.GetProperty("money").GetProperty("kpis").GetProperty("outstanding").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(0, own.GetProperty("latestPayments").GetArrayLength());
    }

    [Fact]
    public async Task A_wrong_period_is_a_400_and_the_period_changes_the_money_figures()
    {
        var world = SeedCompany();
        SeedInvoice(world, 1_000_000, ageDays: 5, payments: [(3, 400_000)]);
        var fm = await SignedInAs(world.Finance.Email);

        var wrong = await fm.GetAsync($"/api/v1/finance/overview?from={Today:yyyy-MM-dd}&to={Today.AddDays(-4):yyyy-MM-dd}");
        var inRange = await Json(await fm.GetAsync($"/api/v1/finance/overview?{Range(10)}"));
        var before = await Json(await fm.GetAsync($"/api/v1/finance/overview?{Range(40, 20)}"));

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(400_000, inRange.GetProperty("money").GetProperty("kpis").GetProperty("collected").GetProperty("collectedPiastres").GetInt64());
        Assert.Equal(0, before.GetProperty("money").GetProperty("kpis").GetProperty("collected").GetProperty("collectedPiastres").GetInt64());
    }
}
