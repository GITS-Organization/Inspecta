namespace Inspecta.Domain.Entities;

public sealed record TimesheetActivity(string Name, int Minutes)
{
    public static TimesheetActivity Create(string name, int minutes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Activity name is required.", nameof(name));
        if (minutes < 0)
            throw new ArgumentOutOfRangeException(nameof(minutes));

        return new TimesheetActivity(name.Trim(), minutes);
    }
}
