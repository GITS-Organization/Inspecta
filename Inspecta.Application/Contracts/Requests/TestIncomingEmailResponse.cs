namespace Inspecta.Application.Contracts.Requests;

/// <summary>Generated email text and the inspection request created from the simulated email.</summary>
public sealed record TestIncomingEmailResponse(
    string EmailBody,
    RequestResponse Request);
