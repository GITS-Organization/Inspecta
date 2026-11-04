import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { Inspector } from '../../api/coordinator.models';
import { MACHINES_OPEN, QueueFilter, QueueRow } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { formatDate } from '../../shared/format';
import { KindBadge } from '../../shared/kind-badge';

const TABS: { filter: QueueFilter; label: string; empty: string }[] = [
  { filter: 'unassigned', label: 'To give to an inspector', empty: 'No accepted job is waiting for an inspector.' },
  { filter: 'assigned', label: 'With the inspectors', empty: 'No inspection is in progress.' },
  { filter: 'certify', label: 'Certificates in progress', empty: 'No certificates are being prepared.' },
  { filter: 'approved', label: 'Ready to hand over', empty: 'No approved certificates are waiting to be handed over.' },
  { filter: 'delivered', label: 'Delivered', empty: 'Nothing was delivered yet.' },
];

/** The coordinator's job queue: give accepted jobs to inspectors, follow them, and hand the approved certificates to the client. */
@Component({
  selector: 'app-queue',
  imports: [RouterLink, KindBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './queue.html',
  styleUrl: './queue.scss',
})
export class Queue {
  private readonly api = inject(CoordinatorApi);
  private readonly toast = inject(ToastService);
  protected readonly tabs = TABS;
  protected readonly formatDate = formatDate;

  protected readonly filter = signal<QueueFilter>('unassigned');
  protected readonly rows = signal<QueueRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly empty = computed(() => TABS.find((t) => t.filter === this.filter())?.empty ?? '');

  protected readonly inspectors = signal<Inspector[]>([]);
  /** The row whose assign / change-date panel is open. */
  protected readonly openId = signal<string | null>(null);
  protected readonly panel = signal<'assign' | 'date'>('assign');
  protected readonly inspectorId = signal('');
  protected readonly when = signal('');
  protected readonly note = signal('');
  protected readonly busy = signal(false);
  protected readonly formError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected select(f: QueueFilter): void {
    this.filter.set(f);
    this.openId.set(null);
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    const wanted = this.filter();
    this.api.queue(wanted).subscribe({
      next: (r) => {
        if (wanted !== this.filter()) return;
        this.rows.set(r);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The jobs could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  protected open(r: QueueRow, panel: 'assign' | 'date'): void {
    if (this.openId() === r.requestId && this.panel() === panel) return this.openId.set(null);
    this.openId.set(r.requestId);
    this.panel.set(panel);
    this.formError.set(null);
    this.note.set('');
    this.when.set(r.scheduledAt ? toLocalInput(r.scheduledAt) : '');
    this.inspectorId.set(r.inspectorId ?? '');
    if (panel === 'assign' && !this.inspectors().length) {
      this.api.inspectors().subscribe({ next: (i) => this.inspectors.set(i), error: (e: unknown) => this.formError.set(problemMessage(e, 'The inspectors could not be loaded.')) });
    }
  }

  protected submit(r: QueueRow): void {
    if (this.busy()) return;
    if (!this.when()) {
      this.formError.set('Choose the date and time of the inspection.');
      return;
    }
    if (this.panel() === 'assign' && !this.inspectorId()) {
      this.formError.set('Choose an inspector.');
      return;
    }
    const iso = new Date(this.when()).toISOString();
    this.busy.set(true);
    this.formError.set(null);
    const call = this.panel() === 'assign' ? this.api.assign(r.requestId, this.inspectorId(), iso, this.note().trim()) : this.api.changeInspectionDate(r.requestId, iso, this.note().trim());
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.openId.set(null);
        this.toast.show(this.panel() === 'assign' ? 'The job was given to the inspector.' : 'The inspection date was changed.');
        this.load();
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.formError.set(problemMessage(e));
      },
    });
  }

  /** The job is under way: its machines can still change. */
  protected isOpen(r: QueueRow): boolean {
    return MACHINES_OPEN.includes(r.requestStatus);
  }

  protected inspectorLabel(i: Inspector): string {
    return `${i.fullName} · ${i.activeJobs} active${i.isOnLeave ? ' · on leave' + (i.leaveUntil ? ' until ' + formatDate(i.leaveUntil) : '') : ''}`;
  }

  protected state(r: QueueRow): { text: string; cls: string } {
    switch (r.requestStatus) {
      case 'unassigned':
        return { text: 'Waiting for an inspector', cls: 'chip a' };
      case 'assigned':
      case 'task_accepted':
        return { text: 'Not started', cls: 'chip' };
      case 'in_progress':
        return { text: 'Inspection in progress', cls: 'chip' };
      case 'certificate_submitted':
        return { text: 'Waiting for the technical manager', cls: 'chip' };
      case 'certificate_returned':
        return { text: 'Returned to the inspector', cls: 'chip r' };
      case 'certificate_approved':
        return { text: 'Ready to hand over', cls: 'chip g' };
      case 'sent_to_client':
        return { text: 'Delivered', cls: 'chip g' };
      default:
        return { text: r.requestStatus, cls: 'chip' };
    }
  }
}

/** ISO timestamp -> value of a datetime-local input (the user's own time zone). */
export function toLocalInput(iso: string): string {
  const d = new Date(iso);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}
