import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BackendApi } from '../../api/backend.api';
import { DashboardOverview, JobStatus } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { formatDate, plural } from '../../shared/format';

export interface Segment {
  label: string;
  count: number;
  color: string;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** Groups the API's per-status counts into the five stages of the pipeline bar. */
export function pipelineSegments(pipeline: DashboardOverview['pipeline']): { segments: Segment[]; reachedOffer: number } {
  const count = (...s: JobStatus[]) => pipeline.filter((p) => s.includes(p.status)).reduce((a, p) => a + p.count, 0);
  // `request_received` is the total of the requests that reached the offer stage or went on (see the API docs).
  const reachedOffer = count('request_received');
  const segments: Segment[] = [
    { label: 'Offers', count: reachedOffer, color: 'var(--ink)' },
    { label: 'In progress', count: count('unassigned', 'assigned', 'task_accepted', 'in_progress'), color: 'var(--teal)' },
    { label: 'Review', count: count('certificate_submitted'), color: 'var(--purple)' },
    { label: 'Approved', count: count('certificate_approved', 'sent_to_client'), color: 'var(--green)' },
    { label: 'Returned', count: count('certificate_returned'), color: 'var(--amber)' },
  ].filter((s) => s.count > 0);
  return { segments, reachedOffer };
}

@Component({
  selector: 'app-overview',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './overview.html',
  styleUrl: './overview.scss',
})
export class Overview {
  private readonly api = inject(BackendApi);

  protected readonly data = signal<DashboardOverview | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly formatDate = formatDate;
  protected readonly plural = plural;

  protected readonly pipeline = computed(() => pipelineSegments(this.data()?.pipeline ?? []));

  protected readonly months = computed(() => {
    const by = this.data()?.certificates.byMonth ?? [];
    const max = Math.max(1, ...by.map((m) => m.total));
    return by.map((m) => ({
      label: MONTHS[Number(m.month.slice(5, 7)) - 1] ?? m.month,
      total: m.total,
      safe: (m.safe / max) * 100,
      conditions: (m.withConditions / max) * 100,
      notSafe: (m.notSafe / max) * 100,
      aria: `${m.month}: ${m.safe} safe, ${m.withConditions} with conditions, ${m.notSafe} not safe`,
    }));
  });

  protected readonly maxWorkload = computed(() => Math.max(1, ...(this.data()?.inspectorWorkload ?? []).map((i) => i.certificates)));

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.overview().subscribe({
      next: (d) => {
        this.data.set(d);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The overview could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  protected dueChip(days: number): string {
    return days < 0 ? `${plural(-days, 'day')} late` : days === 0 ? 'due today' : `in ${plural(days, 'day')}`;
  }
}
