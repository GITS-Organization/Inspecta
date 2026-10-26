using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class UserManagementTests
{
    [Fact]
    public async Task TechnicalManagerCanOnlySeeTeamMembersInTheirCompany()
    {
        var company = Company.Create("Company");
        var otherCompany = Company.Create("Other Company");
        var companyId = company.Id;
        var otherCompanyId = otherCompany.Id;
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager, companyId: companyId);
        var inScope = AppUser.Create("inspector@example.com", "Inspector", UserRole.Inspector, companyId: companyId);
        var outOfScope = AppUser.Create("other@example.com", "Other", UserRole.Coordinator, companyId: otherCompanyId);
        var users = new MemoryUserRepository(manager, inScope, outOfScope);
        var service = CreateService(users, new MemoryCompanyRepository(company, otherCompany));

        var team = await service.ListTeamAsync(manager.Id);
        var otherMember = await service.GetTeamMemberAsync(manager.Id, outOfScope.Id);

        Assert.Equal([inScope.Id], team.Select(user => user.Id));
        Assert.Null(otherMember);
    }

    [Fact]
    public async Task TechnicalManagerCannotCreatePrivilegedRoles()
    {
        var company = Company.Create("Company");
        var companyId = company.Id;
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager, companyId: companyId);
        var service = CreateService(new MemoryUserRepository(manager), new MemoryCompanyRepository(company));

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
    public async Task TechnicalManagerCreatesAllowedRolesInsideTheirCompany()
    {
        var company = Company.Create("Company");
        var companyId = company.Id;
        var manager = AppUser.Create("manager@example.com", "Manager", UserRole.TechnicalManager, companyId: companyId);
        var users = new MemoryUserRepository(manager);
        var service = CreateService(users, new MemoryCompanyRepository(company));

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
        Assert.Equal(companyId, created.CompanyId);
        Assert.Equal(manager.Id, created.CreatedById);
    }

    [Fact]
    public async Task DeletedUsersAreSoftDeletedAndExcludedFromNormalQueries()
    {
        var user = AppUser.Create("staff@example.com", "Staff Member", UserRole.Inspector);
        var users = new MemoryUserRepository(user);
        var service = CreateService(users, new MemoryCompanyRepository());

        await service.DeactivateAsync(user.Id);

        Assert.True(user.IsDeleted);
        Assert.False(user.IsActive);
        Assert.NotNull(user.DeletedAt);
        Assert.Null(await users.GetByIdAsync(user.Id));
        Assert.Single(await users.ListAsync(includeDeleted: true));
    }

    [Fact]
    public async Task ManagerCanSuspendOnlyMembersInsideTheirOwnCompany()
    {
        var company = Company.Create("Company");
        var otherCompany = Company.Create("Other Company");
        var manager = AppUser.Create(
            "manager@example.com",
            "Manager",
            UserRole.TechnicalManager,
            companyId: company.Id);
        var member = AppUser.Create(
            "member@example.com",
            "Member",
            UserRole.Inspector,
            companyId: company.Id);
        var outOfScope = AppUser.Create(
            "other@example.com",
            "Other Member",
            UserRole.Inspector,
            companyId: otherCompany.Id);
        var users = new MemoryUserRepository(manager, member, outOfScope);
        var service = CreateService(users, new MemoryCompanyRepository(company, otherCompany));

        await service.SetTeamMemberActiveAsync(manager.Id, member.Id, isActive: false);

        var error = await Assert.ThrowsAsync<AuthException>(() =>
            service.SetTeamMemberActiveAsync(manager.Id, outOfScope.Id, isActive: false));
        Assert.False(member.IsActive);
        Assert.Equal("user_not_found", error.Code);
        Assert.True(outOfScope.IsActive);
    }

    [Fact]
    public async Task SuspendedOrExpiredCompanyCannotAuthorizeManagerOperations()
    {
        var company = Company.Create("Test Company");
        var manager = AppUser.Create(
            "manager@example.com",
            "Manager",
            UserRole.TechnicalManager,
            companyId: company.Id);
        var companies = new MemoryCompanyRepository(company);
        var service = CreateService(new MemoryUserRepository(manager), companies);

        company.UpdateSubscription(SubscriptionStatus.Suspended, DateTimeOffset.UtcNow.AddDays(10));

        var suspended = await Assert.ThrowsAsync<AuthException>(() => service.ListTeamAsync(manager.Id));
        company.UpdateSubscription(SubscriptionStatus.Active, DateTimeOffset.UtcNow.AddSeconds(-1));
        var expired = await Assert.ThrowsAsync<AuthException>(() => service.ListTeamAsync(manager.Id));

        Assert.Equal("subscription_inactive", suspended.Code);
        Assert.Equal("subscription_inactive", expired.Code);
    }

    [Fact]
    public async Task CompanyDeletionIsSoftAndSubscriptionCanBeUpdated()
    {
        var company = Company.Create("Test Company");
        var companies = new MemoryCompanyRepository(company);
        var service = CreateService(new MemoryUserRepository(), companies);

        var updated = await service.UpdateCompanySubscriptionAsync(company.Id, new UpdateCompanySubscriptionRequest
        {
            SubscriptionStatus = SubscriptionStatus.Suspended,
            SubscriptionEndDate = DateTimeOffset.UtcNow.AddMonths(1)
        });

        await service.DeleteCompanyAsync(company.Id);

        Assert.Equal(SubscriptionStatus.Suspended, updated.SubscriptionStatus);
        Assert.True(company.IsDeleted);
        Assert.NotNull(company.DeletedAt);
        Assert.Single(await service.ListCompaniesAsync(includeDeleted: true));
    }

    [Fact]
    public async Task NewLocalUsersRequireACompanyUnlessTheyAreAdmins()
    {
        var company = Company.Create("Company");
        var companyId = company.Id;
        var users = new MemoryUserRepository();
        var service = CreateService(users, new MemoryCompanyRepository(company));

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
            CompanyId = companyId
        });

        Assert.Equal("company_required", error.Code);
        Assert.Equal(companyId, created.CompanyId);
    }

    private static UserManagementService CreateService(
        MemoryUserRepository users,
        MemoryCompanyRepository companies) =>
        new(users, companies, new FakePasswordService());

    private sealed class FakePasswordService : IPasswordService
    {
        public string Hash(AppUser user, string password) => $"hashed:{password}";
        public bool Verify(AppUser user, string password) => user.PasswordHash == $"hashed:{password}";
    }

    private sealed class MemoryCompanyRepository : ICompanyRepository
    {
        private readonly List<Company> _companies = [];

        public MemoryCompanyRepository(params Company[] companies) => _companies.AddRange(companies);

        public Task<IReadOnlyList<Company>> ListAsync(
            CancellationToken cancellationToken = default,
            bool includeDeleted = false) =>
            Task.FromResult<IReadOnlyList<Company>>(
                _companies.Where(company => includeDeleted || !company.IsDeleted).ToArray());

        public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_companies.SingleOrDefault(company => company.Id == companyId && !company.IsDeleted));

        public Task<bool> ExistsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_companies.Any(company => company.Id == companyId && company.HasActiveSubscription()));

        public Task AddWithManagerAsync(Company company, AppUser manager, CancellationToken cancellationToken = default)
        {
            _companies.Add(company);
            return Task.CompletedTask;
        }

        public Task SaveAsync(Company company, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> SoftDeleteAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            var company = _companies.SingleOrDefault(candidate => candidate.Id == companyId && !candidate.IsDeleted);
            if (company is null) return Task.FromResult(false);
            company.SoftDelete();
            return Task.FromResult(true);
        }

    }

    private sealed class MemoryUserRepository(params AppUser[] initialUsers) : IUserRepository
    {
        private readonly List<AppUser> _users = initialUsers.ToList();

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.SingleOrDefault(user => user.Email == email));

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.SingleOrDefault(user => user.Id == id && !user.IsDeleted));

        public Task<IReadOnlyList<AppUser>> ListAsync(
            Guid? companyId = null,
            UserRole? role = null,
            bool? isActive = null,
            CancellationToken cancellationToken = default,
            bool includeDeleted = false)
        {
            IEnumerable<AppUser> query = includeDeleted ? _users : _users.Where(user => !user.IsDeleted);
            if (companyId is not null) query = query.Where(user => user.CompanyId == companyId);
            if (role is not null) query = query.Where(user => user.Role == role);
            if (isActive is not null) query = query.Where(user => user.IsActive == isActive);
            return Task.FromResult<IReadOnlyList<AppUser>>(query.ToArray());
        }

        public Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
        {
            _users.Add(user);
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

        public Task<bool> SoftDeleteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = _users.SingleOrDefault(candidate => candidate.Id == userId);
            if (user is null || user.IsDeleted) return Task.FromResult(false);
            user.SoftDelete();
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
