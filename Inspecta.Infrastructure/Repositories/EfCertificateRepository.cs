using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCertificateRepository(InspectaDbContext dbContext) : ICertificateRepository
{
    public async Task<IReadOnlyList<InspectionCertificate>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        await dbContext.Certificates.AsNoTracking()
            .Where(certificate => certificate.RequestId == requestId)
            .OrderBy(certificate => certificate.RequestItemId)
            .ToListAsync(cancellationToken);

    public Task<InspectionCertificate?> GetByRequestItemIdAsync(
        Guid requestId,
        Guid requestItemId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        IQueryable<InspectionCertificate> query = dbContext.Certificates;
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(
            certificate => certificate.RequestId == requestId && certificate.RequestItemId == requestItemId,
            cancellationToken);
    }

    public async Task AddAsync(InspectionCertificate certificate, CancellationToken cancellationToken = default)
    {
        dbContext.Certificates.Add(certificate);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}