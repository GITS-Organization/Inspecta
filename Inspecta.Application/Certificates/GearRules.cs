namespace Inspecta.Application.Certificates;

/// <summary>Only gears (slings, shackles, hooks...) are counted in numbers; every other machine is one unit.</summary>
public static class GearRules
{
    /// <summary>True when the catalogue marks the name as a gear.</summary>
    public static bool IsGear(string? machineName) =>
        !string.IsNullOrWhiteSpace(machineName) && CertificateTemplateCatalog.Resolve(machineName).Item?.Gear == true;

    /// <summary>
    /// The serial number to keep with a registered item: none for a gear. Gears are replaced every six months, so the client's list does not
    /// keep their serial numbers; they are written on the certificates when the inspector divides the gear.
    /// </summary>
    public static string? SerialFor(string? machineName, string? serialNumber) => IsGear(machineName) ? null : serialNumber;

    /// <summary>
    /// The quantity to store for a machine: what was asked for when the catalogue marks the name as a gear (an invalid value is left for the
    /// item to refuse), else 1. A name the catalogue does not know is not a gear.
    /// </summary>
    public static int QuantityFor(string? machineName, int requested) =>
        !string.IsNullOrWhiteSpace(machineName) && CertificateTemplateCatalog.Resolve(machineName).Item?.Gear == true
            ? requested
            : 1;
}
