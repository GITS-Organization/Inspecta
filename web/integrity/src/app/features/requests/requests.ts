import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { RequestDetails, RequestSummary, Triage } from '../../api/coordinator.models';
import { JobStatus } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { STAGE_LABEL, formatDate, plural } from '../../shared/format';

const STATUS_FILTERS: { value: string; label: string }[] = [
  { value: '', label: 'New requests' },
  { value: 'quote_draft', label: 'Quotation being prepared' },
  { value: 'quote_sent', label: 'Quotation sent' },
  { value: 'client_countered', label: 'Client replied' },
  { value: 'quote_rejected', label: 'Rejected' },
];

/** The request inbox: new requests, and the emails from unknown senders that wait for a decision. */
@Component({
  selector: 'app-requests',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="head">
      <div>
        <h1>Requests</h1>
        <p class="muted">Inspection requests from your clients' emails and the ones you enter yourself</p>
      </div>
      <a class="btn go" routerLink="/requests/new">New request</a>
    </div>

    <div class="tabs" role="tablist" aria-label="Request lists">
      <button type="button" role="tab" [attr.aria-selected]="triage() === 'request'" (click)="setTriage('request')">
        Requests @if (summary(); as s) { <span class="chip">{{ s.requests }}</span> }
      </button>
      <button type="button" role="tab" [attr.aria-selected]="triage() === 'maybe'" (click)="setTriage('maybe')">
        Unknown senders @if (summary(); as s) { <span class="chip" [class.a]="s.maybe > 0">{{ s.maybe }}</span> }
      </button>
    </div>

    <div class="toolbar">
      <div class="field">
        <label for="rq">Search</label>
        <input id="rq" type="text" placeholder="Client, location, subject or sender" [value]="query()" (input)="query.set($any($event.target).value)" (keydown.enter)="load()" />
      </div>
      @if (triage() === 'request') {
        <div class="field">
          <label for="rs">Show</label>
          <select id="rs" (change)="setStatus($any($event.target).value)">
            @for (f of filters; track f.value) {
              <option [value]="f.value" [selected]="f.value === status()">{{ f.label }}</option>
            }
          </select>
        </div>
      }
      <button type="button" class="btn ghost" (click)="load()">Search</button>
    </div>

    @if (triage() === 'maybe') {
      <p class="muted">These emails came from senders that are not yet attached to a client. Add the sender to a client under Clients and the email becomes a request by itself, or delete it. Unanswered ones are deleted after 7 days.</p>
    }

    <div aria-live="polite">
      @if (error(); as msg) {
        <div class="error" role="alert">{{ msg }}</div>
      } @else if (loading()) {
        <p class="muted empty">Loading requests…</p>
      } @else if (!rows().length) {
        <p class="empty">{{ triage() === 'maybe' ? 'No emails are waiting for a decision.' : 'No requests match. New requests appear here when a client emails you.' }}</p>
      } @else {
        @for (r of rows(); track r.id) {
          <div class="job">
            <div>
              <b>{{ r.clientName || r.emailFromName || r.emailFrom || 'No client yet' }}</b>
              <div class="muted">
                {{ r.subject || 'No subject' }} · {{ formatDate(r.receivedAt) }}
                @if (r.items.length) { · {{ plural(r.items.length, 'item') }} }
                @if (r.location) { · {{ r.location }} }
              </div>
              @if (r.missingFields.length && r.triage === 'request') {
                <div class="miss">Missing: {{ r.missingFields.join(', ') }}</div>
              }
            </div>
            <span class="rowline">
              @if (r.hasUnreadClientReply) { <span class="chip a">Client replied</span> }
              @if (r.triage === 'maybe') { <span class="chip a">Unknown sender</span> } @else { <span class="chip">{{ stage(r.status) }}</span> }
            </span>
            <a class="btn ghost" [routerLink]="['/requests', r.id]">Open</a>
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
export class Requests {
  private readonly api = inject(CoordinatorApi);
  protected readonly filters = STATUS_FILTERS;
  protected readonly formatDate = formatDate;
  protected readonly plural = plural;

  protected readonly triage = signal<Triage>('request');
  protected readonly status = signal('');
  protected readonly query = signal('');
  protected readonly rows = signal<RequestDetails[]>([]);
  protected readonly summary = signal<RequestSummary | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly stage = (s: JobStatus) => STAGE_LABEL[s] ?? s;

  constructor() {
    this.api.requestSummary().subscribe({ next: (s) => this.summary.set(s), error: () => undefined });
    this.load();
  }

  protected setTriage(t: Triage): void {
    this.triage.set(t);
    this.status.set('');
    this.load();
  }
  protected setStatus(v: string): void {
    this.status.set(v);
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.requests({ triage: this.triage(), status: this.status(), q: this.query().trim() }).subscribe({
      next: (r) => {
        this.rows.set(r);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The requests could not be loaded.'));
        this.loading.set(false);
      },
    });
  }
}
