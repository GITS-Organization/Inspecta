using System.Security.Claims;
using Inspecta.Application.Contracts.Mail;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

public sealed class MailTriageTests
{
    private static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly Guid CoordinatorId = Guid.NewGuid();

    [Theory]
    [InlineData("Buyer@AlNoor.com", "buyer@alnoor.com")]
    [InlineData("@alnoor.com", "alnoor.com")]
    [InlineData("*@alnoor.com", "alnoor.com")]
    [InlineData("  AlNoor.com ", "alnoor.com")]
    public void SenderPatternsAreNormalized(string input, string expected) =>
        Assert.Equal(expected, ClientMailRule.NormalizePattern(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("localhost")]
    [InlineData("a@b")]
    [InlineData("a b@alnoor.com")]
    [InlineData("x@@alnoor.com")]
    [InlineData("-bad-.com")]
    public void InvalidSenderPatternsAreRejected(string input) =>
        Assert.Throws<ArgumentException>(() => ClientMailRule.NormalizePattern(input));

    [Fact]
    public void DomainRuleMatchesTheWholeDomainButNotSubdomainsOrLookalikes()
    {
        var rule = ClientMailRule.Create(ManagerId, "alnoor.com", null, CoordinatorId);

        Assert.True(rule.Matches("buyer@alnoor.com"));
        Assert.True(rule.Matches("BUYER@ALNOOR.COM"));
        Assert.False(rule.Matches("buyer@mail.alnoor.com"));
        Assert.False(rule.Matches("buyer@notalnoor.com"));
        Assert.False(rule.Matches(null));
    }

    [Fact]
    public void AddressRuleMatchesOnlyThatAddressAndWinsOverADomainRule()
    {
        var address = ClientMailRule.Create(ManagerId, "buyer@alnoor.com", Guid.NewGuid(), CoordinatorId);
        var domain = ClientMailRule.Create(ManagerId, "alnoor.com", null, CoordinatorId);

        Assert.False(address.Matches("other@alnoor.com"));
        Assert.Same(address, ClientMailRule.FindMatch([domain, address], "buyer@alnoor.com"));
        Assert.Same(domain, ClientMailRule.FindMatch([domain, address], "other@alnoor.com"));
        Assert.Null(ClientMailRule.FindMatch([domain, address], "someone@spam.example"));
    }

    [Fact]
    public void KnownSenderCreatesARequestLinkedToTheClient()
    {
        var clientId = Guid.NewGuid();

        var request = InspectionRequest.CreateFromEmail(
            ManagerId, CoordinatorId, Email(), clientId: clientId, clientName: "Al Noor Heavy Industries Ltd.");

        Assert.Equal(RequestTriage.Request, request.Triage);
        Assert.Equal(clientId, request.ClientId);
        Assert.Equal("Al Noor Heavy Industries Ltd.", request.ClientName);
        Assert.Equal(["location", "items"], request.GetMissingFields());
        Assert.Null(request.MaybeExpiresAt);
    }

    [Fact]
    public void MaybeEmailExpiresAfterOneWeekAndCannotBeWorkedOnUntilConfirmed()
    {
        var request = InspectionRequest.CreateFromEmail(
            ManagerId, CoordinatorId, Email(), triage: RequestTriage.Maybe);

        Assert.Equal(request.CreatedAt + TimeSpan.FromDays(7), request.MaybeExpiresAt);
        Assert.False(request.CanEditDetails);
        Assert.Throws<InvalidOperationException>(() => request.UpdateDetails("Al Noor", "Cairo", null, null, null));
        Assert.Throws<InvalidOperationException>(() => request.AddItem(RequestItem.Create("Valve", 1)));
        Assert.Throws<InvalidOperationException>(() => request.TransitionTo(JobStatus.QuoteDraft, CoordinatorId));

        request.Confirm();

        Assert.Equal(RequestTriage.Request, request.Triage);
        Assert.Null(request.MaybeExpiresAt);
        Assert.True(request.CanEditDetails);
        request.UpdateDetails("Al Noor", "Cairo", null, null, null);
        Assert.Throws<InvalidOperationException>(request.Confirm);
    }

    [Fact]
    public async Task ListDefaultsToRealRequestsAndClampsPaging()
    {
        var repository = new FakeRepository();
        var service = new InspectionRequestService(repository, null!);

        await service.ListVisibleAsync(CoordinatorId, "technical_manager", new Application.Contracts.Requests.RequestListQuery(Page: 3, PageSize: 1000, Search: "  noor "));

        Assert.Equal(RequestTriage.Request, repository.LastFilter!.Triage);
        Assert.Equal(Application.Contracts.Requests.RequestListQuery.MaxPageSize, repository.LastFilter.Take);
        Assert.Equal(2 * Application.Contracts.Requests.RequestListQuery.MaxPageSize, repository.LastFilter.Skip);
        Assert.Equal("noor", repository.LastFilter.Search);
        Assert.Equal([JobStatus.QuoteDraft, JobStatus.QuoteSent, JobStatus.ClientCountered], repository.LastFilter.ExcludeStatuses);

        await service.ListVisibleAsync(CoordinatorId, "technical_manager", new Application.Contracts.Requests.RequestListQuery(Triage: RequestTriage.Maybe, Page: 0, PageSize: 0));

        Assert.Equal(RequestTriage.Maybe, repository.LastFilter.Triage);
        Assert.Equal(0, repository.LastFilter.Skip);
        Assert.Equal(1, repository.LastFilter.Take);
    }

    [Fact]
    public async Task SummaryCountsTabsAndStatusesSeparately()
    {
        var repository = new FakeRepository
        {
            Counts =
            [
                new RequestCount(RequestTriage.Request, JobStatus.RequestReceived, 3),
                new RequestCount(RequestTriage.Request, JobStatus.QuoteDraft, 2),
                new RequestCount(RequestTriage.Maybe, JobStatus.RequestReceived, 40)
            ]
        };
        var service = new InspectionRequestService(repository, null!);

        var summary = await service.GetSummaryAsync(CoordinatorId, "coordinator");

        Assert.Equal(3, summary.Requests);
        Assert.Equal(40, summary.Maybe);
        Assert.Equal([JobStatus.RequestReceived, JobStatus.QuoteDraft], summary.ByStatus.Select(item => item.Status));
        Assert.Equal(CoordinatorId, repository.LastFilter!.CoordinatorId);
        Assert.Null(repository.LastFilter.Triage);
    }

    [Fact]
    public async Task OwnerCanConfirmOrDeleteAMaybeEmailButOthersCannot()
    {
        var maybe = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email(), triage: RequestTriage.Maybe);
        var repository = new FakeRepository(maybe);
        var service = new InspectionRequestService(repository, null!);
        var otherCoordinator = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConfirmAsync(maybe.Id, otherCoordinator, "coordinator"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteMaybeAsync(maybe.Id, otherCoordinator, "coordinator"));
        await Assert.ThrowsAsync<AuthException>(() => service.DeleteMaybeAsync(maybe.Id, ManagerId, "technical_manager"));
        Assert.False(repository.Deleted);

        await service.DeleteMaybeAsync(maybe.Id, CoordinatorId, "coordinator");
        Assert.True(repository.Deleted);

        var confirmed = await service.ConfirmAsync(maybe.Id, CoordinatorId, "coordinator");
        Assert.Equal(RequestTriage.Request, confirmed.Triage);
        Assert.Null(confirmed.MaybeExpiresAt);
    }

