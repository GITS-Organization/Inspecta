using Inspecta.Application.Contracts.Timesheets;

namespace Inspecta.Application.Interfaces;

public interface ITimesheetService
{
    Task<TimesheetResponse?> GetAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<TimesheetResponse> SaveAsync(Guid requestId, Guid actorId, SaveTimesheetRequest request, CancellationToken cancellationToken = default);
    Task<TimesheetResponse> ReviewAsync(Guid requestId, Guid actorId, ReviewTimesheetRequest request, CancellationToken cancellationToken = default);
}