using System.Net;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The Work screen over HTTP.</summary>
public sealed class ExecutiveWorkApiTests(ApiHttpTests.Api api) : ExecutiveApiTestBase(api), IClassFixture<ApiHttpTests.Api>
{
    private InspectionRequest SeedJob(World world, JobStatus upTo, AppUser? reviewer = null, bool inspector = false, string client = "Steel Co.", RequestTriage? maybe = null)
    {
        InspectionRequest request;
        if (maybe is not null)
        {
            request = InspectionRequest.CreateFromEmail(world.Company.Id, world.Coordinator.Id, new IncomingEmail(
                "9", 1, "text", null, "x@y.com", "X", "Maybe", [], [], [], [], [], $"<{Guid.NewGuid():N}@y.com>", null, null, DateTimeOffset.UtcNow), triage: RequestTriage.Maybe);
        }
        else
        {
            request = InspectionRequest.Create(client, "Site", null, [RequestItem.Create("Crane", 2)], world.Coordinator.Id, managerId: world.Company.Id);
            if (reviewer is not null) request.UseReviewer(reviewer.Id);
            var path = new[] { JobStatus.QuoteDraft, JobStatus.QuoteSent, JobStatus.Unassigned, JobStatus.Assigned, JobStatus.TaskAccepted, JobStatus.InProgress, JobStatus.CertificateSubmitted, JobStatus.CertificateApproved, JobStatus.SentToClient };
            foreach (var status in path)
            {
                if (status == JobStatus.Assigned) request.AssignInspector(world.Inspector.Id, DateTimeOffset.UtcNow.AddDays(1), world.Coordinator.Id);
                else request.TransitionTo(status, status is JobStatus.TaskAccepted or JobStatus.InProgress or JobStatus.CertificateSubmitted ? world.Inspector.Id : world.Coordinator.Id);
                if (status == upTo) break;
            }
        }

        Api.SeedAs(db => db.Requests.Add(request));
        return request;
    }

    private void SeedApprovedCertificate(InspectionRequest request, AppUser reviewer, CertificateResult result, Guid inspectorId)
    {
        var certificate = InspectionCertificate.Create(request.Id, inspectorId);
        Api.SeedAs(db =>
        {
            db.Certificates.Add(certificate);
            db.SaveChanges();
            db.Entry(certificate).Property(nameof(InspectionCertificate.ReviewedAt)).CurrentValue = (DateTimeOffset?)DateTimeOffset.UtcNow;
            db.Entry(certificate).Property(nameof(InspectionCertificate.FinalResult)).CurrentValue = (CertificateResult?)result;
            db.Entry(certificate).Property(nameof(InspectionCertificate.ReviewerId)).CurrentValue = (Guid?)reviewer.Id;
            db.SaveChanges();
        });
    }

    [Fact]
    public async Task Jobs_in_flight_are_counted_by_stage_group_and_maybe_emails_and_finished_jobs_are_not()
    {
        var world = SeedCompany();
        SeedJob(world, JobStatus.QuoteSent, world.Manager);
        SeedJob(world, JobStatus.InProgress, world.Manager);
        SeedJob(world, JobStatus.CertificateSubmitted, world.SecondManager);
        SeedJob(world, JobStatus.SentToClient, world.Manager);
        SeedJob(world, JobStatus.RequestReceived, maybe: RequestTriage.Maybe);
        var gm = await SignedInAs(world.General.Email);

        var work = await Json(await gm.GetAsync("/api/v1/executive/work"));

        Assert.Equal(3, work.GetProperty("jobs").GetProperty("inFlight").GetInt32());
        Assert.Equal(3, work.GetProperty("total").GetInt32());
        var open = work.GetProperty("open").EnumerateArray().ToArray();
        Assert.Equal(["certificate_submitted", "in_progress", "quote_sent"], open.Select(item => item.GetProperty("stage").GetString()).Order());
        Assert.All(open, item => Assert.StartsWith("REQ-", item.GetProperty("requestNumber").GetString()));
        Assert.Contains(open, item => item.GetProperty("stageGroup").GetString() == "review" && item.GetProperty("reviewerName").GetString() == world.SecondManager.FullName);
        Assert.All(open, item => Assert.Equal(2, item.GetProperty("machines").GetInt32()));
    }

    [Fact]
    public async Task The_reviewer_filter_narrows_jobs_but_not_the_reviewers_or_inspectors_tables()
    {
        var world = SeedCompany();
        SeedJob(world, JobStatus.QuoteSent, world.Manager);
        SeedJob(world, JobStatus.InProgress, world.SecondManager);
        var gm = await SignedInAs(world.General.Email);

        var all = await Json(await gm.GetAsync("/api/v1/executive/work"));
        var mine = await Json(await gm.GetAsync($"/api/v1/executive/work?reviewerId={world.Manager.Id}"));

        Assert.Equal(2, all.GetProperty("jobs").GetProperty("inFlight").GetInt32());
        Assert.Equal(1, mine.GetProperty("jobs").GetProperty("inFlight").GetInt32());
        Assert.Equal(2, mine.GetProperty("reviewers").GetArrayLength());
        Assert.Equal(all.GetProperty("inspectors").GetArrayLength(), mine.GetProperty("inspectors").GetArrayLength());
    }

