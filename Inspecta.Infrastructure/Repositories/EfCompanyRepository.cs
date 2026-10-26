using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCompanyRepository(InspectaDbContext dbContext) : ICompanyRepository
{
    public async Task<IReadOnlyList<Company>> ListAsync(
        CancellationToken cancellationToken = default,
        bool includeDeleted = false)
    {
        var query = includeDeleted
            ? dbContext.Companies.IgnoreQueryFilters().AsNoTracking()
            : dbContext.Companies.AsNoTracking();
        return await query.OrderBy(company => company.Name).ToListAsync(cancellationToken);
    }

    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.Companies.SingleOrDefaultAsync(company => company.Id == companyId, cancellationToken);

    public Task<bool> ExistsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.Companies.AnyAsync(
            company => company.Id == companyId &&
                company.SubscriptionStatus == Inspecta.Domain.Enums.SubscriptionStatus.Active &&
                (company.SubscriptionEndDate == null || company.SubscriptionEndDate > DateTimeOffset.UtcNow),
            cancellationToken);

    public async Task AddWithManagerAsync(
        Company company,
        AppUser manager,
        CancellationToken cancellationToken = default)
    {
        dbContext.Companies.Add(company);
        dbContext.Users.Add(manager);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(Company company, CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(company).State == EntityState.Detached)
            dbContext.Companies.Update(company);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SoftDeleteAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var company = await dbContext.Companies.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null || company.IsDeleted)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        company.SoftDelete();
        var companyUserIds = dbContext.Users.IgnoreQueryFilters()
            .Where(user => user.CompanyId == companyId)
            .Select(user => user.Id);
        await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Where(token => companyUserIds.Contains(token.UserId) && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                update => update.SetProperty(token => token.RevokedAt, DateTimeOffset.UtcNow),
                cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
