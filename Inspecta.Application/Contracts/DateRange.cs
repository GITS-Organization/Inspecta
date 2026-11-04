namespace Inspecta.Application.Contracts;

/// <summary>The date filter of the lists: whole days, both ends included, counted in UTC.</summary>
public static class DateRange
{
    /// <summary>
    /// The first moment to include and the first moment to leave out for <paramref name="from"/> and <paramref name="to"/> (a day each, either
    /// may be left out). <c>to</c> before <c>from</c> is refused.
    /// </summary>
    public static (DateTimeOffset? From, DateTimeOffset? Until) Bounds(DateOnly? from, DateOnly? to)
    {
        if (from is { } start && to is { } end && end < start)
            throw new ArgumentException("The end date cannot be before the start date.");
        return (
            from is { } first ? new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null,
            to is { } last ? new DateTimeOffset(last.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null);
    }
}
