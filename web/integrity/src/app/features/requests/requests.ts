import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { RequestDetails, RequestSummary } from '../../api/coordinator.models';
import { JobStatus } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { formatDate, formatDateTime, plural, relativeTime } from '../../shared/format';
import { AssignDialog } from '../../shared/assign-dialog';
import { KindBadge } from '../../shared/kind-badge';

export type TabId = 'new' | 'details' | 'progress' | 'man_day' | 'closed' | 'maybe' | 'all';

/** Statuses of a request that was accepted: the work goes on in Jobs, the request stays here for reference. */
export const IN_PROGRESS: JobStatus[] = [
  'unassigned', 'assigned', 'task_accepted', 'in_progress', 'certificate_submitted', 'certificate_returned', 'certificate_approved', 'sent_to_client',
];
export const CLOSED: JobStatus[] = ['quote_rejected', 'client_declined'];

export interface TabDef {
  id: TabId;
  label: string;
  triage: 'request' | 'maybe';
  /** Sent to the API as `status` when the tab is one status. */
  status?: JobStatus;
  /** A tab the API cannot filter by itself: the list is loaded (up to 100) and filtered here. */
  derived?: (r: RequestDetails) => boolean;
  empty: { title: string; hint: string };
}

export const TABS: TabDef[] = [
  { id: 'new', label: 'New requests', triage: 'request', status: 'request_received', empty: { title: 'No new requests.', hint: 'When a client emails you, the request appears here. You can also enter one yourself.' } },
  { id: 'details', label: 'Needs details', triage: 'request', status: 'request_received', derived: (r) => r.status === 'request_received' && r.missingFields.length > 0, empty: { title: 'Every new request is complete.', hint: 'Requests that miss a client, a location or an item wait here until you fill them in.' } },
  { id: 'progress', label: 'Accepted', triage: 'request', derived: (r) => IN_PROGRESS.includes(r.status), empty: { title: 'No accepted request yet.', hint: 'Accepted requests are worked on under Jobs.' } },
  { id: 'man_day', label: 'Man days', triage: 'request', derived: (r) => r.kind === 'man_day', empty: { title: 'No man day yet.', hint: 'A man day is a day of an inspector that a client asked for, with no list of machines. Create one when a client phones.' } },
  { id: 'closed', label: 'Closed', triage: 'request', derived: (r) => CLOSED.includes(r.status), empty: { title: 'No closed requests.', hint: 'Requests you turned down, or that the client declined, end up here.' } },
  { id: 'maybe', label: 'Unknown senders', triage: 'maybe', empty: { title: 'No email is waiting for a decision.', hint: 'Emails from senders that belong to no client wait here. Add the sender to a client and they become requests.' } },
  { id: 'all', label: 'All', triage: 'request', empty: { title: 'No request matches.', hint: 'Try another search, or clear it.' } },
];

const PAGE = 20;
const STORE = 'integrity.requests.tab';
const isTab = (v: string | undefined | null): v is TabId => !!v && TABS.some((t) => t.id === v);

/** Counts for the tab badges: from the summary where the API knows them, otherwise unknown (null). */
export function tabCounts(s: RequestSummary | null, needsDetails: number | null, manDays: number | null = null): Record<TabId, number | null> {
  const by = (...st: JobStatus[]) => (s ? s.byStatus.filter((x) => st.includes(x.status)).reduce((a, x) => a + x.count, 0) : null);
  return { new: by('request_received'), details: needsDetails, progress: by(...IN_PROGRESS), man_day: manDays, closed: by(...CLOSED), maybe: s ? s.maybe : null, all: s ? s.requests : null };
}

/** What a request needs next: one chip, and the colour of the edge of its row. */
export function describe(r: RequestDetails): { label: string; chip: string; edge: string } {
  if (r.triage === 'maybe') return { label: 'Unknown sender', chip: 'a', edge: 'amber' };
  switch (r.status) {
    case 'request_received':
      return r.missingFields.length ? { label: 'Needs details', chip: 'a', edge: 'amber' } : { label: 'New request', chip: '', edge: 'ink' };
    case 'quote_draft':
      return { label: 'Quotation being prepared', chip: '', edge: 'ink' };
    case 'quote_sent':
    case 'counter_offer_sent':
      return { label: 'Waiting for the client', chip: '', edge: 'ink' };
    case 'client_countered':
      return { label: 'Client replied', chip: 'a', edge: 'amber' };
    case 'quote_rejected':
    case 'client_declined':
      return { label: 'Closed', chip: '', edge: 'grey' };
    case 'certificate_approved':
      return { label: 'Certificates ready', chip: 'g', edge: 'green' };
    case 'sent_to_client':
      return { label: 'Delivered', chip: 'g', edge: 'green' };
    default:
      return { label: 'Accepted', chip: 'g', edge: 'teal' };
  }
}

