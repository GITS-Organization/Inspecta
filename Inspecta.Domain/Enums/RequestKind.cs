namespace Inspecta.Domain.Enums;

/// <summary>What the request is for.</summary>
public enum RequestKind
{
    /// <summary>The usual request: machines to inspect, a quotation, then the inspection.</summary>
    Standard,

    /// <summary>A day of an inspector's time that a client asked for: no machines and no quotation. The coordinator assigns an inspector,
    /// who adds the machines found on site and certifies them.</summary>
    ManDay,

    /// <summary>A certificate created directly by the person who inspects, with no request from the client and no quotation.</summary>
    Direct,

    /// <summary>A request of a client with a contract that was given to an inspector straight away, with no quotation.</summary>
    Contract
}
