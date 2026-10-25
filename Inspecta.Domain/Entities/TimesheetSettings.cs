namespace Inspecta.Domain.Entities;

public sealed class TimesheetSettings
{
    private TimesheetSettings() { }

    public Guid InspectorId { get; private set; }
    public string? LegacyId { get; private set; }
    public long HourlyRatePiastres { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static TimesheetSettings Create(
        Guid inspectorId,
        long hourlyRatePiastres,
        DateTimeOffset? updatedAt = null,
        string? legacyId = null)
    {
        if (inspectorId == Guid.Empty)
            throw new ArgumentException("Inspector is required.", nameof(inspectorId));
        if (hourlyRatePiastres < 0)
            throw new ArgumentOutOfRangeException(nameof(hourlyRatePiastres));

        return new TimesheetSettings
        {
            InspectorId = inspectorId,
            LegacyId = legacyId,
            HourlyRatePiastres = hourlyRatePiastres,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow
        };
    }

    public void SetHourlyRate(long hourlyRatePiastres, DateTimeOffset? updatedAt = null)
    {
        if (hourlyRatePiastres < 0)
            throw new ArgumentOutOfRangeException(nameof(hourlyRatePiastres));

        HourlyRatePiastres = hourlyRatePiastres;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }
}
