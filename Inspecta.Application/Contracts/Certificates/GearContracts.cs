using System.ComponentModel.DataAnnotations;
using Inspecta.Application.Contracts.Requests;

namespace Inspecta.Application.Contracts.Certificates;

/// <summary>
/// Divides one machine over several certificates: the serial numbers (one per entry) are shared out in order, so each certificate
/// lists its own. <c>certificates</c> is how many to make; leave it out for the fewest that fit.
/// </summary>
public sealed record SplitMachineRequest
{
    [Required]
    public IReadOnlyList<string> SerialNumbers { get; init; } = [];

    [Range(1, 5000)]
    public int? Certificates { get; init; }
}

/// <summary>Creates a certificate straight away, without a request or a quotation: the client and the machines to inspect.</summary>
public sealed record CreateCertificationRequest
{
    /// <summary>Technical manager owner; honored only for Admin.</summary>
    public Guid? ManagerId { get; init; }

    [Required]
    public Guid ClientId { get; init; }

    /// <summary>Where the inspection takes place; the client's location when left out.</summary>
    [MaxLength(500)]
    public string? Location { get; init; }

    /// <summary>The machines: a registered one by <c>equipmentId</c>, or a new one by its details (registered for the client, except gears).</summary>
    public IReadOnlyList<CreateRequestItem> Machines { get; init; } = [];
}

/// <summary>A client a certificate can be created for: for an inspector, only the clients whose jobs are with them now.</summary>
public sealed record CertificationClientResponse(Guid Id, string Name, string? Location);

/// <summary>One kind of machine the catalogue knows, so the screen can offer it when a new machine is added.</summary>
public sealed record MachineTypeResponse(
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Standards,
    string TemplateKey,
    string TemplateName,
    bool IsGear);

/// <summary>The most a printed certificate takes, so the screen can plan a split before it asks for one.</summary>
public sealed record SerialLimitsResponse(int MaxCharactersPerCertificate, int MaxLengthPerSerialNumber, int MaxSerialNumbers);
