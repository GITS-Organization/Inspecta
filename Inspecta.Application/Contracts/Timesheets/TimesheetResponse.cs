using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Timesheets;

/// <summary>Inspector time and expenses, calculated price, and review status.</summary>
public sealed record TimesheetResponse(
    Guid RequestId,
    Guid InspectorId,
    IReadOnlyDictionary<string, int> Activities,
    long HourlyRatePiastres,
    long PricePiastres,
    IReadOnlyDictionary<string, long> Expenses,
    long ExpensesPiastres,
    long TotalPiastres,
    TimesheetStatus Status,
    string? ReviewNote,
    DateTimeOffset? ReviewedAt);