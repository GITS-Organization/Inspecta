using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Application.Executive;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The period every executive answer is worked out over, and the company targets.</summary>
public sealed class ExecutivePeriodTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    [Fact]
    public void Without_dates_the_period_is_the_first_of_the_month_to_today_and_previous_is_the_same_number_of_days_before()
    {
        var period = ExecutivePeriod.Resolve(null, null, Today);

        Assert.Equal(new DateOnly(2026, 11, 1), period.From);
        Assert.Equal(Today, period.To);
        Assert.Equal(12, period.Days);
        Assert.Equal(new DateOnly(2026, 10, 31), period.PreviousTo);
        Assert.Equal(new DateOnly(2026, 10, 20), period.PreviousFrom);
    }

    [Fact]
    public void A_future_to_is_cut_to_today()
    {
        var period = ExecutivePeriod.Resolve(new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31), Today);

        Assert.Equal(Today, period.To);
    }

    [Fact]
    public void Only_to_starts_the_period_at_the_first_of_that_month()
    {
        var period = ExecutivePeriod.Resolve(null, new DateOnly(2026, 9, 15), Today);

        Assert.Equal(new DateOnly(2026, 9, 1), period.From);
        Assert.Equal(new DateOnly(2026, 9, 15), period.To);
    }

    [Fact]
    public void Contains_includes_both_days()
    {
        var period = ExecutivePeriod.Resolve(new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 5), Today);

        Assert.True(period.Contains(new DateOnly(2026, 11, 3)));
        Assert.True(period.Contains(new DateOnly(2026, 11, 5)));
        Assert.False(period.Contains(new DateOnly(2026, 11, 6)));
    }

    [Fact]
    public void A_wrong_range_is_refused()
    {
        Assert.Throws<ArgumentException>(() => ExecutivePeriod.Resolve(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1), Today));
        Assert.Throws<ArgumentException>(() => ExecutivePeriod.Resolve(new DateOnly(2024, 1, 1), new DateOnly(2026, 11, 1), Today));
        Assert.Throws<ArgumentException>(() => ExecutivePeriod.Resolve(new DateOnly(2026, 12, 1), null, Today));
    }
}

public sealed class ExecutiveTargetsTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Defaults_are_85_30_28_and_2()
    {
        Assert.Equal((85, 30, 28, 2), (ExecutiveTargets.DefaultCollectionRatePercent, ExecutiveTargets.DefaultDaysToCollect,
            ExecutiveTargets.DefaultRequestToDeliveryDays, ExecutiveTargets.DefaultReviewDaysPerStep));
    }

    [Fact]
    public void A_target_set_keeps_its_values_and_update_replaces_all_four()
    {
        var targets = ExecutiveTargets.Create(Company, 90, 25, 20, 3, Actor);
        targets.Update(80, 40, 30, 1, Actor);

        Assert.Equal((80, 40, 30, 1), (targets.CollectionRatePercent, targets.DaysToCollect, targets.RequestToDeliveryDays, targets.ReviewDaysPerStep));
        Assert.Equal(Company, targets.ManagerId);
    }

    [Theory]
    [InlineData(0, 30, 28, 2)]
    [InlineData(101, 30, 28, 2)]
    [InlineData(85, 0, 28, 2)]
    [InlineData(85, 366, 28, 2)]
    [InlineData(85, 30, 0, 2)]
    [InlineData(85, 30, 28, 366)]
    public void Out_of_range_values_are_refused(int rate, int collect, int delivery, int review) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ExecutiveTargets.Create(Company, rate, collect, delivery, review, Actor));
}

