namespace Inspecta.Application.Executive;

/// <summary>
/// The days an executive answer is worked out over (both days included, Cairo days), and the same number of days directly before it,
/// which the screens compare against ("vs previous period").
/// </summary>
public sealed record ExecutivePeriod(DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo)
{
    public const int MaxDays = 731;

    public int Days => To.DayNumber - From.DayNumber + 1;

    public bool Contains(DateOnly day) => day >= From && day <= To;

    public bool PreviousContains(DateOnly day) => day >= PreviousFrom && day <= PreviousTo;

    /// <summary>
    /// Without dates: the first of the current month to today. Only <c>to</c>: from the first of that month. A <c>to</c> in the future is cut to
    /// today. A start after the end, or a span over two years, is refused.
    /// </summary>
    public static ExecutivePeriod Resolve(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to is DateOnly requested && requested < today ? requested : today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (start > end) throw new ArgumentException("from cannot be after to.");
        if (end.DayNumber - start.DayNumber + 1 > MaxDays) throw new ArgumentException("The period can be at most two years.");

        var days = end.DayNumber - start.DayNumber + 1;
        var previousTo = start.AddDays(-1);
        return new ExecutivePeriod(start, end, previousTo.AddDays(-(days - 1)), previousTo);
    }
}
