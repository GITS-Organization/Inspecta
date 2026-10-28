using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class IncomingEmailRequestTests
{
    [Fact]
    public async Task CreatingRequestUsesResolvedTenantInsteadOfRequestBodyOwner()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var repository = new FakeRequestRepository();
        var service = new InspectionRequestService(repository, new FakeUserRepository(manager));

        await service.CreateAsync(
            new CreateInspectionRequest
            {
                ManagerId = Guid.NewGuid(),
                ClientName = "Example Client",
                Location = "Cairo",
                Items = [new CreateRequestItem { Name = "Crane" }]
            },
            Guid.NewGuid(),
            manager.Id);

        Assert.Equal(manager.Id, repository.SavedRequest?.ManagerId);
    }

    [Fact]
    public async Task AssignmentRejectsInspectorFromAnotherTenant()
    {
        var managerId = Guid.NewGuid();
        var request = InspectionRequest.Create(
            "Example Client",
            "Cairo",
            Guid.NewGuid(),
            [RequestItem.Create("Crane", 1)],
            Guid.NewGuid(),
            managerId: managerId);
        request.TransitionTo(JobStatus.QuoteDraft, Guid.NewGuid());
        request.TransitionTo(JobStatus.QuoteSent, Guid.NewGuid());
        request.TransitionTo(JobStatus.QuoteAccepted, Guid.NewGuid());
        var otherManagerId = Guid.NewGuid();
        var inspector = AppUser.Create(
            "inspector@example.com",
            "Inspector",
            UserRole.Inspector,
            managerId: otherManagerId);
        var service = new InspectionRequestService(
            new FakeRequestRepository(request),
            new FakeUserRepository(inspector));

        await Assert.ThrowsAsync<AuthException>(() => service.AssignAsync(
            request.Id,
            managerId,
            inspector.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            null));
    }

    [Fact]
    public async Task IncomingEmailCreatesEmailSourcedInspectionRequest()
    {
        var repository = new FakeRequestRepository();
        var service = new InspectionRequestService(repository, new FakeUserRepository());
        var emailBody = "Please inspect our crane. It makes a loud noise.";

        var created = await service.CreateFromEmailAsync(
            new CreateInspectionRequest
            {
                ClientName = "Example Client",
                Location = "Cairo",
                Items =
                [
                    new CreateRequestItem
                    {
                        Name = "Overhead Crane",
                        Type = "Overhead Crane",
                        Quantity = 1,
                        SerialNumber = "CR-1042",
                        IssueDescription = "It makes a loud noise."
                    }
                ]
            },
            "contact@example.com",
            emailBody,
            Guid.NewGuid(),
            null);

        var savedRequest = Assert.IsType<InspectionRequest>(repository.SavedRequest);
        Assert.Equal(RequestSource.Email, created.Source);
        Assert.Equal("contact@example.com", created.EmailFrom);
        Assert.Equal(emailBody, created.EmailPreview);
        Assert.Equal("It makes a loud noise.", Assert.Single(savedRequest.Items).IssueDescription);
    }

    private sealed class FakeRequestRepository : IRequestRepository
    {
        public InspectionRequest? SavedRequest { get; private set; }

        public FakeRequestRepository(InspectionRequest? request = null) =>
            SavedRequest = request;

        public Task AddAsync(InspectionRequest request, CancellationToken cancellationToken = default)
        {
            SavedRequest = request;
            return Task.CompletedTask;
        }

        public Task SaveAsync(InspectionRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default) =>
            Task.FromResult(SavedRequest?.Id == requestId ? SavedRequest : null);

        public Task<IReadOnlyList<InspectionRequest>> ListAsync(RequestListFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InspectionRequest>>([]);

        public Task DeleteAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<RequestCount>> CountAsync(RequestListFilter scope, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RequestCount>>([]);

        public Task AddItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<RequestEmail?> GetEmailAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RequestEmail?>(null);

        public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);
    }

    internal sealed class FakeUserRepository(params AppUser[] users) : IUserRepository
    {
        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<AppUser?>(null);

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(users.SingleOrDefault(user => user.Id == id));

        public Task<IReadOnlyList<AppUser>> ListAsync(
            Guid? managerId = null,
            UserRole? role = null,
            bool? isActive = null,
            CancellationToken cancellationToken = default,
            bool includeDeleted = false) =>
            Task.FromResult<IReadOnlyList<AppUser>>([]);

        public Task AddAsync(AppUser user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddTeamMemberAsync(
            AppUser user,
            CoordinatorEmailConfig? emailConfig,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(AppUser user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SoftDeleteAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default) => Task.FromResult<RefreshToken?>(null);
        public Task<bool> RotateRefreshTokenAsync(RefreshToken currentToken, RefreshToken nextToken, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RevokeRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeRefreshTokenFamilyAsync(Guid familyId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
