using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfTimesheetRepository(InspectaDbContext dbContext) : ITimesheetRepository
{
    public Task<TimesheetEntry?> GetByRequestIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default)
    {
        IQueryable<TimesheetEntry> query = dbContext.Timesheets;
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(timesheet => timesheet.RequestId == requestId, cancellationToken);
    }

    public async Task AddAsync(TimesheetEntry timesheet, CancellationToken cancellationToken = default)
    {
        dbContext.Timesheets.Add(timesheet);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}