namespace Inspecta.Application.Contracts.Auth;

/// <summary>What the signed-in user prints under their name in the cover letter of the quotations they prepare. An empty value clears it.</summary>
public sealed record UpdateMySignatureRequest
{
    /// <summary>For example <c>Senior Coordinator</c>; at most 100 characters. Empty prints "Coordinator".</summary>
    public string? JobTitle { get; init; }

    /// <summary>At most 40 characters. Empty prints no phone line.</summary>
    public string? Phone { get; init; }
}
