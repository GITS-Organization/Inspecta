using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class RequestSaveTests
{
    private static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly Guid CoordinatorId = Guid.NewGuid();

    [Fact]
    public async Task OmittingItemsSavesTheDetailsAndLeavesTheItemsAlone()
    {
        var fixture = new Fixture();

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest { ClientName = "Al Noor", Location = "Industrial City" });

        Assert.Equal("Al Noor", saved.ClientName);
        Assert.Equal(2, saved.Items.Count);
        Assert.Empty(fixture.Repository.LastChanges!.Added);
        Assert.Empty(fixture.Repository.LastChanges.Removed);
    }

    [Fact]
    public async Task TheItemListReplacesTheCurrentOneInASingleSave()
    {
        var fixture = new Fixture();

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientName = "Al Noor",
            Location = "Industrial City",
            AccessNotes = "Contact reception",
            Items =
            [
                new SaveRequestItem { Id = fixture.Crane.Id, Name = "Mobile crane 100 t", Quantity = 2, Type = "Crane", Location = "Yard" },
                new SaveRequestItem { Name = "Safety valve DN40", Quantity = 6 }
            ]
        });

        Assert.Equal(1, fixture.Repository.Saves);
        Assert.Equal(2, saved.Items.Count);
        var crane = saved.Items.Single(item => item.Id == fixture.Crane.Id);
        Assert.Equal("Mobile crane 100 t", crane.Name);
        Assert.Equal(2, crane.Quantity);
        Assert.Equal("Yard", crane.Location);
        var added = Assert.Single(fixture.Repository.LastChanges!.Added);
        Assert.Equal("Safety valve DN40", added.Name);
        Assert.Contains(saved.Items, item => item.Id == added.Id);
        var removed = Assert.Single(fixture.Repository.LastChanges.Removed);
        Assert.Equal(fixture.Receiver.Id, removed.Id);
        Assert.DoesNotContain(saved.Items, item => item.Id == fixture.Receiver.Id);
        Assert.Equal("Contact reception", saved.AccessNotes);
        Assert.Empty(saved.MissingFields);
    }

    [Fact]
    public async Task AnEmptyListRemovesEveryItem()
    {
        var fixture = new Fixture();

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest { ClientName = "Al Noor", Location = "Cairo", Items = [] });

        Assert.Empty(saved.Items);
        Assert.Equal(["items"], saved.MissingFields);
        Assert.Equal(2, fixture.Repository.LastChanges!.Removed.Count);
    }

    [Fact]
    public async Task AnInvalidItemRejectsTheWholeSaveAndChangesNothing()
    {
        var fixture = new Fixture();
        var before = fixture.Request.UpdatedAt;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientName = "Changed",
            Location = "Changed",
            Items =
            [
                new SaveRequestItem { Id = fixture.Crane.Id, Name = "Renamed", Quantity = 3 },
                new SaveRequestItem { Name = "Broken", Quantity = 0 }
            ]
        }));

        Assert.Equal(0, fixture.Repository.Saves);
        Assert.Equal("Al Noor", fixture.Request.ClientName);
        Assert.Equal("Cairo", fixture.Request.Location);
        Assert.Equal(before, fixture.Request.UpdatedAt);
        Assert.Equal(2, fixture.Request.Items.Count);
        Assert.Equal("Mobile crane", fixture.Crane.Name);
    }

    [Fact]
    public async Task UnknownAndRepeatedItemIdsAreRejected()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            Items = [new SaveRequestItem { Id = Guid.NewGuid(), Name = "Not mine" }]
        }));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            Items =
            [
                new SaveRequestItem { Id = fixture.Crane.Id, Name = "One" },
                new SaveRequestItem { Id = fixture.Crane.Id, Name = "Two" }
            ]
        }));

        Assert.Equal(0, fixture.Repository.Saves);
        Assert.Equal(2, fixture.Request.Items.Count);
    }

    [Fact]
    public async Task AStaleScreenGetsAConflictInsteadOfOverwritingNewerChanges()
    {
        var fixture = new Fixture();
        var stale = fixture.Request.UpdatedAt.AddMinutes(-5);

        var conflict = await Assert.ThrowsAsync<AuthException>(() => fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientName = "Stale",
            Items = [],
            ExpectedUpdatedAt = stale
        }));

        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(0, fixture.Repository.Saves);
        Assert.Equal(2, fixture.Request.Items.Count);
    }

    [Fact]
    public async Task ASaveWithTheCurrentUpdatedAtIsAccepted()
    {
        var fixture = new Fixture();

        var saved = await fixture.SaveAsync(new UpdateRequestDetailsRequest
        {
            ClientName = "Al Noor",
            Location = "Cairo",
            ExpectedUpdatedAt = fixture.Request.UpdatedAt.AddTicks(3)
        });

        Assert.Equal("Al Noor", saved.ClientName);
        Assert.Equal(1, fixture.Repository.Saves);
    }

    [Fact]
    public async Task ItemsCannotBeReplacedOnceTheQuoteIsSentAndOnlyTheOwnerCanSave()
    {
        var fixture = new Fixture();
        fixture.Request.UpdateDetails("Al Noor", "Cairo", Guid.NewGuid(), null, null);
        fixture.Request.TransitionTo(JobStatus.QuoteDraft, CoordinatorId);
        fixture.Request.TransitionTo(JobStatus.QuoteSent, CoordinatorId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.SaveAsync(new UpdateRequestDetailsRequest { Items = [] }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.UpdateDetailsAsync(
            fixture.Request.Id, Guid.NewGuid(), "coordinator", new UpdateRequestDetailsRequest { Items = [] }));
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, new IncomingEmail(
                "42", 1, "Please quote.", null, "buyer@alnoor.com", "Al Noor", "Quote request",
                [], [], [], [], [], "<id@alnoor.com>", null, null, DateTimeOffset.UtcNow.AddMinutes(-5)));
            Request.UpdateDetails("Al Noor", "Cairo", null, null, null);
            Crane = Request.AddItem(RequestItem.Create("Mobile crane", 1, type: "Crane"));
            Receiver = Request.AddItem(RequestItem.Create("Air receiver", 4, type: "Air receiver"));
            Repository = new Repo(Request);
            Service = new InspectionRequestService(Repository, null!);
        }

        public InspectionRequest Request { get; }
        public RequestItem Crane { get; }
        public RequestItem Receiver { get; }
        public Repo Repository { get; }
        public InspectionRequestService Service { get; }

        public Task<RequestResponse> SaveAsync(UpdateRequestDetailsRequest details) =>
            Service.UpdateDetailsAsync(Request.Id, CoordinatorId, "coordinator", details);
    }

    private sealed class Repo(InspectionRequest request) : IRequestRepository
    {
        public int Saves { get; private set; }
        public RequestItemChanges? LastChanges { get; private set; }

        public Task SaveRevisionAsync(InspectionRequest value, RequestItemChanges changes, CancellationToken cancellationToken = default)
        {
            Saves++;
            LastChanges = changes;
            return Task.CompletedTask;
        }

        public Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default) =>
            Task.FromResult<InspectionRequest?>(request.Id == requestId ? request : null);

        public Task AddAsync(InspectionRequest value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(InspectionRequest value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(InspectionRequest value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddItemAsync(InspectionRequest value, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveItemAsync(InspectionRequest value, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RequestEmail?> GetEmailAsync(Guid requestId, CancellationToken cancellationToken = default) => Task.FromResult<RequestEmail?>(null);
        public Task<IReadOnlyList<InspectionRequest>> ListAsync(RequestListFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InspectionRequest>>([request]);
        public Task<IReadOnlyList<RequestCount>> CountAsync(RequestListFilter scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestCount>>([]);
        public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
    }
}
