using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>A technical manager a job can be given to for review.</summary>
/// <param name="IsDefault">True for the company's default reviewer: the one every new request starts with.</param>
public sealed record ReviewerResponse(Guid Id, string FullName, string Email, bool IsDefault);

/// <summary>Gives a job to another reviewer.</summary>
public sealed record ChangeReviewerRequest(Guid ReviewerId, [MaxLength(500)] string? Note = null);
