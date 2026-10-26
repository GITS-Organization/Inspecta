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
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var manager = AppUser.Create(null, "manager@example.com", "Manager", UserRole.TechnicalManager, companyId: companyId);
        var inScope = AppUser.Create(null, "inspector@example.com", "Inspector", UserRole.Inspector, companyId: companyId);
        var outOfScope = AppUser.Create(null, "other@example.com", "Other", UserRole.Coordinator, companyId: otherCompanyId);
        var users = new MemoryUserRepository(manager, inScope, outOfScope);
        var service = CreateService(users, new MemoryCompanyRepository(companyId, otherCompanyId));

        var team = await service.ListTeamAsync(manager.Id);
        var otherMember = await service.GetTeamMemberAsync(manager.Id, outOfScope.Id);

        Assert.Equal([inScope.Id], team.Select(user => user.Id));
        Assert.Null(otherMember);
    }

    [Fact]
    public async Task TechnicalManagerCannotCreatePrivilegedRoles()
    {
        var companyId = Guid.NewGuid();
        var manager = AppUser.Create(null, "manager@example.com", "Manager", UserRole.TechnicalManager, companyId: companyId);
        var service = CreateService(new MemoryUserRepository(manager), new MemoryCompanyRepository(companyId));

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
        var companyId = Guid.NewGuid();
        var manager = AppUser.Create(null, "manager@example.com", "Manager", UserRole.TechnicalManager, companyId: companyId);
        var users = new MemoryUserRepository(manager);
        var service = CreateService(users, new MemoryCompanyRepository(companyId));

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
        Assert.Null(created.FirebaseUid);
    }

    [Fact]
    public async Task NewLocalUsersCanBeCreatedWithoutFirebaseUidButNeedACompanyUnlessAdmin()
    {
        var companyId = Guid.NewGuid();
        var users = new MemoryUserRepository();
        var service = CreateService(users, new MemoryCompanyRepository(companyId));

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
        Assert.Null(created.FirebaseUid);
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

    private sealed class MemoryCompanyRepository(params Guid[] companyIds) : ICompanyRepository
    {
        private readonly HashSet<Guid> _companyIds = companyIds.ToHashSet();
        private readonly List<Company> _companies = companyIds
            .Select(id => Company.Create($"Company-{id}"))
            .ToList();

        public Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Company>>(_companies);

        public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_companies.SingleOrDefault(company => company.Id == companyId));

        public Task<bool> ExistsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_companyIds.Contains(companyId));

        public Task AddWithManagerAsync(Company company, AppUser manager, CancellationToken cancellationToken = default)
        {
            _companies.Add(company);
            _companyIds.Add(company.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryUserRepository(params AppUser[] initialUsers) : IUserRepository
    {
        private readonly List<AppUser> _users = initialUsers.ToList();

        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.SingleOrDefault(user => user.Email == email));

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.SingleOrDefault(user => user.Id == id));

        public Task<AppUser?> GetByFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.SingleOrDefault(user => user.FirebaseUid == firebaseUid));

        public Task<IReadOnlyList<AppUser>> ListAsync(
            Guid? companyId = null,
            UserRole? role = null,
            bool? isActive = null,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<AppUser> query = _users;
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

        public Task<bool> DeactivateAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = _users.SingleOrDefault(candidate => candidate.Id == userId);
            if (user is null || !user.IsActive) return Task.FromResult(false);
            user.Deactivate();
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
