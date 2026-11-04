import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BackendApi } from '../../api/backend.api';
import { DateRangeService } from '../../core/date-range.service';
import { DateEmpty } from '../../shared/date-empty';
import { QueueRow } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { formatDate, plural } from '../../shared/format';
import { KindBadge } from '../../shared/kind-badge';

@Component({
  selector: 'app-review-list',
  imports: [RouterLink, KindBadge, DateEmpty],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="head">
      <div>
        <h1>To review</h1>
        <p class="muted">Jobs whose certificates are all submitted</p>
      </div>
    </div>
    @if (error(); as msg) {
      <div class="error" role="alert">{{ msg }}</div>
    } @else if (loading()) {
      <p class="muted empty" role="status">Loading jobs…</p>
    } @else if (!rows().length) {
      @if (range.active()) { <app-date-empty /> } @else { <p class="empty">Nothing is waiting for your review.</p> }
    } @else {
      @for (r of rows(); track r.id) {
        <div class="job">
          <div>
            <b>{{ r.clientName || 'Client' }}</b> <app-kind-badge [kind]="r.kind" />
            <div class="muted">
              {{ r.quotationNumber || r.proposalNumber }} · {{ r.location }} · {{ plural(r.totalUnits, 'machine') }}
              @if (r.inspectorName) { · sent by {{ r.inspectorName }} }
            </div>
          </div>
          <span class="chip a">Updated {{ formatDate(r.updatedAt) }}</span>
          <a class="btn" [routerLink]="['/review', r.requestId]">Review</a>
        </div>
      }
    }
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
    @media (max-width: 820px) {
      .job {
        grid-template-columns: 1fr;
        justify-items: start;
      }
    }
  `,
})
export class ReviewList {
  private readonly api = inject(BackendApi);
  protected readonly range = inject(DateRangeService);
  protected readonly plural = plural;
  protected readonly formatDate = formatDate;
  protected readonly rows = signal<QueueRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const from = this.range.from();
      const to = this.range.to();
      untracked(() => this.load(from, to));
    });
  }

  private load(from: string, to: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.toReview(from, to).subscribe({
      next: (r) => {
        this.rows.set(r);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The jobs could not be loaded.'));
        this.loading.set(false);
      },
    });
  }
}