    [Fact]
    public async Task ConfirmedRequestsCannotBeDeletedThroughTheMaybeEndpoint()
    {
        var request = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email());
        var repository = new FakeRepository(request);
        var service = new InspectionRequestService(repository, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteMaybeAsync(request.Id, CoordinatorId, "coordinator"));
        Assert.False(repository.Deleted);
    }

    [Fact]
    public void ChangingTheMailboxRestartsTheSyncFromNowButAPasswordChangeDoesNot()
    {
        var created = DateTimeOffset.UtcNow.AddDays(-3);
        var config = CoordinatorEmailConfig.Create(
            CoordinatorId, ManagerId, "imap.example.com", 993, "coordinator@example.com", "secret", true, created);

        Assert.Equal(created, config.SyncFromUtc);

        var passwordChange = created.AddDays(1);
        config.Update("imap.example.com", 993, "coordinator@example.com", "new-secret", true, passwordChange);
        Assert.Equal(created, config.SyncFromUtc);

        var mailboxChange = created.AddDays(2);
        config.Update("imap.other.example.com", 993, "coordinator@example.com", "new-secret", true, mailboxChange);
        Assert.Equal(mailboxChange, config.SyncFromUtc);
        Assert.Null(config.LastUidValidity);
        Assert.Equal(0, config.LastUid);
    }

    [Fact]
    public async Task RuleCreationRequiresAnActiveManagerAndAClientOfTheSameTeam()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var repository = new FakeRuleRepository { ClientManagerId = Guid.NewGuid() };
        var service = new ClientMailRuleService(repository, new IncomingEmailRequestTests.FakeUserRepository(manager));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            Guid.NewGuid(), CoordinatorId, new CreateClientMailRuleRequest { Pattern = "alnoor.com" }));
        await Assert.ThrowsAsync<AuthException>(() => service.CreateAsync(
            manager.Id, CoordinatorId, new CreateClientMailRuleRequest { Pattern = "alnoor.com", ClientId = Guid.NewGuid() }));

        repository.ClientManagerId = manager.Id;
        var created = await service.CreateAsync(
            manager.Id, CoordinatorId, new CreateClientMailRuleRequest { Pattern = "@AlNoor.com", ClientId = Guid.NewGuid() });

        Assert.Equal("alnoor.com", created.Pattern);
        Assert.True(created.IsDomain);
        Assert.Equal(manager.Id, created.ManagerId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            manager.Id, CoordinatorId, new CreateClientMailRuleRequest { Pattern = "alnoor.com" }));
    }

    [Fact]
    public void MailRulesAreUniquePerTeamAndScopedToTheTenant()
    {
        using var dbContext = CreateContext("coordinator", ManagerId);
        var entity = dbContext.Model.FindEntityType(typeof(ClientMailRule))!;

        Assert.Single(entity.GetIndexes(), item =>
            item.IsUnique && item.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(ClientMailRule.ManagerId), nameof(ClientMailRule.Pattern)]));
        var sql = dbContext.ClientMailRules.ToQueryString();
        Assert.Contains("ManagerId", sql, StringComparison.Ordinal);
        Assert.Contains(ManagerId.ToString(), sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ADifferentAddressOfTheSameClientIsRecognisedWithoutARule()
    {
        var clientId = Guid.NewGuid();
        var implied = ClientMailRule.ForKnownAddress(ManagerId, " Procurement@AlNoor.com ", clientId);
        var domain = ClientMailRule.Create(ManagerId, "alnoor.com", Guid.NewGuid(), CoordinatorId);

        Assert.Equal("procurement@alnoor.com", implied.Pattern);
        Assert.Equal(clientId, implied.ClientId);
        Assert.Same(implied, ClientMailRule.FindMatch([domain, implied], "procurement@alnoor.com"));
        Assert.Throws<ArgumentException>(() => ClientMailRule.ForKnownAddress(ManagerId, "alnoor.com", clientId));
    }

    [Fact]
    public void ARequestFromAKnownClientStartsWithTheClientsLocationAndKeepsWhatWasTyped()
    {
        var clientId = Guid.NewGuid();

        var request = InspectionRequest.CreateFromEmail(
            ManagerId, CoordinatorId, Email(), clientId: clientId, clientName: "Al Noor", location: " Borg El Arab ");

        Assert.Equal("Borg El Arab", request.Location);
        Assert.Equal(["items"], request.GetMissingFields());

        var maybe = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email(), triage: RequestTriage.Maybe);
        maybe.AdoptClient(clientId, "Al Noor", "Cairo");
        Assert.Equal("Cairo", maybe.Location);

        var typed = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email(), triage: RequestTriage.Maybe, location: "Site 7");
        typed.AdoptClient(clientId, "Al Noor", "Cairo");
        Assert.Equal("Site 7", typed.Location);
    }

    [Fact]
    public void AMaybeTakesTheClientWhenItsSenderIsRegistered()
    {
        var clientId = Guid.NewGuid();
        var maybe = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email(), triage: RequestTriage.Maybe);

        maybe.AdoptClient(clientId, " Al Noor ");

        Assert.Equal(RequestTriage.Request, maybe.Triage);
        Assert.Equal(clientId, maybe.ClientId);
        Assert.Equal("Al Noor", maybe.ClientName);
        Assert.Throws<InvalidOperationException>(() => maybe.AdoptClient(clientId, "Al Noor"));
    }

    [Fact]
    public async Task AddingASenderToAClientTurnsItsWaitingMaybesIntoRequests()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var fromBuyer = InspectionRequest.CreateFromEmail(manager.Id, CoordinatorId, Email(), triage: RequestTriage.Maybe);
        var fromSomeoneElse = InspectionRequest.CreateFromEmail(
            manager.Id, CoordinatorId, Email() with { FromAddress = "other@elsewhere.com" }, triage: RequestTriage.Maybe);
        var clientId = Guid.NewGuid();
        var repository = new FakeRuleRepository { ClientManagerId = manager.Id, ClientName = "Al Noor Heavy Industries", ClientLocation = "Borg El Arab, Alexandria" };
        repository.Waiting.AddRange([fromBuyer, fromSomeoneElse]);
        var service = new ClientMailRuleService(repository, new IncomingEmailRequestTests.FakeUserRepository(manager));

        var added = await service.AddForClientAsync(clientId, manager.Id, CoordinatorId, new AddClientEmailRequest { Pattern = "alnoor.com" });

        Assert.Equal(1, added.PromotedRequests);
        Assert.Equal(clientId, added.ClientId);
        Assert.Equal(RequestTriage.Request, fromBuyer.Triage);
        Assert.Equal("Al Noor Heavy Industries", fromBuyer.ClientName);
        Assert.Equal("Borg El Arab, Alexandria", fromBuyer.Location);
        Assert.Equal(RequestTriage.Maybe, fromSomeoneElse.Triage);
        Assert.Equal(string.Empty, fromSomeoneElse.Location);
        Assert.Single(await service.ListForClientAsync(clientId, manager.Id));
    }

    [Fact]
    public async Task AClientsEmailsCanOnlyBeManagedByItsOwnTeam()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var repository = new FakeRuleRepository { ClientManagerId = manager.Id, ClientName = "Al Noor" };
        var service = new ClientMailRuleService(repository, new IncomingEmailRequestTests.FakeUserRepository(manager));
        var clientId = Guid.NewGuid();
        var otherTeam = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ListForClientAsync(clientId, otherTeam));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AddForClientAsync(clientId, otherTeam, CoordinatorId, new AddClientEmailRequest { Pattern = "alnoor.com" }));

        var added = await service.AddForClientAsync(clientId, null, CoordinatorId, new AddClientEmailRequest { Pattern = "buyer@alnoor.com" });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RemoveForClientAsync(Guid.NewGuid(), added.Id, manager.Id));
        await service.RemoveForClientAsync(clientId, added.Id, manager.Id);
        Assert.Empty(await service.ListForClientAsync(clientId, manager.Id));
    }

    [Fact]
    public async Task ConfirmingAMaybeNeedsItsSenderToBelongToAClient()
    {
        var clientId = Guid.NewGuid();
        var maybe = InspectionRequest.CreateFromEmail(ManagerId, CoordinatorId, Email(), triage: RequestTriage.Maybe);
        var stranger = new FakeSenders();
        var service = new InspectionRequestService(new FakeRepository(maybe), null!, senderRules: stranger);

        var refused = await Assert.ThrowsAsync<AuthException>(() => service.ConfirmAsync(maybe.Id, CoordinatorId, "coordinator"));
        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(RequestTriage.Maybe, maybe.Triage);

        var known = new FakeSenders(new SenderRule(ClientMailRule.ForKnownAddress(ManagerId, "buyer@alnoor.com", clientId), "Al Noor", "Cairo"));
        service = new InspectionRequestService(new FakeRepository(maybe), null!, senderRules: known);

        var confirmed = await service.ConfirmAsync(maybe.Id, CoordinatorId, "coordinator");

        Assert.Equal(RequestTriage.Request, confirmed.Triage);
        Assert.Equal(clientId, confirmed.ClientId);
        Assert.Equal("Al Noor", confirmed.ClientName);
        Assert.Equal("Cairo", confirmed.Location);
    }

    private sealed class FakeSenders(params SenderRule[] rules) : IEmailSyncRepository
    {
        public Task<IReadOnlyList<CoordinatorEmailConfig>> ListActiveConfigsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CoordinatorEmailConfig>>([]);

        public Task<IReadOnlyList<SenderRule>> ListSenderRulesAsync(Guid managerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SenderRule>>(rules);

        public Task<bool> SaveRequestIfNewAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task SaveSyncCursorAsync(Guid coordinatorId, string uidValidity, long lastUid, DateTimeOffset syncFromUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountUntouchedEmailRequestsAsync(DateTimeOffset createdBefore, Guid? managerId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> DeleteUntouchedEmailRequestsAsync(DateTimeOffset createdBefore, Guid? managerId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> DeleteExpiredMaybeRequestsAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private static IncomingEmail Email() => new(
        "42", 1, "Kindly quote for our air receivers.", null, "buyer@alnoor.com", "Al Noor", "Quote request",
        [], [], [], [], [], "<id@alnoor.com>", null, null, DateTimeOffset.UtcNow.AddMinutes(-5));

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

    private sealed class FakeRepository(params InspectionRequest[] requests) : IRequestRepository
    {
        public RequestListFilter? LastFilter { get; private set; }
        public bool Deleted { get; private set; }
        public IReadOnlyList<RequestCount> Counts { get; init; } = [];

        public Task AddAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(InspectionRequest request, CancellationToken cancellationToken = default)
        {
            Deleted = true;
            return Task.CompletedTask;
        }

        public Task AddItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default) =>
            Task.FromResult(requests.SingleOrDefault(request => request.Id == requestId));

        public Task<RequestEmail?> GetEmailAsync(Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RequestEmail?>(null);

        public Task<IReadOnlyList<InspectionRequest>> ListAsync(RequestListFilter filter, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult<IReadOnlyList<InspectionRequest>>(requests);
        }

        public Task<IReadOnlyList<RequestCount>> CountAsync(RequestListFilter scope, CancellationToken cancellationToken = default)
        {
            LastFilter = scope;
            return Task.FromResult(Counts);
        }

        public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
    }

    private sealed class FakeRuleRepository : IClientMailRuleRepository
    {
        private readonly List<ClientMailRule> rules = [];

        public Guid? ClientManagerId { get; set; }

        public Task<IReadOnlyList<ClientMailRule>> ListAsync(Guid? managerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ClientMailRule>>(rules.Where(rule => managerId is null || rule.ManagerId == managerId).ToArray());

        public Task<ClientMailRule?> GetByIdAsync(Guid ruleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(rules.SingleOrDefault(rule => rule.Id == ruleId));

        public Task<bool> ExistsAsync(Guid managerId, string pattern, CancellationToken cancellationToken = default) =>
            Task.FromResult(rules.Any(rule => rule.ManagerId == managerId && rule.Pattern == pattern));

        public Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ClientManagerId);

        public string? ClientName { get; set; }
        public string? ClientLocation { get; set; }
        public List<InspectionRequest> Waiting { get; } = [];

        public Task<ClientSummary?> GetClientSummaryAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ClientName is null ? null : new ClientSummary(ClientName, ClientLocation));

        public Task<int> PromoteMaybesAsync(
            ClientMailRule rule,
            ClientSummary client,
            Func<InspectionRequest, Task<IReadOnlyList<RequestItem>>>? autofill = null,
            CancellationToken cancellationToken = default)
        {
            var matching = Waiting.Where(request => rule.Matches(request.EmailFrom)).ToList();
            foreach (var request in matching) request.AdoptClient(rule.ClientId!.Value, client.Name, client.Location);
            return Task.FromResult(matching.Count);
        }

        public Task AddAsync(ClientMailRule rule, CancellationToken cancellationToken = default)
        {
            rules.Add(rule);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(ClientMailRule rule, CancellationToken cancellationToken = default)
        {
            rules.Remove(rule);
            return Task.CompletedTask;
        }
    }
}
