using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ITimesheetRepository
{
    Task<TimesheetEntry?> GetByRequestIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default);
    Task AddAsync(TimesheetEntry timesheet, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}