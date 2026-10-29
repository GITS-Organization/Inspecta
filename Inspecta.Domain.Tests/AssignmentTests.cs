using Inspecta.Application.Exceptions;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class AssignmentTests
{
    private static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly Guid CoordinatorId = Guid.NewGuid();

    [Fact]
    public async Task TheOwningCoordinatorCanGiveAnAcceptedJobToAnInspectorOfTheTeam()
    {
        var fixture = new Fixture();

        var assigned = await fixture.Service.AssignAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", fixture.Inspector.Id, DateTimeOffset.UtcNow.AddDays(1), "Bring a ladder");

        Assert.Equal(JobStatus.Assigned, assigned.Status);
        Assert.Equal(fixture.Inspector.Id, assigned.InspectorId);
    }

    [Fact]
    public async Task ManagersAndAdminsCanAssignRequestsTheyCanSee()
    {
        var byManager = new Fixture();
        await byManager.Service.AssignAsync(
            byManager.Request.Id, ManagerId, "technical_manager", byManager.Inspector.Id, DateTimeOffset.UtcNow.AddDays(1), null);
        Assert.Equal(JobStatus.Assigned, byManager.Request.Status);

        var byAdmin = new Fixture();
        await byAdmin.Service.AssignAsync(
            byAdmin.Request.Id, Guid.NewGuid(), "admin", byAdmin.Inspector.Id, DateTimeOffset.UtcNow.AddDays(1), null);
        Assert.Equal(JobStatus.Assigned, byAdmin.Request.Status);
    }

    [Fact]
    public async Task AnotherCoordinatorCannotAssignSomeoneElsesEmailRequest()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.AssignAsync(
            fixture.Request.Id, Guid.NewGuid(), "coordinator", fixture.Inspector.Id, DateTimeOffset.UtcNow.AddDays(1), null));
        Assert.Equal(JobStatus.Unassigned, fixture.Request.Status);
    }

    [Fact]
    public async Task InspectorsCannotAssignJobs()
    {
        var fixture = new Fixture();

        var denied = await Assert.ThrowsAsync<AuthException>(() => fixture.Service.AssignAsync(
            fixture.Request.Id, fixture.Inspector.Id, "inspector", fixture.Inspector.Id, DateTimeOffset.UtcNow.AddDays(1), null));

        Assert.Equal(403, denied.StatusCode);
    }

    [Fact]
    public async Task OnlyAnAcceptedJobCanBeAssigned()
    {
        var fixture = new Fixture(accepted: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.AssignAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", fixture.Inspector.Id, DateTimeOffset.UtcNow.AddDays(1), null));
    }

    [Fact]
    public async Task ACoordinatorSeesTheirTeamsActiveInspectorsWithTheirWorkload()
    {
        var fixture = new Fixture();
        var busy = AppUser.Create("busy@example.com", "Busy", UserRole.Inspector, managerId: ManagerId);
        var retired = AppUser.Create("retired@example.com", "Retired", UserRole.Inspector, managerId: ManagerId);
        retired.Deactivate();
        var otherTeam = AppUser.Create("other@example.com", "Other", UserRole.Inspector, managerId: Guid.NewGuid());
        var withUsers = new Fixture(extraUsers: [busy, retired, otherTeam]);
        await withUsers.Service.AssignAsync(
            withUsers.Request.Id, CoordinatorId, "coordinator", busy.Id, DateTimeOffset.UtcNow.AddDays(1), null);

        var inspectors = await withUsers.Service.ListInspectorsAsync(CoordinatorId, "coordinator");

        Assert.Equal(["Busy", withUsers.Inspector.FullName], inspectors.Select(inspector => inspector.FullName));
        Assert.Equal(1, inspectors.Single(inspector => inspector.Id == busy.Id).ActiveJobs);
        Assert.Equal(0, inspectors.Single(inspector => inspector.Id == withUsers.Inspector.Id).ActiveJobs);
        Assert.DoesNotContain(inspectors, inspector => inspector.Id == fixture.Inspector.Id);
    }

    [Fact]
    public async Task AManagerSeesTheirOwnTeamsInspectors()
    {
        var fixture = new Fixture();

        var inspectors = await fixture.Service.ListInspectorsAsync(ManagerId, "technical_manager");

        Assert.Equal(fixture.Inspector.Id, Assert.Single(inspectors).Id);
    }

    private sealed class Fixture
    {
        public Fixture(bool accepted = true, params AppUser[] extraUsers)
        {
            Request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, new IncomingEmail(
                "42", 1, "Please inspect.", null, "buyer@alnoor.com", "Al Noor", "Inspection request",
                [], [], [], [], [], "<id@alnoor.com>", null, null, DateTimeOffset.UtcNow.AddMinutes(-5)));
            Request.UpdateDetails("Al Noor", "Cairo", Guid.NewGuid(), null, null);
            Request.AddItem(RequestItem.Create("Crane", 1));
            if (accepted)
            {
                Request.TransitionTo(JobStatus.QuoteDraft, CoordinatorId);
                Request.TransitionTo(JobStatus.QuoteSent, CoordinatorId);
                Request.TransitionTo(JobStatus.Unassigned, CoordinatorId);
            }

            Inspector = AppUser.Create("inspector@example.com", "Karim", UserRole.Inspector, managerId: ManagerId);
            var coordinator = AppUser.Create("coordinator@example.com", "Coordinator", UserRole.Coordinator, managerId: ManagerId);
            typeof(AppUser).GetProperty(nameof(AppUser.Id))!.SetValue(coordinator, CoordinatorId);
            Service = new InspectionRequestService(
                new QuotationTests.FakeRequests(Request),
                new IncomingEmailRequestTests.FakeUserRepository([Inspector, coordinator, .. extraUsers]));
        }

        public InspectionRequest Request { get; }
        public AppUser Inspector { get; }
        public InspectionRequestService Service { get; }
    }
}
