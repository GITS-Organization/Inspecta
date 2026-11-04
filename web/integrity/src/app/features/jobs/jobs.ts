import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BackendApi } from '../../api/backend.api';
import { AuthService } from '../../core/auth.service';
import { QueueFilter, QueueRow } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { plural } from '../../shared/format';
import { KindBadge } from '../../shared/kind-badge';

interface Tab {
  filter: QueueFilter;
  label: string;
  chip?: string;
}

const TABS: Record<'inspector' | 'coordinator', Tab[]> = {
  inspector: [
    { filter: 'assigned', label: 'To inspect' },
    { filter: 'returned', label: 'Returned', chip: 'r' },
    { filter: 'review', label: 'Submitted' },
  ],
  coordinator: [
    { filter: 'approved', label: 'Ready to hand over' },
    { filter: 'delivered', label: 'Delivered' },
  ],
};

@Component({
  selector: 'app-jobs',
  imports: [RouterLink, KindBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="head">
      <div>
        <h1>{{ isInspector() ? 'My jobs' : 'Jobs' }}</h1>
        <p class="muted">{{ isInspector() ? 'Inspections assigned to you' : 'Certificates your team has finished' }}</p>
      </div>
      @if (isInspector()) {
        <a class="btn ghost" routerLink="/create">Create certification</a>
      }
    </div>

    <div class="tabs" role="tablist" aria-label="Job lists">
      @for (t of tabs(); track t.filter) {
        <button type="button" role="tab" [id]="'tab-' + t.filter" [attr.aria-selected]="t.filter === active()" [attr.aria-controls]="'jobs-panel'" (click)="select(t.filter)">
          {{ t.label }}
          @if (t.filter === active() && !loading() && rows().length) {
            <span class="chip" [class.r]="t.chip === 'r'">{{ rows().length }}</span>
          }
        </button>
      }
    </div>

    <div id="jobs-panel" role="tabpanel" [attr.aria-labelledby]="'tab-' + active()" aria-live="polite">
      @if (error(); as msg) {
        <div class="error" role="alert">{{ msg }} <button class="btn ghost small" type="button" (click)="load()">Try again</button></div>
      } @else if (loading()) {
        <p class="muted empty">Loading jobs…</p>
      } @else if (!rows().length) {
        <p class="empty">{{ emptyText() }}</p>
      } @else {
        @for (r of rows(); track r.id) {
          <div class="job">
            <div>
              <b>{{ r.clientName || 'Client' }}</b> <app-kind-badge [kind]="r.kind" />
              <div class="muted">
                {{ r.quotationNumber || r.proposalNumber }} · {{ r.location }} · {{ plural(r.totalUnits, 'machine') }}
              </div>
            </div>
            <span class="chip" [class]="statusChip(r)">{{ statusText(r) }}</span>
            <a class="btn" [class.ghost]="r.requestStatus !== 'assigned'" [routerLink]="['/jobs', r.requestId]">{{ actionText(r) }}</a>
          </div>
        }
      }
    </div>
  `,
  styles: `
    .job {
      display: grid;
      grid-template-columns: 1fr auto auto;
      gap: 18px;
      align-items: center;
      padding: 16px 0;
      border-bottom: 1px solid var(--line);
      b {
        color: var(--ink);
        font-size: 16px;
      }
    }
    .tabs .chip {
      margin-left: 4px;
    }
    @media (max-width: 820px) {
      .job {
        grid-template-columns: 1fr;
        justify-items: start;
      }
    }
  `,
})
export class Jobs {
  private readonly api = inject(BackendApi);
  private readonly auth = inject(AuthService);
  protected readonly plural = plural;

  protected readonly isInspector = computed(() => this.auth.role() === 'inspector');
  protected readonly tabs = computed(() => TABS[this.isInspector() ? 'inspector' : 'coordinator']);
  protected readonly active = signal<QueueFilter>(TABS[this.auth.role() === 'inspector' ? 'inspector' : 'coordinator'][0].filter);
  protected readonly rows = signal<QueueRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly emptyText = computed(() => {
    switch (this.active()) {
      case 'assigned':
        return 'No inspections are waiting for you.';
      case 'returned':
        return 'Nothing was sent back to you.';
      case 'review':
        return 'No jobs are waiting for the technical manager.';
      case 'approved':
        return 'No approved certificates are waiting to be handed over.';
      default:
        return 'Nothing here yet.';
    }
  });

  constructor() {
    this.load();
  }

  protected select(f: QueueFilter): void {
    if (f === this.active()) return;
    this.active.set(f);
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    const wanted = this.active();
    this.api.queue(wanted).subscribe({
      next: (rows) => {
        if (wanted !== this.active()) return; // a newer tab was chosen meanwhile
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The jobs could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  protected statusText(r: QueueRow): string {
    const p = r.certificates;
    switch (r.requestStatus) {
      case 'certificate_returned':
        return 'Returned with a note';
      case 'certificate_submitted':
        return 'Waiting for review';
      case 'certificate_approved':
        return 'Approved';
      case 'sent_to_client':
        return 'Delivered';
      case 'assigned':
      case 'task_accepted':
        return 'Not started';
      default:
        return p && p.total ? `${p.submitted} of ${p.total} submitted` : 'In progress';
    }
  }

  protected statusChip(r: QueueRow): string {
    return r.requestStatus === 'certificate_returned' ? 'chip r' : r.requestStatus === 'in_progress' ? 'chip a' : r.requestStatus === 'certificate_approved' || r.requestStatus === 'sent_to_client' ? 'chip g' : 'chip';
  }

  protected actionText(r: QueueRow): string {
    switch (r.requestStatus) {
      case 'assigned':
      case 'task_accepted':
        return 'Start inspection';
      case 'certificate_returned':
        return 'Fix and resubmit';
      default:
        return 'Open';
    }
  }
}
