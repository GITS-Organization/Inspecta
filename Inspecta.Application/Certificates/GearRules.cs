namespace Inspecta.Application.Certificates;

/// <summary>Only gears (slings, shackles, hooks...) are counted in numbers; every other machine is one unit.</summary>
public static class GearRules
{
    /// <summary>
    /// The quantity to store for a machine: what was asked for when the catalogue marks the name as a gear (an invalid value is left for the
    /// item to refuse), else 1. A name the catalogue does not know is not a gear.
    /// </summary>
    public static int QuantityFor(string? machineName, int requested) =>
        !string.IsNullOrWhiteSpace(machineName) && CertificateTemplateCatalog.Resolve(machineName).Item?.Gear == true
            ? requested
            : 1;
}
