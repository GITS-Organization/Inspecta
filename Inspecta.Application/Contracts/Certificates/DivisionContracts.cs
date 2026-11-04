namespace Inspecta.Application.Contracts.Certificates;

/// <summary>One certificate of a divided gear: its own quantity and its own serial numbers, written as one free text.</summary>
public sealed record CertificateShare
{
    /// <summary>How many pieces this certificate covers; at least 1.</summary>
    public int Quantity { get; init; }

    /// <summary>The serial numbers exactly as they are printed (for example <c>CS-0001-CS-0150, CS-0300</c>); at most 1000 characters so the certificate stays one page.</summary>
    public string SerialNumbers { get; init; } = string.Empty;
}

/// <summary>The certificates a gear is divided into, as the inspector decided: as many as he wants, no maximum.</summary>
public sealed record DivideMachineRequest
{
    public IReadOnlyList<CertificateShare> Certificates { get; init; } = [];
}
