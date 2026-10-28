namespace Inspecta.Application.Contracts.Email;

public sealed record ParsedIncomingEmail(
    string? ClientName,
    string? Location,
    string? MachineDetails);
