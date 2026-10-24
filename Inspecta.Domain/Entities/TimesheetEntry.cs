using System.Text.Json;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class TimesheetEntry
{
    private TimesheetEntry() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid InspectorId { get; private set; }
    public string ActivitiesJson { get; private set; } = "{}";
    public long HourlyRatePiastres { get; private set; }
    public long PricePiastres { get; private set; }
    public string ExpensesJson { get; private set; } = "{}";
    public TimesheetStatus Status { get; private set; }
    public string? ReviewNote { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public long ExpensesPiastres => Deserialize<Dictionary<string, long>>(ExpensesJson).Values.Sum();
    public long TotalPiastres => checked(PricePiastres + ExpensesPiastres);

    public static TimesheetEntry Priced(
        Guid requestId,
        Guid inspectorId,
        IReadOnlyDictionary<string, int> activities,
        IReadOnlyDictionary<string, long> expenses,
        long hourlyRatePiastres)
    {
        if (requestId == Guid.Empty || inspectorId == Guid.Empty) throw new ArgumentException("Request and inspector are required.");
        if (hourlyRatePiastres < 0) throw new ArgumentOutOfRangeException(nameof(hourlyRatePiastres));
        ValidateAmounts(activities, expenses);
        var paidMinutes = activities.Where(entry => entry.Key != "unpaidBreak").Sum(entry => (long)entry.Value);
        var price = paidMinutes <= 0 || hourlyRatePiastres <= 0
            ? 0
            : checked((paidMinutes * hourlyRatePiastres + 30) / 60);

        return new TimesheetEntry
        {
            Id = Guid.NewGuid(), RequestId = requestId, InspectorId = inspectorId,
            ActivitiesJson = JsonSerializer.Serialize(activities), ExpensesJson = JsonSerializer.Serialize(expenses),
            HourlyRatePiastres = hourlyRatePiastres, PricePiastres = price, Status = TimesheetStatus.Pending
        };
    }

    public void Update(
        IReadOnlyDictionary<string, int> activities,
        IReadOnlyDictionary<string, long> expenses,
        long hourlyRatePiastres)
    {
        if (Status == TimesheetStatus.Approved) throw new InvalidOperationException("Approved timesheets are locked.");
        if (hourlyRatePiastres < 0) throw new ArgumentOutOfRangeException(nameof(hourlyRatePiastres));
        ValidateAmounts(activities, expenses);
        var paidMinutes = activities.Where(entry => entry.Key != "unpaidBreak").Sum(entry => (long)entry.Value);
        ActivitiesJson = JsonSerializer.Serialize(activities);
        ExpensesJson = JsonSerializer.Serialize(expenses);
        HourlyRatePiastres = hourlyRatePiastres;
        PricePiastres = paidMinutes <= 0 || hourlyRatePiastres <= 0 ? 0 : checked((paidMinutes * hourlyRatePiastres + 30) / 60);
        Status = TimesheetStatus.Pending;
        ReviewNote = null;
        ReviewedBy = null;
        ReviewedAt = null;
    }

    public void Approve(Guid coordinatorId)
    {
        if (Status == TimesheetStatus.Approved) throw new InvalidOperationException("Timesheet is already approved.");
        Status = TimesheetStatus.Approved;
        ReviewNote = null;
        ReviewedBy = coordinatorId;
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    public void Return(Guid coordinatorId, string note)
    {
        if (Status == TimesheetStatus.Approved) throw new InvalidOperationException("Approved timesheets are locked.");
        if (string.IsNullOrWhiteSpace(note)) throw new ArgumentException("A return note is required.", nameof(note));
        Status = TimesheetStatus.Returned;
        ReviewNote = note.Trim();
        ReviewedBy = coordinatorId;
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateAmounts(IReadOnlyDictionary<string, int> activities, IReadOnlyDictionary<string, long> expenses)
    {
        if (activities.Values.Any(value => value < 0)) throw new ArgumentOutOfRangeException(nameof(activities));
        if (expenses.Values.Any(value => value < 0)) throw new ArgumentOutOfRangeException(nameof(expenses));
    }

    private static T Deserialize<T>(string json) where T : new() => JsonSerializer.Deserialize<T>(json) ?? new T();
}