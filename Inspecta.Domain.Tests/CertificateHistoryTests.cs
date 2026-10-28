using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class CertificateHistoryTests
{
    [Fact]
    public async Task RequestMovesToReviewOnlyAfterEveryItemCertificateIsSubmitted()
    {
        var coordinatorId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var request = InspectionRequest.Create(
            "Acme",
            "Cairo",
            Guid.NewGuid(),
            [RequestItem.Create("Crane", 1), RequestItem.Create("Hoist", 1)],
            coordinatorId);
        request.TransitionTo(JobStatus.QuoteDraft, coordinatorId);
        request.TransitionTo(JobStatus.QuoteSent, coordinatorId);
        request.TransitionTo(JobStatus.QuoteAccepted, coordinatorId);
        request.AssignInspector(inspectorId, DateTimeOffset.UtcNow.AddDays(1), coordinatorId);
        request.TransitionTo(JobStatus.TaskAccepted, inspectorId);
        request.TransitionTo(JobStatus.InProgress, inspectorId);

        var requestRepository = new FakeRequestRepository(request);
        var certificateRepository = new FakeCertificateRepository(request);
        var service = new CertificateService(certificateRepository, requestRepository);
        var firstItemId = request.Items[0].Id;
        var secondItemId = request.Items[1].Id;

        await service.SaveDraftAsync(request.Id, firstItemId, inspectorId, "inspector", CompleteDraft());
        await service.SubmitAsync(request.Id, firstItemId, inspectorId, "inspector");

        Assert.Equal(JobStatus.InProgress, request.Status);

        await service.SaveDraftAsync(request.Id, secondItemId, inspectorId, "inspector", CompleteDraft());
        await service.SubmitAsync(request.Id, secondItemId, inspectorId, "inspector");

        Assert.Equal(JobStatus.CertificateSubmitted, request.Status);
        Assert.Equal(2, (await service.ListAsync(request.Id, inspectorId, "inspector"))?.Count);
    }

    [Fact]
    public async Task ClientHistoryIncludesAllRequestsAndTheirCertificates()
    {
        var client = Client.Create("Acme");
        var requestItem = RequestItem.Create("Crane", 1);
        var request = InspectionRequest.Create(
            "Acme",
            "Cairo",
            client.Id,
            [requestItem],
            Guid.NewGuid());
        var certificate = InspectionCertificate.Create(
            request.Id,
            Guid.NewGuid(),
            requestItemId: requestItem.Id);
        request.Certificates.Add(certificate);

        var service = new ClientService(new FakeClientRepository(client, request), new FakeUserRepository());
        var history = await service.GetHistoryAsync(client.Id);

        Assert.NotNull(history);
        var requestHistory = Assert.Single(history.Requests);
        Assert.Equal(request.Id, requestHistory.Request.Id);
        var certificateHistory = Assert.Single(requestHistory.Certificates);
        Assert.Equal(certificate.Id, certificateHistory.Id);
        Assert.Equal(requestItem.Id, certificateHistory.RequestItemId);
    }

    private static SaveCertificateDraftRequest CompleteDraft() => new()
    {
        Texts = new Dictionary<string, string>
        {
            ["standardOfInspection"] = "Standard",
            ["testType"] = "Visual inspection"
        },
        Dates = new Dictionary<string, DateTimeOffset>
        {
            ["examination"] = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero),
            ["nextExamination"] = new DateTimeOffset(2027, 4, 28, 0, 0, 0, TimeSpan.Zero)
        },
        Answers = new Dictionary<string, bool>
        {
            ["firstExamination"] = false,
            ["within6Months"] = true,
            ["within12Months"] = false,
            ["examinationScheme"] = true,
            ["exceptionalCircumstances"] = false,
            ["existingDanger"] = false,
            ["futureDanger"] = false
        },
        FunctionCheck = ChecklistAnswer.Pass,
        FinalResult = CertificateResult.SafeToOperate
    };

    private sealed class FakeRequestRepository(InspectionRequest request) : IRequestRepository
    {
        public Task AddAsync(InspectionRequest value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveAsync(InspectionRequest value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<InspectionRequest?> GetByIdAsync(
            Guid requestId,
            bool tracking,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<InspectionRequest?>(request.Id == requestId ? request : null);

        public Task<IReadOnlyList<InspectionRequest>> ListAsync(
            RequestListFilter filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InspectionRequest>>([request]);

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

    private sealed class FakeCertificateRepository(InspectionRequest request) : ICertificateRepository
    {
        public Task<IReadOnlyList<InspectionCertificate>> ListForRequestAsync(
            Guid requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InspectionCertificate>>(
                request.Certificates.Where(certificate => certificate.RequestId == requestId).ToArray());

        public Task<InspectionCertificate?> GetByRequestItemIdAsync(
            Guid requestId,
            Guid requestItemId,
            bool tracking,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(request.Certificates.SingleOrDefault(certificate =>
                certificate.RequestId == requestId && certificate.RequestItemId == requestItemId));

        public Task AddAsync(InspectionCertificate certificate, CancellationToken cancellationToken = default)
        {
            request.Certificates.Add(certificate);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeClientRepository(Client client, InspectionRequest request) : IClientRepository
    {
        public Task AddAsync(Client value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Client?> GetByIdAsync(
            Guid clientId,
            bool tracking,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Client?>(client.Id == clientId ? client : null);

        public Task<IReadOnlyList<Client>> ListAsync(
            bool includeArchived,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Client>>([client]);

        public Task<IReadOnlyList<InspectionRequest>> GetRequestHistoryAsync(
            Guid clientId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InspectionRequest>>(
                request.ClientId == clientId ? [request] : []);

        public Task SaveAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<AppUser?>(null);

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<AppUser?>(null);

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
