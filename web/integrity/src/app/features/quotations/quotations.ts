import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { QuoteRow, QuoteStage } from '../../api/coordinator.models';
import { problemMessage } from '../../core/problem';
import { formatDate, formatEgp } from '../../shared/format';

const TABS: { stage: QuoteStage; label: string }[] = [
  { stage: 'draft', label: 'Being prepared' },
  { stage: 'sent', label: 'Sent to the client' },
  { stage: 'accepted', label: 'Accepted' },
  { stage: 'rejected', label: 'Rejected' },
];

/** All quotations by stage. The editor opens from a row. */
@Component({
  selector: 'app-quotations',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="head">
      <div>
        <h1>Quotations</h1>
        <p class="muted">Prepare, send and follow up the offers to your clients</p>
      </div>
    </div>

    <div class="tabs" role="tablist" aria-label="Quotation stages">
      @for (t of tabs; track t.stage) {
        <button type="button" role="tab" [attr.aria-selected]="t.stage === stage()" (click)="select(t.stage)">{{ t.label }}</button>
      }
    </div>

    <div class="toolbar">
      <div class="field">
        <label for="qq">Search</label>
        <input id="qq" type="text" placeholder="Client, quotation number or location" [value]="query()" (input)="query.set($any($event.target).value)" (keydown.enter)="load()" />
      </div>
      <button type="button" class="btn ghost" (click)="load()">Search</button>
    </div>

    <div aria-live="polite">
      @if (error(); as msg) {
        <div class="error" role="alert">{{ msg }}</div>
      } @else if (loading()) {
        <p class="muted empty">Loading quotations…</p>
      } @else if (!rows().length) {
        <p class="empty">{{ emptyText() }}</p>
      } @else {
        @for (r of rows(); track r.id) {
          <div class="job">
            <div>
              <b>{{ r.clientName || 'Client' }}</b>
              <div class="muted">
                {{ r.quotationNumber || r.proposalNumber }} · {{ r.location }} · {{ r.totalUnits }} {{ r.totalUnits === 1 ? 'unit' : 'units' }}
                @if (r.amountPiastres !== null) { · {{ egp(r.amountPiastres) }} }
              </div>
              @if (r.missingPrices && r.stage === 'draft') { <div class="miss">{{ r.missingPrices }} {{ r.missingPrices === 1 ? 'price' : 'prices' }} missing</div> }
            </div>
            <span class="rowline">
              @if (r.hasUnreadClientReply) { <span class="chip a">Client replied</span> }
              @if (r.requestStatus === 'counter_offer_sent') { <span class="chip">Counter offer sent</span> }
              @if (r.version > 1) { <span class="chip">Revision {{ r.version }}</span> }
              @if (r.stage === 'sent' && r.isExpired) { <span class="chip r">Expired</span> }
              @else if (r.stage === 'sent') { <span class="chip">Valid until {{ formatDate(r.validUntil) }}</span> }
            </span>
            <a class="btn" [class.ghost]="r.stage !== 'draft'" [routerLink]="['/quotations', r.requestId, r.id]">{{ r.stage === 'draft' ? 'Continue' : 'Open' }}</a>
          </div>
        }
      }
    </div>
  `,
  styles: `
    .miss {
      color: var(--amber);
      font-size: 13px;
    }
  `,
})
export class Quotations {
  private readonly api = inject(CoordinatorApi);
  protected readonly tabs = TABS;
  protected readonly formatDate = formatDate;
  protected readonly egp = formatEgp;
  protected readonly stage = signal<QuoteStage>('draft');
  protected readonly query = signal('');
  protected readonly rows = signal<QuoteRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected emptyText(): string {
    return { draft: 'No quotation is being prepared. Prepare one from a request.', sent: 'No quotation is waiting for a client answer.', accepted: 'No accepted quotations yet.', rejected: 'No rejected quotations.' }[this.stage()];
  }

  protected select(s: QuoteStage): void {
    this.stage.set(s);
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    const wanted = this.stage();
    this.api.quotations({ stage: wanted, q: this.query().trim() }).subscribe({
      next: (r) => {
        if (wanted !== this.stage()) return;
        this.rows.set(r);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The quotations could not be loaded.'));
        this.loading.set(false);
      },
    });
  }
}
