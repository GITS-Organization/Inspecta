using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>Why the request is turned down. No email is sent to the client.</summary>
public sealed record RejectRequestRequest
{
    [Required, MaxLength(2000)]
    public required string Reason { get; init; }
}
