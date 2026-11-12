using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class UserManagementTests
{
    [Fact]
    public async Task TechnicalManagerCanOnlySeeTheirOwnTeamMembers()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var otherManager = AppUser.Create("other-manager@example.com", "Other Manager", UserRole.TechnicalManager);
        var inScope = AppUser.Create("inspector@example.com", "Inspector", UserRole.Inspector, managerId: manager.Id);
        var outOfScope = AppUser.Create("other@example.com", "Other", UserRole.Coordinator, managerId: otherManager.Id);
        var users = new MemoryUserRepository(manager, otherManager, inScope, outOfScope);
        var service = CreateService(users);

        var team = await service.ListTeamAsync(manager.Id);
        var otherMember = await service.GetTeamMemberAsync(manager.Id, outOfScope.Id);

        Assert.Equal([inScope.Id], team.Select(user => user.Id));
        Assert.Null(otherMember);
    }

    [Fact]
    public async Task TechnicalManagerCannotCreatePrivilegedRoles()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var service = CreateService(new MemoryUserRepository(manager));

        var error = await Assert.ThrowsAsync<AuthException>(() => service.CreateTeamMemberAsync(
            manager.Id,
            new CreateTeamMemberRequest
            {
                Email = "admin@example.com",
                FullName = "New Admin",
                Password = "StrongPassword123!",
                Role = UserRole.Admin
            }));

        Assert.Equal(403, error.StatusCode);
        Assert.Equal("role_forbidden", error.Code);
    }

    [Fact]
    public async Task TechnicalManagerCreatesAllowedRolesAssignedToTheirAccount()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var users = new MemoryUserRepository(manager);
        var service = CreateService(users);

        var created = await service.CreateTeamMemberAsync(
            manager.Id,
            new CreateTeamMemberRequest
            {
                Email = "inspector@example.com",
                FullName = "Inspector",
                Password = "StrongPassword123!",
                Role = UserRole.Inspector
            });

        Assert.Equal(UserRole.Inspector, created.Role);
        Assert.Equal(manager.Id, created.ManagerId);
        Assert.Null(created.CreatedById);
    }

    [Fact]
    public async Task TechnicalManagerMustProvideEmailSettingsWhenCreatingCoordinator()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var service = CreateService(new MemoryUserRepository(manager));

        var error = await Assert.ThrowsAsync<AuthException>(() => service.CreateTeamMemberAsync(
            manager.Id,
            new CreateTeamMemberRequest
            {
                Email = "coordinator@example.com",
                FullName = "Coordinator",
                Password = "StrongPassword123!",
                Role = UserRole.Coordinator
            }));

        Assert.Equal("coordinator_email_config_required", error.Code);
    }

    [Fact]
    public async Task InspectorCannotBeCreatedWithEmailSettings()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var service = CreateService(new MemoryUserRepository(manager));

        var error = await Assert.ThrowsAsync<AuthException>(() => service.CreateTeamMemberAsync(
            manager.Id,
            new CreateTeamMemberRequest
            {
                Email = "inspector@example.com",
                FullName = "Inspector",
                Password = "StrongPassword123!",
                Role = UserRole.Inspector,
                EmailConfig = CreateEmailConfigRequest()
            }));

        Assert.Equal("email_config_coordinator_only", error.Code);
    }

    [Fact]
    public async Task CoordinatorCreationStoresEncryptedMailboxSettingsWithTheAccount()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var users = new MemoryUserRepository(manager);
        var service = CreateService(users);

        var created = await service.CreateTeamMemberAsync(
            manager.Id,
            new CreateTeamMemberRequest
            {
                Email = "coordinator@example.com",
                FullName = "Coordinator",
                Password = "StrongPassword123!",
                Role = UserRole.Coordinator,
                EmailConfig = CreateEmailConfigRequest()
            });

        var config = Assert.Single(users.EmailConfigs);
        Assert.Equal(created.Id, config.CoordinatorId);
        Assert.Equal(manager.Id, config.TenantId);
        Assert.Equal("encrypted:mail-password", config.EncryptedPassword);
        Assert.Equal("imap.example.com", config.ImapServer);
    }

    [Fact]
    public async Task AdminCreationAlsoRequiresAndStoresCoordinatorEmailSettings()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var users = new MemoryUserRepository(manager);
        var service = CreateService(users);

        var created = await service.CreateAsync(new CreateStaffUserRequest
        {
            Email = "coordinator@example.com",
            FullName = "Coordinator",
            Password = "StrongPassword123!",
            Role = UserRole.Coordinator,
            ManagerId = manager.Id,
            EmailConfig = CreateEmailConfigRequest()
        });

        Assert.Equal(UserRole.Coordinator, created.Role);
        Assert.Equal("encrypted:mail-password", Assert.Single(users.EmailConfigs).EncryptedPassword);
    }

    [Fact]
    public async Task TechnicalManagerProvisioningCreatesAccountWithSubscription()
    {
        var users = new MemoryUserRepository();
        var companies = new FakeCompanyRepository();
        var service = CreateService(users, companies);

        var created = await service.CreateTechnicalManagerAsync(new CreateTechnicalManagerRequest
        {
            Email = "manager@example.com",
            FullName = "Technical Manager",
            Password = "StrongPassword123!",
            SubscriptionMonths = 12
        });

        var account = Assert.Single(await users.ListAsync(role: UserRole.TechnicalManager));
        var company = Assert.Single(companies.Items);
        Assert.Equal(account.Id, created.Id);
        Assert.Equal(account.Id, company.Id);
        Assert.Equal(account.Id, company.DefaultReviewerId);
        Assert.Equal(SubscriptionStatus.Active, created.SubscriptionStatus);
        Assert.InRange(
            created.SubscriptionEndDate!.Value,
            DateTimeOffset.UtcNow.AddMonths(11),
            DateTimeOffset.UtcNow.AddMonths(13));
    }

    [Fact]
    public async Task ATechnicalManagerIsCreatedTogetherWithItsCompanyProfile()
    {
        var users = new MemoryUserRepository();
        var profiles = new RecordingProfiles();
        var service = new UserManagementService(users, new FakePasswordService(), new FakeSecretProtector(), profiles, companies: new FakeCompanyRepository());
        var profile = new Inspecta.Application.Contracts.Company.SaveCompanyProfileRequest { Name = "Acme", Address = "Cairo" };

        var created = await service.CreateTechnicalManagerAsync(new CreateTechnicalManagerRequest
        {
            Email = "manager@acme.com", FullName = "Acme Manager", Password = "StrongPassword123!", CompanyProfile = profile
        });

        Assert.Equal(created.Id, profiles.SavedFor);
        Assert.Same(profile, profiles.Saved);
    }

    [Fact]
    public async Task AMistakeInTheCompanyProfileCreatesNoAccount()
    {
        var users = new MemoryUserRepository();
        var profiles = new RecordingProfiles { Fail = true };
        var service = new UserManagementService(users, new FakePasswordService(), new FakeSecretProtector(), profiles, companies: new FakeCompanyRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateTechnicalManagerAsync(new CreateTechnicalManagerRequest
        {
            Email = "manager@acme.com", FullName = "Acme Manager", Password = "StrongPassword123!",
            CompanyProfile = new Inspecta.Application.Contracts.Company.SaveCompanyProfileRequest { Name = "Acme", Address = "Cairo" }
        }));

        Assert.Empty(await users.ListAsync(role: UserRole.TechnicalManager));
        Assert.Null(profiles.Saved);
    }

    [Fact]
    public async Task DeletedUsersAreRemovedForGood()
    {
        var user = AppUser.Create("staff@example.com", "Staff Member", UserRole.Inspector);
        var users = new MemoryUserRepository(user);
        var service = CreateService(users);

        await service.DeactivateAsync(user.Id);

        Assert.Null(await users.GetByIdAsync(user.Id));
        Assert.Empty(await users.ListAsync(includeDeleted: true));
    }

    [Fact]
    public async Task ManagerCanSuspendOnlyMembersAssignedToThem()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var otherManager = AppUser.Create("other-manager@example.com", "Other Manager", UserRole.TechnicalManager);
        var member = AppUser.Create("member@example.com", "Member", UserRole.Inspector, managerId: manager.Id);
        var outOfScope = AppUser.Create("other@example.com", "Other Member", UserRole.Inspector, managerId: otherManager.Id);
        var users = new MemoryUserRepository(manager, otherManager, member, outOfScope);
        var service = CreateService(users);

        await service.SetTeamMemberActiveAsync(manager.Id, member.Id, isActive: false);

        var error = await Assert.ThrowsAsync<AuthException>(() =>
            service.SetTeamMemberActiveAsync(manager.Id, outOfScope.Id, isActive: false));
        Assert.False(member.IsActive);
        Assert.Equal("user_not_found", error.Code);
        Assert.True(outOfScope.IsActive);
    }

    [Fact]
    public async Task SuspendedOrExpiredManagerCannotAccessManagerOperations()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var users = new MemoryUserRepository(manager);
        var companies = CompaniesOf(users);
        var service = CreateService(users, companies);
        var company = companies.Items.Single();

        company.UpdateSubscription(SubscriptionStatus.Suspended, DateTimeOffset.UtcNow.AddDays(10));
        var suspended = await Assert.ThrowsAsync<AuthException>(() => service.ListTeamAsync(manager.Id));
        company.UpdateSubscription(SubscriptionStatus.Active, DateTimeOffset.UtcNow.AddSeconds(-1));
        var expired = await Assert.ThrowsAsync<AuthException>(() => service.ListTeamAsync(manager.Id));

        Assert.Equal("subscription_inactive", suspended.Code);
        Assert.Equal("subscription_inactive", expired.Code);
    }

    [Fact]
    public async Task AdminCanUpdateTechnicalManagerSubscription()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var users = new MemoryUserRepository(manager);
        var companies = CompaniesOf(users);
        var service = CreateService(users, companies);

        var updated = await service.UpdateTechnicalManagerSubscriptionAsync(
            manager.Id,
            new UpdateTechnicalManagerSubscriptionRequest
            {
                SubscriptionStatus = SubscriptionStatus.Suspended,
                SubscriptionEndDate = DateTimeOffset.UtcNow.AddMonths(1)
            });

        Assert.Equal(SubscriptionStatus.Suspended, updated.SubscriptionStatus);
        Assert.False(companies.Items.Single().HasActiveSubscription());
    }

    [Fact]
    public async Task NonAdminStaffRequireAnActiveTechnicalManager()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var managerId = manager.Id;
        var users = new MemoryUserRepository(manager);
        var service = CreateService(users);

        var error = await Assert.ThrowsAsync<AuthException>(() => service.CreateAsync(new CreateStaffUserRequest
        {
            Email = "staff@example.com",
            FullName = "Staff Member",
            Password = "StrongPassword123!",
            Role = UserRole.Inspector
        }));
        var created = await service.CreateAsync(new CreateStaffUserRequest
        {
            Email = "staff@example.com",
            FullName = "Staff Member",
            Password = "StrongPassword123!",
            Role = UserRole.Inspector,
            ManagerId = managerId
        });

        Assert.Equal("manager_required", error.Code);
        Assert.Equal(managerId, created.ManagerId);
    }

    private static CoordinatorEmailConfigRequest CreateEmailConfigRequest() => new()
    {
        ImapServer = "imap.example.com",
        ImapPort = 993,
        EmailUsername = "coordinator@example.com",
        Password = "mail-password"
    };

    private static UpdateStaffProfileRequest Profile(UserRole role, Guid? managerId = null, DateTimeOffset? leaveUntil = null, bool onLeave = false) =>
        new() { FullName = "Staff Member", Role = role, ManagerId = managerId, IsOnLeave = onLeave, LeaveUntil = leaveUntil };

    [Fact]
    public async Task AStaffMemberMovesToAnotherActiveTeamAndKeepsTheirRole()
    {
        var first = AppUser.Create("first@example.com", "First", UserRole.TechnicalManager);
        var second = AppUser.Create("second@example.com", "Second", UserRole.TechnicalManager);
        var member = AppUser.Create("coordinator@example.com", "Coordinator", UserRole.Coordinator, managerId: first.Id);
        var service = CreateService(new MemoryUserRepository(first, second, member));

        var moved = await service.UpdateProfileAsync(member.Id, Profile(UserRole.Coordinator, second.Id));

        Assert.Equal((UserRole.Coordinator, second.Id), (moved.Role, moved.ManagerId));
    }

    [Fact]
    public async Task APromotionToAdminCannotCarryATeam()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var member = AppUser.Create("inspector@example.com", "Inspector", UserRole.Inspector, managerId: manager.Id);
        var service = CreateService(new MemoryUserRepository(manager, member));

        var error = await Assert.ThrowsAsync<AuthException>(() => service.UpdateProfileAsync(member.Id, Profile(UserRole.Admin, manager.Id)));

        Assert.Equal(("admin_manager_not_allowed", 400), (error.Code, error.StatusCode));
    }

    [Fact]
    public async Task ATechnicalManagerCannotBecomeStaffAndStaffCannotBecomeATechnicalManager()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var other = AppUser.Create("other@example.com", "Other", UserRole.TechnicalManager);
        var member = AppUser.Create("inspector@example.com", "Inspector", UserRole.Inspector, managerId: manager.Id);
        var service = CreateService(new MemoryUserRepository(manager, other, member));

        var demote = await Assert.ThrowsAsync<AuthException>(() => service.UpdateProfileAsync(manager.Id, Profile(UserRole.Inspector, other.Id)));
        var promote = await Assert.ThrowsAsync<AuthException>(() => service.UpdateProfileAsync(member.Id, Profile(UserRole.TechnicalManager)));

        Assert.Equal("technical_manager_role_immutable", demote.Code);
        Assert.Equal("technical_manager_role_immutable", promote.Code);
    }

    [Fact]
    public async Task StaffNeedATeamAndTheTeamMustBeAnActiveTechnicalManager()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var coordinator = AppUser.Create("coordinator@example.com", "Coordinator", UserRole.Coordinator, managerId: manager.Id);
        var loose = AppUser.Create("loose@example.com", "Loose", UserRole.Inspector);
        var service = CreateService(new MemoryUserRepository(manager, coordinator, loose));

        var none = await Assert.ThrowsAsync<AuthException>(() => service.UpdateProfileAsync(loose.Id, Profile(UserRole.Inspector)));
        var notAManager = await Assert.ThrowsAsync<AuthException>(() => service.UpdateProfileAsync(loose.Id, Profile(UserRole.Inspector, coordinator.Id)));

        Assert.Equal("manager_required", none.Code);
        Assert.Equal("manager_invalid", notAManager.Code);
    }

    [Fact]
    public async Task AnUnknownUserIsNotFoundAndLeaveMustEndInTheFuture()
    {
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager);
        var member = AppUser.Create("inspector@example.com", "Inspector", UserRole.Inspector, managerId: manager.Id);
        var service = CreateService(new MemoryUserRepository(manager, member));

        var missing = await Assert.ThrowsAsync<AuthException>(() => service.UpdateProfileAsync(Guid.NewGuid(), Profile(UserRole.Inspector, manager.Id)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateProfileAsync(member.Id, Profile(UserRole.Inspector, manager.Id, DateTimeOffset.UtcNow.AddDays(-1), onLeave: true)));

        Assert.Equal(404, missing.StatusCode);
    }

    // Every technical manager of the repository heads a company with the same id (a company that existed before the Companies table).
    private static UserManagementService CreateService(MemoryUserRepository users, FakeCompanyRepository? companies = null) =>
        new(users, new FakePasswordService(), new FakeSecretProtector(), companies: companies ?? CompaniesOf(users));

    private static FakeCompanyRepository CompaniesOf(MemoryUserRepository users) =>
        new(users.Users.Where(user => user.Role == UserRole.TechnicalManager).Select(manager =>
        {
            var company = Company.Create(manager.FullName, id: manager.Id);
            company.SetDefaultReviewer(manager.Id);
            return company;
        }).ToArray());

    private sealed class RecordingProfiles : ICompanyProfileService
    {
        public bool Fail { get; init; }
        public Guid? SavedFor { get; private set; }
        public Inspecta.Application.Contracts.Company.SaveCompanyProfileRequest? Saved { get; private set; }

        public void Validate(Inspecta.Application.Contracts.Company.SaveCompanyProfileRequest request)
        {
            if (Fail) throw new ArgumentException("bad profile");
        }

        public Task<Inspecta.Application.Contracts.Company.CompanyProfileResponse> SaveAsync(
            Guid managerId, Inspecta.Application.Contracts.Company.SaveCompanyProfileRequest request, CancellationToken cancellationToken = default)
        {
            (SavedFor, Saved) = (managerId, request);
            return Task.FromResult<Inspecta.Application.Contracts.Company.CompanyProfileResponse>(null!);
        }

        public Task<Inspecta.Application.Contracts.Company.CompanyProfileResponse?> GetAsync(Guid managerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Inspecta.Application.Contracts.Company.CompanyProfileResponse?>(null);

        public Task<byte[]?> GetLogoAsync(Guid managerId, CancellationToken cancellationToken = default) => Task.FromResult<byte[]?>(null);
    }

    private sealed class FakePasswordService : IPasswordService
    {
        public string Hash(AppUser user, string password) => $"hashed:{password}";
        public bool Verify(AppUser user, string password) => user.PasswordHash == $"hashed:{password}";
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"encrypted:{plaintext}";
        public string Unprotect(string protectedValue) => protectedValue["encrypted:".Length..];
    }

    private sealed class MemoryUserRepository(params AppUser[] initialUsers) : IUserRepository
    {
        private readonly List<AppUser> _users = initialUsers.ToList();
        public IReadOnlyList<AppUser> Users => _users;
        public List<CoordinatorEmailConfig> EmailConfigs { get; } = [];

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.SingleOrDefault(user => user.Email == email));

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.SingleOrDefault(user => user.Id == id && !user.IsDeleted));

        public Task<IReadOnlyList<AppUser>> ListAsync(
            Guid? managerId = null,
            UserRole? role = null,
            bool? isActive = null,
            CancellationToken cancellationToken = default,
            bool includeDeleted = false)
        {
            IEnumerable<AppUser> query = includeDeleted ? _users : _users.Where(user => !user.IsDeleted);
            if (managerId is not null) query = query.Where(user => user.ManagerId == managerId);
            if (role is not null) query = query.Where(user => user.Role == role);
            if (isActive is not null) query = query.Where(user => user.IsActive == isActive);
            return Task.FromResult<IReadOnlyList<AppUser>>(query.ToArray());
        }

        public Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task AddTeamMemberAsync(
            AppUser user,
            CoordinatorEmailConfig? emailConfig,
            CancellationToken cancellationToken = default)
        {
            _users.Add(user);
            if (emailConfig is not null)
                EmailConfigs.Add(emailConfig);
            return Task.CompletedTask;
        }

        public Task SaveAsync(AppUser user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> SetActiveAsync(
            Guid userId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            var user = _users.SingleOrDefault(candidate => candidate.Id == userId && !candidate.IsDeleted);
            if (user is null) return Task.FromResult(false);
            if (isActive) user.Activate();
            else user.Deactivate();
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = _users.SingleOrDefault(candidate => candidate.Id == userId);
            if (user is null) return Task.FromResult(false);
            _users.Remove(user);
            return Task.FromResult(true);
        }

        public Task SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RotateRefreshTokenAsync(RefreshToken currentToken, RefreshToken nextToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeRefreshTokenFamilyAsync(Guid familyId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
