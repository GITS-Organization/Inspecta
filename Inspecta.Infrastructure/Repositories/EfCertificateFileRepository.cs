using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCertificateFileRepository(InspectaDbContext dbContext) : ICertificateFileRepository
{
    private const int NumberAttempts = 5;

    public async Task AddAsync(IssuedCertificateFile file, CancellationToken cancellationToken = default)
    {
        dbContext.IssuedCertificateFiles.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IssuedCertificateFile?> GetByCertificateIdAsync(Guid certificateId, CancellationToken cancellationToken = default) =>
        dbContext.IssuedCertificateFiles.AsNoTracking()
            .SingleOrDefaultAsync(file => file.CertificateId == certificateId, cancellationToken);

    public async Task<IReadOnlyList<IssuedCertificateFile>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        await dbContext.IssuedCertificateFiles.AsNoTracking()
            .Where(file => file.RequestId == requestId)
            .OrderBy(file => file.CertificateNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IssuedCertificateSummary>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var rows = await Summaries(dbContext.IssuedCertificateFiles.AsNoTracking().Where(file => file.ClientId == clientId))
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(row => row.IssuedAt).ToArray();
    }

    public Task<IssuedCertificateFile?> GetForClientAsync(Guid clientId, Guid fileId, CancellationToken cancellationToken = default) =>
        dbContext.IssuedCertificateFiles.AsNoTracking()
            .SingleOrDefaultAsync(file => file.ClientId == clientId && file.Id == fileId, cancellationToken);

    public async Task<IssuedCertificateSummary?> FindByVerificationCodeAsync(string verificationCode, CancellationToken cancellationToken = default) =>
        await Summaries(dbContext.IssuedCertificateFiles.IgnoreQueryFilters().AsNoTracking()
                .Where(file => file.VerificationCode == verificationCode))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IssuedCertificateSummary?> FindLatestForEquipmentAsync(
        Guid equipmentId, Guid excludingCertificateId, CancellationToken cancellationToken = default)
    {
        // Ordered in memory: SQLite cannot order by a DateTimeOffset, and a machine has few certificates.
        var rows = await Summaries(dbContext.IssuedCertificateFiles.AsNoTracking()
                .Where(file => file.EquipmentId == equipmentId && file.CertificateId != excludingCertificateId))
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(row => row.IssuedAt).FirstOrDefault();
    }

    private static IQueryable<IssuedCertificateSummary> Summaries(IQueryable<IssuedCertificateFile> files) =>
        files.Select(file => new IssuedCertificateSummary(
            file.Id, file.CertificateId, file.RequestId, file.ClientId, file.ClientName, file.ItemName,
            file.CertificateNumber, file.VerificationCode, file.ExaminationDate, file.NextExaminationDate,
            file.FileName, file.Sha256, file.IssuedAt, file.EquipmentId, file.QrData, file.PreparedBy));

    public async Task<string> NextCertificateNumberAsync(Guid managerId, int year, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var counter = await dbContext.CertificateCounters
                .SingleOrDefaultAsync(value => value.ManagerId == managerId && value.Year == year, cancellationToken);
            if (counter is null)
            {
                counter = CertificateCounter.Create(managerId, year);
                dbContext.CertificateCounters.Add(counter);
            }

            var number = counter.Increment();
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return CertificateCounter.Format(year, number);
            }
            catch (DbUpdateException) when (attempt < NumberAttempts)
            {
                // Another approval took the number first (or created the year's counter): look again.
                foreach (var entry in dbContext.ChangeTracker.Entries<CertificateCounter>().ToList())
                    entry.State = EntityState.Detached;
            }
        }
    }
}

public sealed class EfCertificateDeliveryRepository(InspectaDbContext dbContext) : ICertificateDeliveryRepository
{
    public Task<CertificateDelivery?> GetSentAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        dbContext.CertificateDeliveries.FirstOrDefaultAsync(value => value.RequestId == requestId && value.Sent, cancellationToken);

    public Task<CertificateDelivery?> GetPendingAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        dbContext.CertificateDeliveries.FirstOrDefaultAsync(value => value.RequestId == requestId && !value.Sent, cancellationToken);

    public async Task AddAsync(CertificateDelivery delivery, CancellationToken cancellationToken = default)
    {
        dbContext.CertificateDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
