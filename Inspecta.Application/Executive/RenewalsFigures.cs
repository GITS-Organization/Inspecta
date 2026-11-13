using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Services;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Executive;

/// <summary>Machines due for inspection: how many are late or coming, by week, by client, and which ones. Pure, over the machines of the company.</summary>
public static class RenewalsFigures
{
    public static int DaysUntilDue(ExecMachine machine, DateOnly today) => InvoiceCalendar.Of(machine.NextDueAt!.Value).DayNumber - today.DayNumber;

    /// <summary>Overdue, and cumulative from today: within 7, 30, 60 and 90 days.</summary>
    public static RenewalBuckets Buckets(IEnumerable<ExecMachine> machines, DateOnly today)
    {
        var days = machines.Select(machine => DaysUntilDue(machine, today)).ToList();
        return new RenewalBuckets(
            days.Count(day => day < 0),
            days.Count(day => day is >= 0 and <= 7),
            days.Count(day => day is >= 0 and <= 30),
            days.Count(day => day is >= 0 and <= 60),
            days.Count(day => day is >= 0 and <= 90));
    }

    /// <summary>The overdue count, then the next eleven weeks (this week is days 0 to 6).</summary>
    public static IReadOnlyList<int> Weekly(IEnumerable<ExecMachine> machines, DateOnly today)
    {
        var days = machines.Select(machine => DaysUntilDue(machine, today)).ToList();
        var weeks = Enumerable.Range(0, 11).Select(week => days.Count(day => day >= week * 7 && day <= week * 7 + 6));
        return [days.Count(day => day < 0), .. weeks];
    }

    /// <summary>Machines due within 30 days (today included) that an open request already has an item for.</summary>
    public static int WithJob(IEnumerable<ExecMachine> machines, IEnumerable<ExecRequest> requests, DateOnly today)
    {
        var withJob = OpenEquipment(requests);
        return machines.Count(machine => DaysUntilDue(machine, today) is >= 0 and <= 30 && withJob.Contains(machine.Id));
    }

    /// <summary>The share of machines with a last result that are safe to operate (0 when none has a result).</summary>
    public static int SafePercent(IEnumerable<ExecMachine> machines)
    {
        var judged = machines.Where(machine => machine.LastResult is not null).ToList();
        return judged.Count == 0 ? 0 : (int)Math.Round(judged.Count(machine => machine.LastResult == CertificateResult.SafeToOperate) * 100m / judged.Count, MidpointRounding.AwayFromZero);
    }

    public static IReadOnlyList<OverdueByClient> MostOverdueByClient(IEnumerable<ExecMachine> machines, IEnumerable<ExecClient> clients, DateOnly today)
    {
        var names = clients.ToDictionary(client => client.Id, client => client.Name);
        return machines.Where(machine => DaysUntilDue(machine, today) < 0)
            .GroupBy(machine => machine.ClientId)
            .Select(group => new OverdueByClient(group.Key, names.GetValueOrDefault(group.Key, "Unknown client"), group.Count()))
            .OrderByDescending(item => item.Overdue).ThenBy(item => item.ClientName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>The machines of a filter, overdue first then by date: all, overdue, soon (today to 30 days), later (after 30) or notSafe (last result with conditions or not safe).</summary>
    public static IReadOnlyList<RenewalItem> Items(
        IEnumerable<ExecMachine> machines, IEnumerable<ExecClient> clients, IEnumerable<ExecRequest> requests, string filter, DateOnly today)
    {
        if (filter is not ("all" or "overdue" or "soon" or "later" or "notSafe"))
            throw new ArgumentException("filter must be 'all', 'overdue', 'soon', 'later' or 'notSafe'.");
        var names = clients.ToDictionary(client => client.Id, client => client.Name);
        var withJob = OpenEquipment(requests);
        bool Keep(ExecMachine machine)
        {
            var days = DaysUntilDue(machine, today);
            return filter switch
            {
                "all" => true,
                "overdue" => days < 0,
                "soon" => days is >= 0 and <= 30,
                "later" => days > 30,
                "notSafe" => machine.LastResult is CertificateResult.SafeWithConditions or CertificateResult.NotSafe,
                _ => throw new ArgumentException("filter must be 'all', 'overdue', 'soon', 'later' or 'notSafe'.")
            };
        }

        return machines.Where(Keep)
            .OrderBy(machine => machine.NextDueAt).ThenBy(machine => machine.Name, StringComparer.OrdinalIgnoreCase).ThenBy(machine => machine.Id)
            .Select(machine => new RenewalItem(
                machine.Id, machine.Name, machine.Serial, machine.Capacity, machine.ClientId, names.GetValueOrDefault(machine.ClientId, "Unknown client"),
                machine.LastResult, machine.NextDueAt!.Value, DaysUntilDue(machine, today), withJob.Contains(machine.Id))).ToList();
    }

    /// <summary>Machines that appear on an item of a request that is still open (not a maybe email, not turned down, not delivered).</summary>
    internal static HashSet<Guid> OpenEquipment(IEnumerable<ExecRequest> requests) =>
        requests.Where(IsOpen).SelectMany(request => request.EquipmentIds).ToHashSet();

    internal static bool IsOpen(ExecRequest request) =>
        request.Triage != RequestTriage.Maybe && request.Status is not (JobStatus.QuoteRejected or JobStatus.ClientDeclined or JobStatus.SentToClient);
}
