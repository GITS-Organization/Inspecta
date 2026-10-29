namespace Inspecta.Application.Contracts.Requests;

/// <summary>An inspector of the team that a job can be given to, with how much they already have.</summary>
/// <param name="ActiveJobs">Jobs assigned to them that are not finished yet (assigned up to certificate returned).</param>
public sealed record InspectorResponse(
    Guid Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Qualifications,
    bool IsOnLeave,
    DateTimeOffset? LeaveUntil,
    int ActiveJobs);
