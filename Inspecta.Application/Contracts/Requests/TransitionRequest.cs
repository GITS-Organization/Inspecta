using System.ComponentModel.DataAnnotations;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>Payload for a workflow status transition.</summary>
public sealed record TransitionRequest
{
    public required JobStatus Status { get; init; }

    [MaxLength(2000)]
    public string? Note { get; init; }
}