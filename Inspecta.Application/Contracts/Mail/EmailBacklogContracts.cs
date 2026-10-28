using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Mail;

/// <summary>Removes email requests nobody has started working on, such as a backlog imported by mistake.</summary>
public sealed record CleanupEmailBacklogRequest
{
    /// <summary>Only requests imported before this moment are considered.</summary>
    [Required]
    public required DateTimeOffset CreatedBefore { get; init; }

    /// <summary>Limit the cleanup to one technical manager team.</summary>
    public Guid? ManagerId { get; init; }

    /// <summary>When true (the default) nothing is deleted; the response only reports how many would be.</summary>
    public bool DryRun { get; init; } = true;
}

/// <summary>How many untouched email requests matched and how many were deleted.</summary>
public sealed record CleanupEmailBacklogResponse(int Matched, int Deleted, bool DryRun);