/// <summary>Who may read and write the targets, over HTTP.</summary>
public sealed class ExecutiveTargetsApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 240;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public ExecutiveTargetsApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    internal async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.55.77." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"login of {email} answered {(int)response.StatusCode}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private (Company Company, AppUser CompanyAdmin, AppUser General, AppUser Coordinator, AppUser Finance, AppUser Owner) Seed()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Targets {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"tg-tm-{number}@example.com", UserRole.TechnicalManager, company.Id);
        api.SeedAs(db => db.Companies.Single(item => item.Id == company.Id).SetDefaultReviewer(manager.Id));
        return (company,
            api.AddUser($"tg-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id),
            api.AddUser($"tg-gm-{number}@example.com", UserRole.GeneralManager, company.Id),
            api.AddUser($"tg-coordinator-{number}@example.com", UserRole.Coordinator, company.Id),
            api.AddUser($"tg-fm-{number}@example.com", UserRole.FinancialManager, company.Id),
            api.AddUser($"tg-owner-{number}@example.com", UserRole.Admin));
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task A_new_company_answers_the_defaults_and_the_company_admin_changes_them()
    {
        var setup = Seed();
        var admin = await SignedInAs(setup.CompanyAdmin.Email);

        var defaults = await Json(await admin.GetAsync("/api/v1/executive/targets"));
        var saved = await admin.PutAsJsonAsync("/api/v1/executive/targets", new { collectionRatePercent = 90, daysToCollect = 25, requestToDeliveryDays = 20, reviewDaysPerStep = 3 });
        var read = await Json(await admin.GetAsync("/api/v1/executive/targets"));

        Assert.Equal((85, 30, 28, 2), (defaults.GetProperty("collectionRatePercent").GetInt32(), defaults.GetProperty("daysToCollect").GetInt32(),
            defaults.GetProperty("requestToDeliveryDays").GetInt32(), defaults.GetProperty("reviewDaysPerStep").GetInt32()));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal((90, 25, 20, 3), (read.GetProperty("collectionRatePercent").GetInt32(), read.GetProperty("daysToCollect").GetInt32(),
            read.GetProperty("requestToDeliveryDays").GetInt32(), read.GetProperty("reviewDaysPerStep").GetInt32()));
        var again = await admin.PutAsJsonAsync("/api/v1/executive/targets", new { collectionRatePercent = 70, daysToCollect = 40, requestToDeliveryDays = 35, reviewDaysPerStep = 4 });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(70, (await Json(await admin.GetAsync("/api/v1/executive/targets"))).GetProperty("collectionRatePercent").GetInt32());
    }

    [Fact]
    public async Task Out_of_range_targets_are_a_400()
    {
        var setup = Seed();
        var admin = await SignedInAs(setup.CompanyAdmin.Email);

        var response = await admin.PutAsJsonAsync("/api/v1/executive/targets", new { collectionRatePercent = 150, daysToCollect = 25, requestToDeliveryDays = 20, reviewDaysPerStep = 3 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_company_admin_and_the_platform_owner_manage_targets()
    {
        var setup = Seed();
        foreach (var email in new[] { setup.General.Email, setup.Coordinator.Email, setup.Finance.Email })
        {
            var user = await SignedInAs(email);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/executive/targets")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.PutAsJsonAsync("/api/v1/executive/targets", new { collectionRatePercent = 90, daysToCollect = 25, requestToDeliveryDays = 20, reviewDaysPerStep = 3 })).StatusCode);
        }
    }

    [Fact]
    public async Task The_platform_owner_reads_the_defaults_without_a_company_and_must_name_one_to_write()
    {
        var setup = Seed();
        var owner = await SignedInAs(setup.Owner.Email);
        var body = new { collectionRatePercent = 60, daysToCollect = 45, requestToDeliveryDays = 40, reviewDaysPerStep = 5 };

        var read = await Json(await owner.GetAsync("/api/v1/executive/targets"));
        var withoutCompany = await owner.PutAsJsonAsync("/api/v1/executive/targets", body);
        var withCompany = await owner.PutAsJsonAsync($"/api/v1/executive/targets?managerId={setup.Company.Id}", body);

        Assert.Equal(85, read.GetProperty("collectionRatePercent").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, withoutCompany.StatusCode);
        Assert.Equal("manager_required", (await Json(withoutCompany)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, withCompany.StatusCode);
        var admin = await SignedInAs(setup.CompanyAdmin.Email);
        Assert.Equal(60, (await Json(await admin.GetAsync("/api/v1/executive/targets"))).GetProperty("collectionRatePercent").GetInt32());
    }

    [Fact]
    public async Task One_company_cannot_change_or_read_another_companys_targets()
    {
        var first = Seed();
        var second = Seed();
        var firstAdmin = await SignedInAs(first.CompanyAdmin.Email);
        var secondAdmin = await SignedInAs(second.CompanyAdmin.Email);
        await firstAdmin.PutAsJsonAsync("/api/v1/executive/targets", new { collectionRatePercent = 50, daysToCollect = 10, requestToDeliveryDays = 10, reviewDaysPerStep = 1 });

        var other = await Json(await secondAdmin.GetAsync("/api/v1/executive/targets"));
        var asked = await secondAdmin.GetAsync($"/api/v1/executive/targets?managerId={first.Company.Id}");

        Assert.Equal(85, other.GetProperty("collectionRatePercent").GetInt32());
        Assert.Equal(85, (await Json(asked)).GetProperty("collectionRatePercent").GetInt32());
    }
}