/** The request inbox: the status is the navigation. Tab, search and page live in the URL (`?tab=&q=&page=`). */
@Component({
  selector: 'app-requests',
  imports: [RouterLink, KindBadge, AssignDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './requests.html',
  styleUrl: './requests.scss',
})
export class Requests {
  // Bound from the query string by withComponentInputBinding.
  readonly tab = input<string>();
  readonly q = input<string>();
  readonly page = input<string>();

  private readonly api = inject(CoordinatorApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly tabs = TABS;
  protected readonly describe = describe;
  protected readonly relative = relativeTime;
  protected readonly full = formatDateTime;
  protected readonly formatDate = formatDate;
  protected readonly plural = plural;
  protected readonly skeleton = [1, 2, 3, 4, 5];

  protected readonly summary = signal<RequestSummary | null>(null);
  protected readonly needsDetails = signal<number | null>(null);
  protected readonly manDays = signal<number | null>(null);
  protected readonly fetched = signal<RequestDetails[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly rowError = signal<{ id: string; text: string } | null>(null);
  protected readonly assignFor = signal<RequestDetails | null>(null);
  protected readonly busyId = signal<string | null>(null);
  protected readonly search = signal('');
  private timer: ReturnType<typeof setTimeout> | undefined;
  private latest = '';

  protected readonly active = computed<TabDef>(() => TABS.find((t) => t.id === this.tab()) ?? TABS[0]);
  protected readonly pageNo = computed(() => Math.max(1, parseInt(this.page() ?? '1', 10) || 1));
  protected readonly query = computed(() => (this.q() ?? '').trim());
  protected readonly counts = computed(() => tabCounts(this.summary(), this.needsDetails(), this.manDays()));
  private readonly matching = computed(() => {
    const d = this.active().derived;
    return d ? this.fetched().filter(d) : this.fetched();
  });
  protected readonly rows = computed(() => (this.active().derived ? this.matching().slice((this.pageNo() - 1) * PAGE, this.pageNo() * PAGE) : this.matching()));
  protected readonly hasNext = computed(() => (this.active().derived ? this.matching().length > this.pageNo() * PAGE : this.fetched().length >= PAGE));

  constructor() {
    this.loadSummary();
    effect(() => {
      // Nothing in the URL: open the tab used last time.
      if (this.tab()) return;
      const last = this.read();
      if (isTab(last) && last !== 'new') untracked(() => void this.go({ tab: last }, true));
    });
    effect(() => {
      const t = this.active();
      const q = this.query();
      const p = this.pageNo();
      untracked(() => {
        this.search.set(q);
        this.remember(t.id);
        // On a phone the active tab may sit outside the visible part of the strip.
        setTimeout(() => document.getElementById('tab-' + t.id)?.scrollIntoView?.({ inline: 'center', block: 'nearest' }));
        this.load(t, q, p);
      });
    });
  }

  private read(): string | null {
    try {
      return localStorage.getItem(STORE);
    } catch {
      return null;
    }
  }
  private remember(id: TabId): void {
    try {
      localStorage.setItem(STORE, id);
    } catch {
      /* storage unavailable */
    }
  }

  private loadSummary(): void {
    this.api.requestSummary().subscribe({ next: (s) => this.summary.set(s), error: () => undefined });
  }

  private load(t: TabDef, q: string, page: number): void {
    this.loading.set(true);
    this.error.set(null);
    const key = `${t.id}|${q}|${page}`;
    this.latest = key;
    // Tabs the API cannot filter load the whole default list once and are paged here; the others page on the server.
    const query = t.derived
      ? { triage: t.triage, status: t.status, q, pageSize: 100 }
      : { triage: t.triage, status: t.status, q, page, pageSize: PAGE };
    this.api.requests(query).subscribe({
      next: (r) => {
        if (key !== this.latest) return;
        this.fetched.set(r);
        this.loading.set(false);
        if (t.id === 'details' && !q) this.needsDetails.set(r.filter((x) => x.missingFields.length > 0).length);
        if (t.id === 'man_day' && !q) this.manDays.set(r.filter((x) => x.kind === 'man_day').length);
      },
      error: (e: unknown) => {
        if (key !== this.latest) return;
        this.error.set(problemMessage(e, 'The requests could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  // ---- navigation: all state goes through the URL
  private go(p: { tab?: string; q?: string | null; page?: number | null }, replace = false): Promise<boolean> {
    const queryParams: Record<string, string | null> = {};
    if (p.tab !== undefined) queryParams['tab'] = p.tab === 'new' ? null : p.tab;
    if (p.q !== undefined) queryParams['q'] = p.q || null;
    queryParams['page'] = p.page && p.page > 1 ? String(p.page) : null;
    return this.router.navigate([], { queryParams, queryParamsHandling: 'merge', replaceUrl: replace });
  }

  protected select(id: TabId): void {
    void this.go({ tab: id, page: 1 });
  }

  /** Roving tabindex: arrows, Home and End move the focus and the selection together. */
  protected key(e: KeyboardEvent, i: number): void {
    const n = TABS.length;
    const to = e.key === 'ArrowRight' ? (i + 1) % n : e.key === 'ArrowLeft' ? (i - 1 + n) % n : e.key === 'Home' ? 0 : e.key === 'End' ? n - 1 : -1;
    if (to < 0) return;
    e.preventDefault();
    this.select(TABS[to].id);
    queueMicrotask(() => document.getElementById('tab-' + TABS[to].id)?.focus());
  }

  protected typing(v: string): void {
    this.search.set(v);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.go({ q: v.trim(), page: 1 }, true), 300);
  }
  protected clear(): void {
    clearTimeout(this.timer);
    this.search.set('');
    void this.go({ q: '', page: 1 }, true);
  }
  protected turn(d: number): void {
    void this.go({ page: Math.max(1, this.pageNo() + d) });
  }

  protected assigned(r: RequestDetails): void {
    this.assignFor.set(null);
    this.toast.show('The job was given to the inspector. No quotation is needed.');
    this.loadSummary();
    void this.router.navigate(['/jobs', r.id]);
  }

  protected confirm(r: RequestDetails): void {
    if (this.busyId()) return;
    this.busyId.set(r.id);
    this.rowError.set(null);
    this.api.confirmMaybe(r.id).subscribe({
      next: (x) => {
        this.busyId.set(null);
        this.toast.show('It is now a request.');
        this.loadSummary();
        void this.router.navigate(['/requests', x.id]);
      },
      error: (e: unknown) => {
        this.busyId.set(null);
        this.rowError.set({ id: r.id, text: problemMessage(e) });
      },
    });
  }
}
