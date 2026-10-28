using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Mail;

/// <summary>Adds a sender whose emails become requests immediately.</summary>
public sealed record CreateClientMailRuleRequest
{
    /// <summary>An address (<c>buyer@alnoor.com</c>) or a whole domain (<c>alnoor.com</c> or <c>@alnoor.com</c>).</summary>
    [Required, MaxLength(320)]
    public required string Pattern { get; init; }

    /// <summary>Optional client record to attach to requests from this sender; must belong to the team.</summary>
    public Guid? ClientId { get; init; }

    /// <summary>Technical manager team; honored only for Admin requests.</summary>
    public Guid? ManagerId { get; init; }
}

/// <summary>A configured client sender of a team.</summary>
public sealed record ClientMailRuleResponse(
    Guid Id,
    Guid ManagerId,
    string Pattern,
    bool IsDomain,
    Guid? ClientId,
    DateTimeOffset CreatedAt);
