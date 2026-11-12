using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Contracts.Companies;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class CompanyService(
    ICompanyRepository companies,
    IUserRepository users,
    IPasswordService passwords,
    IReviewerService reviewers,
    ICompanyProfileService? profiles = null,
    ITransactionRunner? transactions = null) : ICompanyService
{
    public async Task<IReadOnlyList<CompanyResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = await companies.ListAsync(cancellationToken);
        var names = await users.GetFullNamesAsync(all.Select(company => company.DefaultReviewerId).OfType<Guid>().Distinct().ToArray(), cancellationToken);
        return all.Select(company => Map(company, company.DefaultReviewerId is { } id && names.TryGetValue(id, out var name) ? name : null)).ToArray();
    }

    public async Task<CompanyResponse?> GetAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await companies.GetByIdAsync(companyId, tracking: false, cancellationToken) is { } company
            ? await MapAsync(company, cancellationToken)
            : null;

    public async Task<CreatedCompanyResponse> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default)
    {
        UserManagementService.ValidatePassword(request.AdminPassword);
        if (request.SubscriptionMonths is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(request.SubscriptionMonths));
        // The identity is checked first: a mistake in it must not leave a company without it.
        if (request.CompanyProfile is not null)
        {
            if (profiles is null) throw new InvalidOperationException("Company profiles are not available.");
            profiles.Validate(request.CompanyProfile);
        }

        var email = request.AdminEmail.Trim().ToLowerInvariant();
        if (await users.GetByEmailAsync(email, cancellationToken) is not null)
            throw new AuthException("account_exists", "Email is already registered.", 409);

        var company = Company.Create(request.Name, DateTimeOffset.UtcNow.AddMonths(request.SubscriptionMonths));
        var admin = AppUser.Create(email, request.AdminFullName, UserRole.CompanyAdmin, managerId: company.Id);
        admin.SetPasswordHash(passwords.Hash(admin, request.AdminPassword));

        async Task<bool> Save()
        {
            await companies.AddAsync(company, cancellationToken);
            await users.AddAsync(admin, cancellationToken);
            if (request.CompanyProfile is not null)
                await profiles!.SaveAsync(company.Id, request.CompanyProfile, cancellationToken);
            return true;
        }

        if (transactions is null) await Save();
        else await transactions.RunAsync(Save, cancellationToken);
        return new CreatedCompanyResponse(Map(company, null), ToResponse(admin, company));
    }

    public async Task<CompanyResponse> RenameAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        var company = await LoadAsync(companyId, cancellationToken);
        company.Rename(name);
        await companies.SaveAsync(cancellationToken);
        return await MapAsync(company, cancellationToken);
    }

    public async Task<CompanyResponse> SetStatusAsync(Guid companyId, bool isActive, CancellationToken cancellationToken = default)
    {
        var company = await LoadAsync(companyId, cancellationToken);
        if (isActive) company.Activate();
        else company.Deactivate();
        await companies.SaveAsync(cancellationToken);
        return await MapAsync(company, cancellationToken);
    }

    public async Task<CompanyResponse> UpdateSubscriptionAsync(
        Guid companyId, UpdateCompanySubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.SubscriptionStatus))
            throw new ArgumentException("Subscription status is invalid.", nameof(request));
        var company = await LoadAsync(companyId, cancellationToken);
        company.UpdateSubscription(request.SubscriptionStatus, request.SubscriptionEndDate);
        await companies.SaveAsync(cancellationToken);
        return await MapAsync(company, cancellationToken);
    }

    public async Task<CompanyResponse> SetDefaultReviewerAsync(Guid companyId, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var company = await LoadAsync(companyId, cancellationToken);
        var reviewer = await reviewers.RequireReviewerAsync(companyId, reviewerId, cancellationToken);
        company.SetDefaultReviewer(reviewer.Id);
        await companies.SaveAsync(cancellationToken);
        return Map(company, reviewer.FullName);
    }

    private async Task<Company> LoadAsync(Guid companyId, CancellationToken cancellationToken) =>
        await companies.GetByIdAsync(companyId, tracking: true, cancellationToken)
        ?? throw new KeyNotFoundException("Company was not found.");

    private async Task<CompanyResponse> MapAsync(Company company, CancellationToken cancellationToken)
    {
        var name = company.DefaultReviewerId is { } reviewerId
            ? (await users.GetFullNamesAsync([reviewerId], cancellationToken)).GetValueOrDefault(reviewerId)
            : null;
        return Map(company, name);
    }

    private static CompanyResponse Map(Company company, string? reviewerName) => new(
        company.Id,
        company.Name,
        company.IsActive,
        company.GetEffectiveSubscriptionStatus(),
        company.SubscriptionEndDate,
        company.DefaultReviewerId,
        reviewerName,
        company.CreatedAt);

    private static AuthUserResponse ToResponse(AppUser user, Company company) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.Role,
        user.IsActive,
        [],
        user.IsOnLeave,
        user.LeaveUntil,
        user.ManagerId,
        user.CreatedById,
        company.GetEffectiveSubscriptionStatus(),
        company.SubscriptionEndDate,
        Authorization.RolePermissions.For(Authorization.RoleNames.Of(user.Role)));
}
