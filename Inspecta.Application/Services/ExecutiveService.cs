using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Executive;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>The read-only answers for the general manager's screens, and the company targets they are measured against.</summary>
public sealed class ExecutiveService(IExecutiveTargetsRepository targetsRepository, IExecutiveDataSource dataSource) : IExecutiveService
{
    public async Task<ExecutiveTargetsResponse> GetTargetsAsync(Guid? managerId, CancellationToken cancellationToken = default)
    {
        var stored = managerId is Guid id ? await targetsRepository.GetAsync(id, false, cancellationToken) : null;
        return Map(stored);
    }

    public async Task<ExecutiveTargetsResponse> SaveTargetsAsync(
        Guid managerId, SaveExecutiveTargetsRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var targets = await targetsRepository.GetAsync(managerId, true, cancellationToken);
        if (targets is null)
        {
            targets = ExecutiveTargets.Create(
                managerId, request.CollectionRatePercent, request.DaysToCollect, request.RequestToDeliveryDays, request.ReviewDaysPerStep, actorId);
            await targetsRepository.AddAsync(targets, cancellationToken);
        }
        else
        {
            targets.Update(request.CollectionRatePercent, request.DaysToCollect, request.RequestToDeliveryDays, request.ReviewDaysPerStep, actorId);
            await targetsRepository.SaveAsync(cancellationToken);
        }

        return Map(targets);
    }

    public async Task<MoneyResponse> MoneyAsync(ExecutiveQuery query, CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(query, cancellationToken);
        return new MoneyResponse(
            scope.Days, scope.PreviousDays, MoneyKpisOf(scope), MoneyFigures.Aging(scope.Invoices, scope.Today),
            MoneyFigures.WhoOwesMost(scope.Invoices, scope.Today, 6), MoneyFigures.Monthly(scope.Invoices, scope.Today));
    }

    // ---- shared pieces ----

    /// <summary>One company's rows with the period and the targets worked out once; the reviewer filter is applied to what hangs on a request.</summary>
    internal sealed record Scope(
        ExecutivePeriod Period, DateOnly Today, ExecutiveTargetsResponse Targets, ExecutiveData Data,
        IReadOnlyList<ExecInvoice> Invoices, IReadOnlyList<ExecRequest> Requests)
    {
        public ExecutiveDays Days => new(Period.From, Period.To);
        public ExecutiveDays PreviousDays => new(Period.PreviousFrom, Period.PreviousTo);
    }

    private async Task<Scope> ScopeAsync(ExecutiveQuery query, CancellationToken cancellationToken)
    {
        var today = InvoiceCalendar.Today();
        var period = ExecutivePeriod.Resolve(query.From, query.To, today);
        var data = await dataSource.LoadAsync(query.ManagerId, cancellationToken);
        var targets = await GetTargetsAsync(query.ManagerId, cancellationToken);

        var requests = data.Requests.Where(request => request.Triage != Domain.Enums.RequestTriage.Maybe).ToList();
        var invoices = data.Invoices;
        if (query.ReviewerId is Guid reviewerId)
        {
            var mine = requests.Where(request => request.ReviewerId == reviewerId).ToList();
            var ids = mine.Select(request => request.Id).ToHashSet();
            requests = mine;
            invoices = data.Invoices.Where(invoice => ids.Contains(invoice.RequestId)).ToList();
        }

        return new Scope(period, today, targets, data, invoices, requests);
    }

    private static MoneyKpis MoneyKpisOf(Scope scope)
    {
        var period = scope.Period;
        var invoiced = MoneyFigures.Invoiced(scope.Invoices, period.From, period.To);
        var previousInvoiced = MoneyFigures.Invoiced(scope.Invoices, period.PreviousFrom, period.PreviousTo);
        var collected = MoneyFigures.Collected(scope.Invoices, period.From, period.To);
        var previousCollected = MoneyFigures.Collected(scope.Invoices, period.PreviousFrom, period.PreviousTo);
        var rate = MoneyFigures.RatePercent(collected, invoiced);
        var days = MoneyFigures.DaysToCollect(scope.Invoices, period.From, period.To);
        return new MoneyKpis(
            new InvoicedKpi(invoiced, previousInvoiced),
            new CollectedKpi(collected, invoiced, rate, MoneyFigures.RatePercent(previousCollected, previousInvoiced), scope.Targets.CollectionRatePercent, rate >= scope.Targets.CollectionRatePercent),
            MoneyFigures.Outstanding(scope.Invoices, scope.Today),
            new DaysKpi(days, MoneyFigures.DaysToCollect(scope.Invoices, period.PreviousFrom, period.PreviousTo), scope.Targets.DaysToCollect, days <= scope.Targets.DaysToCollect));
    }

    internal static ExecutiveTargetsResponse Map(ExecutiveTargets? targets) => targets is null
        ? new ExecutiveTargetsResponse(
            ExecutiveTargets.DefaultCollectionRatePercent, ExecutiveTargets.DefaultDaysToCollect,
            ExecutiveTargets.DefaultRequestToDeliveryDays, ExecutiveTargets.DefaultReviewDaysPerStep)
        : new ExecutiveTargetsResponse(targets.CollectionRatePercent, targets.DaysToCollect, targets.RequestToDeliveryDays, targets.ReviewDaysPerStep);
}
