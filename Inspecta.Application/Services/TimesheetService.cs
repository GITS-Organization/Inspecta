using System.Text.Json;
using Inspecta.Application.Contracts.Timesheets;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class TimesheetService(
    ITimesheetRepository timesheetRepository,
    IRequestRepository requestRepository,
    IUserRepository userRepository) : ITimesheetService
{
    public async Task<TimesheetResponse?> GetAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !CanAccess(request, actorId, role)) return null;
        var timesheet = await timesheetRepository.GetByRequestIdAsync(requestId, tracking: false, cancellationToken);
        return timesheet is null ? null : Map(timesheet);
    }

    public async Task<TimesheetResponse> SaveAsync(Guid requestId, Guid actorId, SaveTimesheetRequest input, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        if (request.InspectorId != actorId)
            throw new Inspecta.Application.Exceptions.AuthException("forbidden", "Only the assigned inspector can edit this timesheet.", 403);
        if (request.Status is not (JobStatus.CertificateApproved or JobStatus.SentToClient))
            throw new InvalidOperationException("Timesheets can only be submitted after certificate approval.");
        var inspector = await userRepository.GetByIdAsync(actorId, cancellationToken)
            ?? throw new Inspecta.Application.Exceptions.AuthException("account_disabled", "Inspector account is unavailable.", 403);
        if (!inspector.IsActive || inspector.Role != UserRole.Inspector)
            throw new Inspecta.Application.Exceptions.AuthException("forbidden", "Only an active inspector can edit a timesheet.", 403);

        var timesheet = await timesheetRepository.GetByRequestIdAsync(requestId, tracking: true, cancellationToken);
        if (timesheet is null)
        {
            timesheet = TimesheetEntry.Priced(requestId, actorId, input.Activities, input.Expenses, inspector.HourlyRatePiastres);
            await timesheetRepository.AddAsync(timesheet, cancellationToken);
        }
        else
        {
            if (timesheet.InspectorId != actorId)
                throw new Inspecta.Application.Exceptions.AuthException("forbidden", "Timesheet belongs to another inspector.", 403);
            timesheet.Update(input.Activities, input.Expenses, inspector.HourlyRatePiastres);
            await timesheetRepository.SaveAsync(cancellationToken);
        }
        return Map(timesheet);
    }

    public async Task<TimesheetResponse> ReviewAsync(Guid requestId, Guid actorId, ReviewTimesheetRequest review, CancellationToken cancellationToken = default)
    {
        var timesheet = await timesheetRepository.GetByRequestIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Timesheet was not found.");
        if (review.Approve) timesheet.Approve(actorId);
        else timesheet.Return(actorId, review.Note ?? string.Empty);
        await timesheetRepository.SaveAsync(cancellationToken);
        return Map(timesheet);
    }

    private static bool CanAccess(InspectionRequest request, Guid actorId, string role) =>
        role is "admin" or "coordinator" || (role == "inspector" && request.InspectorId == actorId);

    private static TimesheetResponse Map(TimesheetEntry timesheet)
    {
        var activities = JsonSerializer.Deserialize<Dictionary<string, int>>(timesheet.ActivitiesJson) ?? [];
        var expenses = JsonSerializer.Deserialize<Dictionary<string, long>>(timesheet.ExpensesJson) ?? [];
        return new TimesheetResponse(timesheet.RequestId, timesheet.InspectorId, activities, timesheet.HourlyRatePiastres,
            timesheet.PricePiastres, expenses, timesheet.ExpensesPiastres, timesheet.TotalPiastres, timesheet.Status,
            timesheet.ReviewNote, timesheet.ReviewedAt);
    }
}