using System.Security.Claims;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

public sealed class EmailRequestTests
{
    private static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly Guid CoordinatorId = Guid.NewGuid();

    [Fact]
    public void EmailCreatesAnIncompleteRequestOwnedByTheReceivingCoordinator()
    {
        var request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email(uid: 7));

        Assert.Equal(RequestSource.Email, request.Source);
        Assert.Equal(JobStatus.RequestReceived, request.Status);
        Assert.Equal(ManagerId, request.ManagerId);
        Assert.Equal(CoordinatorId, request.CoordinatorId);
        Assert.Equal("client@example.test", request.EmailFrom);
        Assert.Equal("Al Noor", request.EmailFromName);
        Assert.Empty(request.Items);
        Assert.Equal(["clientName", "location", "items"], request.GetMissingFields());
        Assert.Equal(request.Id, request.Email!.RequestId);
        Assert.Equal(7, request.Email.MailboxUid);
        Assert.Single(request.History);
    }

    [Fact]
    public void EmailKeepsRecipientsAttachmentsAndThreadHeaders()
    {
        var request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email() with
        {
            Cc = [new EmailAddressInfo("cc@example.test", "Copy")],
            Attachments = [new EmailAttachmentInfo("spec.pdf", "application/pdf", 1234)],
            References = "<root@example.test> <parent@example.test>",
            InReplyTo = "<parent@example.test>",
            MessageId = "<self@example.test>"
        });

        var email = request.Email!;
        Assert.Equal("cc@example.test", Assert.Single(email.GetCcAddresses()).Address);
        Assert.Equal("spec.pdf", Assert.Single(email.GetAttachments()).FileName);
        Assert.Equal("<root@example.test>", request.EmailThreadId);
        Assert.Equal("<self@example.test>", email.MessageId);
    }

    [Fact]
    public void PreviewIsASingleCollapsedLineAndFallsBackToHtmlText()
    {
        var longText = string.Join(" ", Enumerable.Repeat("pressure   vessel\r\ninspection", 40));

        var preview = RequestEmail.BuildPreview(longText, null);
        var htmlPreview = RequestEmail.BuildPreview(null, "<p>Kindly <b>quote</b></p>");

        Assert.NotNull(preview);
        Assert.True(preview!.Length <= RequestEmail.PreviewLength + 1);
        Assert.DoesNotContain('\n', preview);
        Assert.Equal("Kindly quote", htmlPreview);
        Assert.Null(RequestEmail.BuildPreview("   ", null));
    }

    [Fact]
    public void QuoteCannotBeDraftedUntilTheRequestIsComplete()
    {
        var request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());

        Assert.Throws<InvalidOperationException>(() => request.TransitionTo(JobStatus.QuoteDraft, CoordinatorId));

        request.UpdateDetails("Al Noor", "Industrial City", null, null, null);
        Assert.Throws<InvalidOperationException>(() => request.TransitionTo(JobStatus.QuoteDraft, CoordinatorId));

        request.AddItem(RequestItem.Create("Air receiver", 2));
        request.TransitionTo(JobStatus.QuoteDraft, CoordinatorId);

        Assert.Equal(JobStatus.QuoteDraft, request.Status);
    }

    [Fact]
    public void DetailsAndItemsCannotChangeOnceTheQuoteIsSent()
    {
        var request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());
        var item = request.AddItem(RequestItem.Create("Air receiver", 2));
        request.UpdateDetails("Al Noor", "Industrial City", Guid.NewGuid(), null, null);
        request.TransitionTo(JobStatus.QuoteDraft, CoordinatorId);
        request.TransitionTo(JobStatus.QuoteSent, CoordinatorId);

        Assert.False(request.CanEditDetails);
        Assert.Throws<InvalidOperationException>(() => request.UpdateDetails("Other", "Elsewhere", null, null, null));
        Assert.Throws<InvalidOperationException>(() => request.AddItem(RequestItem.Create("Valve", 1)));
        Assert.Throws<InvalidOperationException>(() => request.RemoveItem(item.Id));
    }

    [Fact]
    public void ItemsCanBeUpdatedAndRemovedWhileEditable()
    {
        var request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());
        var item = request.AddItem(RequestItem.Create("Air receiver", 2));

        request.UpdateItem(item.Id, "Air receiver 2,000 L", 3, null, "Pressure vessel", "2,000 L / 10 bar", null, "Compressor room", null);

        Assert.Equal("Air receiver 2,000 L", item.Name);
        Assert.Equal(3, item.Quantity);
        Assert.Equal("Compressor room", item.Location);
        Assert.Throws<KeyNotFoundException>(() => request.RemoveItem(Guid.NewGuid()));
        request.RemoveItem(item.Id);
        Assert.Empty(request.Items);
    }

    [Fact]
    public async Task CoordinatorOnlySeesEmailRequestsFromTheirOwnMailbox()
    {
        var other = InspectionRequest.CreateFromEmail(ManagerId, Guid.NewGuid(), Email());
        var repository = new CapturingRequestRepository(other);
        var service = new InspectionRequestService(repository, null!);

        Assert.Null(await service.GetVisibleAsync(other.Id, CoordinatorId, "coordinator"));
        Assert.Null(await service.GetEmailAsync(other.Id, CoordinatorId, "coordinator"));
        Assert.NotNull(await service.GetVisibleAsync(other.Id, ManagerId, "technical_manager"));
        Assert.NotNull(await service.GetVisibleAsync(other.Id, Guid.NewGuid(), "admin"));
    }

    [Fact]
    public async Task OtherCoordinatorsCannotEditOrTransitionAnEmailRequestByGuessingItsId()
    {
        var other = InspectionRequest.CreateFromEmail(ManagerId, Guid.NewGuid(), Email());
        var service = new InspectionRequestService(new CapturingRequestRepository(other), null!);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateDetailsAsync(
            other.Id, CoordinatorId, "coordinator", new UpdateRequestDetailsRequest { ClientName = "X", Location = "Y" }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AddItemAsync(
            other.Id, CoordinatorId, "coordinator", new CreateRequestItem { Name = "Valve" }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.TransitionAsync(
            other.Id, CoordinatorId, "coordinator", new TransitionRequest { Status = JobStatus.QuoteDraft }));
    }

    [Fact]
    public async Task ManagersCanReadTeamRequestsButOnlyTheOwnerCanEdit()
    {
        var request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());
        var repository = new CapturingRequestRepository(request);
        var service = new InspectionRequestService(repository, null!);
        var details = new UpdateRequestDetailsRequest { ClientName = "Al Noor", Location = "Industrial City" };

        await Assert.ThrowsAsync<AuthException>(() => service.UpdateDetailsAsync(request.Id, ManagerId, "technical_manager", details));

        var updated = await service.UpdateDetailsAsync(request.Id, CoordinatorId, "coordinator", details);

        Assert.Equal("Al Noor", updated.ClientName);
        Assert.Equal(["items"], updated.MissingFields);
    }

    [Fact]
    public async Task LinkingAClientFromAnotherTeamIsRejected()
    {
        var request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());
        var repository = new CapturingRequestRepository(request) { ClientManagerId = Guid.NewGuid() };
        var service = new InspectionRequestService(repository, null!);

        await Assert.ThrowsAsync<AuthException>(() => service.UpdateDetailsAsync(
            request.Id,
            CoordinatorId,
            "coordinator",
            new UpdateRequestDetailsRequest { ClientId = Guid.NewGuid(), ClientName = "X", Location = "Y" }));
    }

    [Fact]
    public async Task ListFiltersFollowTheActorRole()
    {
        var repository = new CapturingRequestRepository();
        var service = new InspectionRequestService(repository, null!);
        var actorId = Guid.NewGuid();
        var managerFilter = Guid.NewGuid();

        var query = new RequestListQuery(managerFilter);
        var request = new RequestListFilter(Triage: RequestTriage.Request, Take: RequestListQuery.DefaultPageSize);

        await service.ListVisibleAsync(actorId, "coordinator", query);
        Assert.Equal([JobStatus.QuoteDraft, JobStatus.QuoteSent, JobStatus.ClientCountered], repository.LastFilter!.ExcludeStatuses);
        request = request with { ExcludeStatuses = repository.LastFilter.ExcludeStatuses };
        Assert.Equal(request with { CoordinatorId = actorId }, repository.LastFilter);

        await service.ListVisibleAsync(actorId, "inspector", query);
        Assert.Equal(request with { InspectorId = actorId }, repository.LastFilter);

        await service.ListVisibleAsync(actorId, "technical_manager", query);
        Assert.Equal(request, repository.LastFilter);

        await service.ListVisibleAsync(actorId, "admin", query);
        Assert.Equal(request with { ManagerId = managerFilter }, repository.LastFilter);
    }

    [Fact]
    public void EmailImportIsGuardedByAUniqueMailboxMessageKey()
    {
        using var dbContext = CreateContext("coordinator", ManagerId);
        var entity = dbContext.Model.FindEntityType(typeof(RequestEmail))!;

        var index = Assert.Single(entity.GetIndexes(), item =>
            item.IsUnique && item.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(RequestEmail.CoordinatorId), nameof(RequestEmail.MailboxUidValidity), nameof(RequestEmail.MailboxUid)]));
        Assert.Equal("IX_RequestEmails_CoordinatorId_MailboxUidValidity_MailboxUid", index.GetDatabaseName());
    }

    [Fact]
    public void RequestEmailQueriesAreScopedToTheTenant()
    {
        using var dbContext = CreateContext("coordinator", ManagerId);

        var sql = dbContext.RequestEmails.ToQueryString();

        Assert.Contains("ManagerId", sql, StringComparison.Ordinal);
        Assert.Contains(ManagerId.ToString(), sql, StringComparison.Ordinal);
    }

    private static IncomingEmail Email(uint uid = 1) => new(
        "42",
        uid,
        "Kindly quote for internal/external examination of our compressor room air receivers.",
        null,
        "client@example.test",
        "Al Noor",
        "test-request-09",
        [new EmailAddressInfo("coordinator@example.test", null)],
        [],
        [],
        [],
        [],
        "<id@example.test>",
        null,
        null,
        DateTimeOffset.UtcNow.AddMinutes(-5));

    private static InspectaDbContext CreateContext(string role, Guid? tenantId)
    {
        var claims = new List<Claim> { new("role", role) };
        if (tenantId is Guid id) claims.Add(new Claim("tenant_id", id.ToString()));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        var options = new DbContextOptionsBuilder<InspectaDbContext>()
            .UseNpgsql("Host=localhost;Database=inspecta_test;Username=test;Password=test")
            .Options;
        return new InspectaDbContext(options, new CurrentTenant(accessor));
    }

    private sealed class CapturingRequestRepository(params InspectionRequest[] requests) : IRequestRepository
    {
        public RequestListFilter? LastFilter { get; private set; }
        public Guid? ClientManagerId { get; init; }

        public bool Deleted { get; private set; }
        public IReadOnlyList<RequestCount> Counts { get; init; } = [];

        public Task DeleteAsync(InspectionRequest request, CancellationToken cancellationToken = default)
        {
            Deleted = true;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RequestCount>> CountAsync(RequestListFilter scope, CancellationToken cancellationToken = default)
        {
            LastFilter = scope;
            return Task.FromResult(Counts);
        }

        public Task AddAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default) =>
            Task.FromResult(requests.SingleOrDefault(request => request.Id == requestId));

        public Task<RequestEmail?> GetEmailAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(requests.SingleOrDefault(request => request.Id == requestId)?.Email);

        public Task<IReadOnlyList<InspectionRequest>> ListAsync(RequestListFilter filter, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult<IReadOnlyList<InspectionRequest>>(requests);
        }

        public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ClientManagerId);
    }
}