    [Fact]
    public async Task Reviewers_show_what_waits_for_them_and_certificates_by_month_count_the_approved_ones()
    {
        var world = SeedCompany();
        var waiting = SeedJob(world, JobStatus.CertificateSubmitted, world.Manager);
        var done = SeedJob(world, JobStatus.SentToClient, world.Manager);
        SeedApprovedCertificate(done, world.Manager, CertificateResult.SafeToOperate, world.Inspector.Id);
        SeedApprovedCertificate(done, world.Manager, CertificateResult.NotSafe, world.Inspector.Id);
        _ = waiting;
        var gm = await SignedInAs(world.General.Email);

        var work = await Json(await gm.GetAsync("/api/v1/executive/work"));

        var reviewer = work.GetProperty("reviewers").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == world.Manager.Id);
        Assert.Equal(1, reviewer.GetProperty("waiting").GetInt32());
        Assert.Equal(2, reviewer.GetProperty("certificates").GetInt32());
        var months = work.GetProperty("certificatesByMonth").EnumerateArray().ToArray();
        Assert.Equal(12, months.Length);
        Assert.Equal((1, 1), (months[11].GetProperty("safe").GetInt32(), months[11].GetProperty("notSafe").GetInt32()));
        var inspector = work.GetProperty("inspectors").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == world.Inspector.Id);
        Assert.Equal(2, inspector.GetProperty("certificates").GetInt32());
    }

    [Fact]
    public async Task An_inspector_on_leave_shows_the_day_the_leave_ends()
    {
        var world = SeedCompany();
        var until = DateTimeOffset.UtcNow.AddDays(8);
        Api.SeedAs(db =>
        {
            var user = db.Users.Single(item => item.Id == world.Inspector.Id);
            db.Entry(user).Property(nameof(AppUser.IsOnLeave)).CurrentValue = true;
            db.Entry(user).Property(nameof(AppUser.LeaveUntil)).CurrentValue = (DateTimeOffset?)until;
        });
        var gm = await SignedInAs(world.General.Email);

        var work = await Json(await gm.GetAsync("/api/v1/executive/work"));

        var inspector = work.GetProperty("inspectors").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == world.Inspector.Id);
        Assert.NotEqual(JsonValueKind.Null, inspector.GetProperty("onLeaveUntil").ValueKind);
    }

    [Fact]
    public async Task Open_jobs_are_paged_one_company_never_sees_another_and_only_the_gm_and_owner_may_ask()
    {
        var first = SeedCompany();
        var second = SeedCompany();
        foreach (var _ in Enumerable.Range(0, 3)) SeedJob(first, JobStatus.InProgress, first.Manager);
        SeedJob(second, JobStatus.InProgress, second.Manager);
        var gm = await SignedInAs(first.General.Email);

        var paged = await Json(await gm.GetAsync("/api/v1/executive/work?pageSize=2&page=2"));
        var empty = await Json(await (await SignedInAs(SeedCompany().General.Email)).GetAsync("/api/v1/executive/work"));

        Assert.Equal(3, paged.GetProperty("total").GetInt32());
        Assert.Equal(1, paged.GetProperty("open").GetArrayLength());
        Assert.Equal(0, empty.GetProperty("jobs").GetProperty("inFlight").GetInt32());
        Assert.Equal(0, empty.GetProperty("open").GetArrayLength());
        Assert.Equal(6, empty.GetProperty("stepTimes").GetArrayLength());
        foreach (var email in new[] { first.Coordinator.Email, first.Finance.Email, first.CompanyAdmin.Email })
            Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(email)).GetAsync("/api/v1/executive/work")).StatusCode);
    }

    [Fact]
    public async Task Quotations_are_counted_with_the_countered_ones_and_the_period_is_checked()
    {
        var world = SeedCompany();
        var countered = SeedJob(world, JobStatus.QuoteSent, world.Manager);
        Api.SeedAs(db =>
        {
            var request = db.Requests.Single(item => item.Id == countered.Id);
            request.TransitionTo(JobStatus.ClientCountered, world.Coordinator.Id);
        });
        var gm = await SignedInAs(world.General.Email);

        var work = await Json(await gm.GetAsync("/api/v1/executive/work"));
        var wrong = await gm.GetAsync($"/api/v1/executive/work?from={Today:yyyy-MM-dd}&to={Today.AddDays(-3):yyyy-MM-dd}");

        Assert.Equal(1, work.GetProperty("quotations").GetProperty("countered").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    }
}
