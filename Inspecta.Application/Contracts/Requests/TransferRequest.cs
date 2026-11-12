using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>Hands a request to another coordinator of the same team.</summary>
public sealed record TransferRequest(
    Guid CoordinatorId,
    [MaxLength(500)] string? Note = null);

/// <summary>A coordinator a request can be handed to.</summary>
public sealed record TeamCoordinatorResponse(Guid Id, string FullName, string Email);
