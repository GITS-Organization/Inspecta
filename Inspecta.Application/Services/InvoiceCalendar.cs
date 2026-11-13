namespace Inspecta.Application.Services;

/// <summary>The calendar day in Cairo, which decides what "today" and "the month" mean for payments and revenue.</summary>
public static class InvoiceCalendar
{
    private static readonly TimeZoneInfo Zone = Find();

    public static DateOnly Today(DateTimeOffset? now = null) => Of(now ?? DateTimeOffset.UtcNow);

    public static DateOnly Of(DateTimeOffset moment) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, Zone).DateTime);

    // A container without time-zone data must not break payments: fall back to Cairo's standard offset.
    private static TimeZoneInfo Find()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Cairo-fallback", TimeSpan.FromHours(2), "Cairo", "Cairo");
        }
    }
}
