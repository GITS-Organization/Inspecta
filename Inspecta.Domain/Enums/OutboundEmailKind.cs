namespace Inspecta.Domain.Enums;

/// <summary>Which quotation reply an outbound email carries.</summary>
public enum OutboundEmailKind
{
    /// <summary>The price offer (a first offer or a counter offer revision).</summary>
    Offer = 1,

    /// <summary>The acceptance of the request.</summary>
    Accept = 2
}
