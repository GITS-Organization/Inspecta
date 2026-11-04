namespace Inspecta.Application.Certificates;

/// <summary>How many serial numbers fit on one certificate, so a gear certificate never overflows its page.</summary>
public static class SerialNumbers
{
    /// <summary>The most characters the printed serial number box takes.</summary>
    public const int MaxCharacters = 1000;

    /// <summary>
    /// What one gear certificate can hold in all: its serial text plus the free texts (defect, repairs, tests, note). Measured on the printed
    /// certificate: about 1250 characters in total fit on the one page (1000 of serial numbers with texts of 60 characters each fit, 100 each do not).
    /// </summary>
    public const int PageBudget = 1200;

    /// <summary>The longest single serial number accepted.</summary>
    public const int MaxLength = 60;

    /// <summary>The most serial numbers one split can take.</summary>
    public const int MaxCount = 5000;

    public const string Separator = ", ";

    /// <summary>Joins serial numbers the way the certificate prints them.</summary>
    public static string Join(IEnumerable<string> serials) => string.Join(Separator, serials);

    /// <summary>How many serial numbers of at most <paramref name="longest"/> characters fit on one certificate.</summary>
    public static int PerCertificate(int longest) => Math.Max(1, (MaxCharacters + Separator.Length) / (longest + Separator.Length));

    /// <summary>Cuts the serial numbers into <paramref name="certificates"/> groups of nearly equal size, in order.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Divide(IReadOnlyList<string> serials, int certificates)
    {
        var groups = new List<IReadOnlyList<string>>();
        var each = serials.Count / certificates;
        var extra = serials.Count % certificates;
        var at = 0;
        for (var index = 0; index < certificates; index++)
        {
            var take = each + (index < extra ? 1 : 0);
            groups.Add(serials.Skip(at).Take(take).ToArray());
            at += take;
        }

        return groups;
    }
}
