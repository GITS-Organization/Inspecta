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

    public async Task<MoneyResponse> MoneyAsync(ExecutiveQuery query, CancellationToken cancellationToken = default) =>
        MoneyOf(await ScopeAsync(query, cancellationToken));

    public async Task<FinanceOverviewResponse> FinanceOverviewAsync(ExecutiveQuery query, CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(query, cancellationToken);
        return new FinanceOverviewResponse(
            MoneyOf(scope),
            FinanceFigures.ReadyToClose(scope.Invoices, scope.Today, 5),
            FinanceFigures.NotHandedOver(scope.Invoices),
            FinanceFigures.OldUnpaid(scope.Invoices, scope.Today, 3),
            FinanceFigures.LatestPayments(scope.Invoices, scope.Data.People, 10));
    }

    private static MoneyResponse MoneyOf(Scope scope) => new(
        scope.Days, scope.PreviousDays, MoneyKpisOf(scope), MoneyFigures.Aging(scope.Invoices, scope.Today),
        MoneyFigures.WhoOwesMost(scope.Invoices, scope.Today, 6), MoneyFigures.Monthly(scope.Invoices, scope.Today));

    public async Task<RenewalsResponse> RenewalsAsync(ExecutiveQuery query, string? filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(query, cancellationToken);
        var machines = scope.Data.Machines;
        // The reviewer filter does not narrow machines: a machine belongs to a client, not to a reviewer.
        var requests = scope.Data.Requests;
        var items = RenewalsFigures.Items(machines, scope.Data.Clients, requests, string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim(), scope.Today);
        var (number, size) = Paging(page, pageSize);
        return new RenewalsResponse(
            RenewalsFigures.Buckets(machines, scope.Today), RenewalsFigures.WithJob(machines, requests, scope.Today), machines.Count, RenewalsFigures.SafePercent(machines),
            RenewalsFigures.Weekly(machines, scope.Today), RenewalsFigures.MostOverdueByClient(machines, scope.Data.Clients, scope.Today),
            items.Count, number, size, items.Skip((number - 1) * size).Take(size).ToList());
    }

    public async Task<ClientsResponse> ClientsAsync(ExecutiveQuery query, string? health, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(query, cancellationToken);
        var parsed = ClientFigures.ParseHealth(health);
        var rows = ClientFigures.Rows(scope.Data, Jobs(scope.Data), scope.Data.Invoices, scope.Today);
        var filtered = ClientFigures.Filter(rows, parsed, search);
        var (number, size) = Paging(page, pageSize);
        return new ClientsResponse(ClientFigures.Counts(rows), filtered.Count, number, size, filtered.Skip((number - 1) * size).Take(size).ToList());
    }

    public async Task<ClientDetailResponse?> ClientAsync(ExecutiveQuery query, Guid clientId, CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(query, cancellationToken);
        var client = scope.Data.Clients.FirstOrDefault(item => item.Id == clientId && item.ArchivedAt is null);
        if (client is null) return null;

        var jobs = Jobs(scope.Data);
        var row = ClientFigures.Row(client, scope.Data, jobs, scope.Data.Invoices, scope.Today);
        var machines = scope.Data.Machines.Where(machine => machine.ClientId == clientId && RenewalsFigures.DaysUntilDue(machine, scope.Today) <= 90)
            .OrderBy(machine => machine.NextDueAt)
            .Select(machine => new ClientMachineRow(machine.Name, machine.Serial, machine.Capacity, machine.LastResult, machine.NextDueAt!.Value, RenewalsFigures.DaysUntilDue(machine, scope.Today))).ToList();
        var invoices = scope.Data.Invoices.Where(invoice => invoice.ClientId == clientId).OrderByDescending(invoice => invoice.CreatedAt).Take(20).Select(ClientFigures.Summary).ToList();
        return new ClientDetailResponse(
            row.Id, row.Name, row.City, row.ReviewerName, row.Contract, row.Since, row.Machines, row.OpenJobs, row.InvoicedYtdPiastres, row.OwedPiastres,
            row.OlderThan60Piastres, row.OverdueMachines, row.NextDueInDays, row.Health,
            client.Contacts.Select(contact => new ClientContactRow(contact.Name, contact.Role, contact.Email, contact.Phone)).ToList(), machines, invoices,
            new ClientRevenue(row.InvoicedYtdPiastres, row.OwedPiastres, row.OlderThan60Piastres));
    }

    public async Task<TodayResponse> TodayAsync(ExecutiveQuery query, CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(query, cancellationToken);
        var period = scope.Period;
        var money = MoneyKpisOf(scope);
        var delivery = WorkFigures.RequestToDelivery(scope.Requests, period.From, period.To);
        var overdue = scope.Data.Machines.Count(machine => RenewalsFigures.DaysUntilDue(machine, scope.Today) < 0);
        var dueIn30 = scope.Data.Machines.Count(machine => RenewalsFigures.DaysUntilDue(machine, scope.Today) is >= 0 and <= 30);
        var kpis = new TodayKpis(
            money.Collected, money.Outstanding, money.DaysToCollect,
            new DaysKpi(delivery, WorkFigures.RequestToDelivery(scope.Requests, period.PreviousFrom, period.PreviousTo), scope.Targets.RequestToDeliveryDays, delivery <= scope.Targets.RequestToDeliveryDays),
            new MachinesOverdueKpi(overdue, dueIn30, overdue == 0));
        return new TodayResponse(
            scope.Days, scope.PreviousDays, kpis,
            DecisionBuilder.Build(scope.Invoices, scope.Requests, scope.Quotes, scope.Data.Machines, scope.Data.Clients, scope.Data.People, scope.Today),
            MoneyFigures.Monthly(scope.Invoices, scope.Today), WorkFigures.Pipeline(scope.Requests),
            new CertificatesIssued(WorkFigures.CertificatesIssued(scope.Certificates, period.From, period.To), WorkFigures.SafePercentOfIssued(scope.Certificates, period.From, period.To)));
    }

    public async Task<ActivityResponse> ActivityAsync(Guid? managerId, string? kind, DateTimeOffset? before, int limit, CancellationToken cancellationToken = default)
    {
        var parsed = ActivityBuilder.ParseKind(kind);
        var data = await dataSource.LoadAsync(managerId, cancellationToken);
        var (items, next) = ActivityBuilder.Page(ActivityBuilder.Events(data), parsed, before, Math.Clamp(limit, 1, 100));
        return new ActivityResponse(items, next, ActivityBuilder.Upcoming(data, InvoiceCalendar.Today()));
    }

    public async Task<WorkResponse> WorkAsync(ExecutiveQuery query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(query, cancellationToken);
        var period = scope.Period;
        var open = WorkFigures.OpenJobs(scope.Requests, scope.Data.People, scope.Today);
        var (number, size) = Paging(page, pageSize);
        // The reviewers and inspectors tables are about the people, so the reviewer filter does not narrow them.
        var allCertificates = scope.Data.Certificates;
        return new WorkResponse(
            scope.Days, scope.PreviousDays,
            WorkFigures.Jobs(scope.Requests, period.From, period.To, scope.Today),
            open.Skip((number - 1) * size).Take(size).ToList(), open.Count, number, size,
            WorkFigures.StepTimes(scope.Requests, period.From, period.To, scope.Targets.ReviewDaysPerStep),
            WorkFigures.Quotations(scope.Quotes, scope.Requests, scope.Data.Clients, period.From, period.To, scope.Today),
            WorkFigures.CertificatesByMonth(scope.Certificates, scope.Today),
            WorkFigures.Reviewers(scope.Data.People, scope.AllRequests, allCertificates, period.From, period.To, scope.Targets.ReviewDaysPerStep),
            WorkFigures.Inspectors(scope.Data.People, scope.AllRequests, allCertificates, period.From, period.To));
    }

    // Requests that count as jobs: emails still marked "maybe" never do.
    private static IReadOnlyList<ExecRequest> Jobs(ExecutiveData data) => data.Requests.Where(request => request.Triage != Domain.Enums.RequestTriage.Maybe).ToList();

    private static (int Page, int PageSize) Paging(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    // ---- shared pieces ----

    /// <summary>One company's rows with the period and the targets worked out once; the reviewer filter is applied to what hangs on a request.</summary>
    internal sealed record Scope(
        ExecutivePeriod Period, DateOnly Today, ExecutiveTargetsResponse Targets, ExecutiveData Data,
        IReadOnlyList<ExecInvoice> Invoices, IReadOnlyList<ExecRequest> Requests, IReadOnlyList<ExecRequest> AllRequests,
        IReadOnlyList<ExecQuote> Quotes, IReadOnlyList<ExecCertificate> Certificates)
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
        var allRequests = requests;
        var invoices = data.Invoices;
        var quotes = data.Quotes;
        var certificates = data.Certificates;
        if (query.ReviewerId is Guid reviewerId)
        {
            var mine = requests.Where(request => request.ReviewerId == reviewerId).ToList();
            var ids = mine.Select(request => request.Id).ToHashSet();
            requests = mine;
            invoices = data.Invoices.Where(invoice => ids.Contains(invoice.RequestId)).ToList();
            quotes = data.Quotes.Where(quote => ids.Contains(quote.RequestId)).ToList();
            certificates = data.Certificates.Where(certificate => ids.Contains(certificate.RequestId)).ToList();
        }

        return new Scope(period, today, targets, data, invoices, requests, allRequests, quotes, certificates);
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
