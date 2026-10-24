using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Timesheets;

/// <summary>Inspector time and expense amounts in minutes and piastres.</summary>
public sealed record SaveTimesheetRequest
{
    public IReadOnlyDictionary<string, int> Activities { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, long> Expenses { get; init; } = new Dictionary<string, long>();
}

/// <summary>Coordinator approval or return decision for a timesheet.</summary>
public sealed record ReviewTimesheetRequest
{
    public required bool Approve { get; init; }

    [MaxLength(4000)]
    public string? Note { get; init; }
}