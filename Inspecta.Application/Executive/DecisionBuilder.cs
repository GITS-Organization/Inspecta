using Inspecta.Application.Contracts.Executive;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Executive;

/// <summary>
/// "Needs your decision": the things that should reach the general manager, by fixed rules, ranked red then violet then amber and, inside a group, the longest
/// waiting first, at most ten. Pure, over the rows the answer is already narrowed to.
/// </summary>
public static class DecisionBuilder
{
    public const int Max = 10;

    public static IReadOnlyList<Decision> Build(
        IReadOnlyList<ExecInvoice> invoices, IReadOnlyList<ExecRequest> requests, IReadOnlyList<ExecQuote> quotes, IReadOnlyList<ExecMachine> machines,
        IReadOnlyList<ExecClient> clients, IReadOnlyList<ExecPerson> people, DateOnly today)
    {
        var decisions = new List<Decision>();
        var names = people.ToDictionary(person => person.Id, person => person.Name);
        var open = requests.Where(WorkFigures.InFlight).ToList();

        string NameOf(Guid? id, string fallback) => id is Guid value && names.TryGetValue(value, out var name) ? name : fallback;

        // 1. money: invoices older than 60 days that still owe money, the biggest balances first
        foreach (var invoice in MoneyFigures.StillOwed(invoices).Where(item => MoneyFigures.AgeDays(item, today) > 60).OrderByDescending(item => item.BalancePiastres).Take(3))
        {
            var age = MoneyFigures.AgeDays(invoice, today);
            decisions.Add(new Decision(
                DecisionKind.Money, DecisionSeverity.Red, $"Invoice {invoice.Number} unpaid for {age} days", "invoice_unpaid_old", invoice.ClientId, invoice.ClientName.Trim(),
                new DecisionOwner("Finance", "financial_manager"), age, invoice.BalancePiastres, ExecutiveScreen.Money, invoice.Id));
        }

        // 2. renewals: clients with machines past their inspection date, the most machines first
        var clientNames = clients.ToDictionary(client => client.Id, client => client.Name);
        foreach (var group in machines.Where(machine => RenewalsFigures.DaysUntilDue(machine, today) < 0).GroupBy(machine => machine.ClientId)
                     .OrderByDescending(item => item.Count()).ThenBy(item => clientNames.GetValueOrDefault(item.Key, string.Empty), StringComparer.OrdinalIgnoreCase).Take(3))
        {
            var latest = requests.Where(request => request.ClientId == group.Key).OrderByDescending(request => request.ReceivedAt).FirstOrDefault();
            decisions.Add(new Decision(
                DecisionKind.Renewals, DecisionSeverity.Red, $"{group.Count()} machines past their inspection date", "machines_overdue", group.Key,
                clientNames.GetValueOrDefault(group.Key, "Unknown client"), new DecisionOwner(NameOf(latest?.ReviewerId, "Reviewer"), "technical_manager"),
                group.Max(machine => -RenewalsFigures.DaysUntilDue(machine, today)), null, ExecutiveScreen.Renewals, group.Key));
        }

        // 3. quotation: the client countered and nobody answered
        foreach (var request in open.Where(item => item.Status == JobStatus.ClientCountered))
        {
            var quote = quotes.Where(item => item.RequestId == request.Id && !item.IsPlaceholder).OrderByDescending(item => item.Version).FirstOrDefault();
            decisions.Add(new Decision(
                DecisionKind.Quotation, DecisionSeverity.Violet, "Client countered the quotation", "quotation_countered", request.ClientId, request.ClientName.Trim(),
                new DecisionOwner(NameOf(request.CoordinatorId, "Coordinator"), "coordinator"), WorkFigures.DaysInStatus(request, today),
                quote?.ClientCounterPricePiastres ?? quote?.TotalWithVatPiastres, ExecutiveScreen.Work, request.Id));
        }

        // 4. work: jobs stuck 30 days or more in the same status, the longest first
        foreach (var request in open.Where(item => WorkFigures.Stuck(item, today)).OrderByDescending(item => WorkFigures.DaysInStatus(item, today)).Take(3))
        {
            var inReview = WorkFigures.GroupOf(request.Status) == StageGroup.Review;
            decisions.Add(new Decision(
                DecisionKind.Work, DecisionSeverity.Amber, $"Job stuck in {request.Status}", "job_stuck", request.ClientId, request.ClientName.Trim(),
                inReview ? new DecisionOwner(NameOf(request.ReviewerId, "Reviewer"), "technical_manager") : new DecisionOwner(NameOf(request.CoordinatorId, "Coordinator"), "coordinator"),
                WorkFigures.DaysInStatus(request, today), null, ExecutiveScreen.Work, request.Id));
        }

        // 5. quotation: one row for the quotations nobody answered for 14 days or more
        var countered = open.Where(item => item.Status == JobStatus.ClientCountered).Select(item => item.Id).ToHashSet();
        var openRequestIds = open.Select(item => item.Id).ToHashSet();
        var unanswered = quotes.Where(quote => !quote.IsPlaceholder && quote.Status == QuoteStatus.Open && quote.SentAt is not null && quote.SupersededAt is null
            && openRequestIds.Contains(quote.RequestId) && !countered.Contains(quote.RequestId)
            && today.DayNumber - Services.InvoiceCalendar.Of(quote.SentAt.Value).DayNumber >= 14).ToList();
        if (unanswered.Count > 0)
            decisions.Add(new Decision(
                DecisionKind.Quotation, DecisionSeverity.Amber, $"{unanswered.Count} quotations unanswered for 14+ days", "quotations_unanswered", null, "Several clients",
                new DecisionOwner("Coordinators", "coordinator"), unanswered.Max(quote => today.DayNumber - Services.InvoiceCalendar.Of(quote.SentAt!.Value).DayNumber),
                unanswered.Sum(quote => quote.TotalWithVatPiastres), ExecutiveScreen.Work, null));

        // 6. work: certificates waiting for review for more than 3 days, one row per reviewer
        foreach (var group in open.Where(item => item.Status == JobStatus.CertificateSubmitted && WorkFigures.DaysInStatus(item, today) > 3).GroupBy(item => item.ReviewerId))
            decisions.Add(new Decision(
                DecisionKind.Work, DecisionSeverity.Violet, $"{group.Count()} jobs with certificates waiting for review", "certificates_waiting", null, $"{group.Count()} jobs",
                new DecisionOwner(NameOf(group.Key, "Reviewer"), "technical_manager"), group.Max(item => WorkFigures.DaysInStatus(item, today)), null, ExecutiveScreen.Work, null));

        return decisions
            .OrderBy(decision => decision.Severity switch { DecisionSeverity.Red => 0, DecisionSeverity.Violet => 1, _ => 2 })
            .ThenByDescending(decision => decision.WaitingDays)
            .Take(Max).ToList();
    }
}
