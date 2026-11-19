using System.Globalization;
using System.Net;
using System.Text;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>The words and the table of a renewal reminder. Dates are written as 2 Dec 2026; every value is HTML-encoded.</summary>
public static class RenewalReminderEmail
{
    public static string DefaultSubject(int machines, string place) =>
        $"Periodic inspection due: {machines} machine{(machines == 1 ? string.Empty : "s")} at {place}";

    private static string Date(DateOnly? date) => date is { } value ? value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "-";

    /// <summary>The machines, the earliest due first, as a plain text table and as an HTML table.</summary>
    public static (string Text, string Html) Table(IEnumerable<ReminderMachine> machines)
    {
        var rows = machines.OrderBy(machine => machine.NextDueOn).ThenBy(machine => machine.Name, StringComparer.OrdinalIgnoreCase).ToList();

        var text = new StringBuilder("Machine | Serial | Last inspected | Due");
        foreach (var machine in rows)
            text.AppendLine().Append($"{machine.Name} | {machine.SerialNumber ?? "-"} | {Date(machine.LastInspectedOn)} | {Date(machine.NextDueOn)}");

        const string cell = "style=\"border:1px solid #d9dee8;padding:6px 10px;text-align:left\"";
        var html = new StringBuilder("<table style=\"border-collapse:collapse;margin:12px 0;font-size:13px\"><thead><tr>");
        foreach (var heading in new[] { "Machine", "Serial", "Last inspected", "Due" })
            html.Append($"<th {cell}>{heading}</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var machine in rows)
            html.Append("<tr>")
                .Append($"<td {cell}>{WebUtility.HtmlEncode(machine.Name)}</td>")
                .Append($"<td {cell}>{WebUtility.HtmlEncode(machine.SerialNumber ?? "-")}</td>")
                .Append($"<td {cell}>{WebUtility.HtmlEncode(Date(machine.LastInspectedOn))}</td>")
                .Append($"<td {cell}>{WebUtility.HtmlEncode(Date(machine.NextDueOn))}</td>")
                .Append("</tr>");
        html.Append("</tbody></table>");
        return (text.ToString(), html.ToString());
    }
}
