import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { CoordinatorApi } from '../api/coordinator.api';
import { Inspector, RequestDetails } from '../api/coordinator.models';
import { apiMessage } from '../core/problem';
import { defaultManDayStart } from '../features/requests/man-day-new';
import { formatDate } from './format';

/** A client with a contract goes straight to an inspector: choose who and when. No quotation is sent. */
@Component({
  selector: 'app-assign-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'closed.emit()' },
  template: `
    <div class="scrim" (click)="closed.emit()"></div>
    <div class="dlg" role="dialog" aria-modal="true" aria-labelledby="ad-title" #box>
      <header>
        <div>
          <h2 id="ad-title">Assign an inspector</h2>
          <p class="muted">{{ clientName() }} · contract client</p>
        </div>
        <button type="button" class="x" aria-label="Close" (click)="closed.emit()">×</button>
      </header>
      @if (error(); as msg) {
        <div class="error" role="alert">{{ msg }}</div>
      }
      <div class="field">
        <label for="ad-i">Inspector</label>
        <select id="ad-i" (change)="inspectorId.set($any($event.target).value)">
          <option value="">Choose an inspector</option>
          @for (i of inspectors(); track i.id) {
            <option [value]="i.id" [selected]="i.id === inspectorId()">{{ label(i) }}</option>
          }
        </select>
        @if (picked(); as p) {
          @if (p.isOnLeave) { <small class="leave">{{ p.fullName }} is on leave{{ p.leaveUntil ? ' until ' + formatDate(p.leaveUntil) : '' }}. The API will refuse the job.</small> }
        }
      </div>
      <div class="field">
        <label for="ad-w">Date and time</label>
        <input id="ad-w" type="datetime-local" [value]="when()" (input)="when.set($any($event.target).value)" />
      </div>
      <div class="field">
        <label for="ad-n">Note for the inspector (optional)</label>
        <input id="ad-n" type="text" maxlength="2000" [value]="note()" (input)="note.set($any($event.target).value)" />
      </div>
      <p class="plain" role="note"><b>No quotation will be sent.</b> The job goes to the inspector now, who adds the machines found on site. You can still prepare a quotation instead by closing this window.</p>
      <div class="actions">
        <button type="button" class="btn go" [disabled]="busy()" (click)="confirm()">{{ busy() ? 'Assigning…' : 'Assign without a quotation' }}</button>
        <button type="button" class="btn ghost" (click)="closed.emit()">Cancel</button>
      </div>
    </div>
  `,
  styles: `
    :host {
      position: fixed;
      inset: 0;
      z-index: 40;
      display: grid;
      place-items: center;
      padding: 16px;
    }
    .scrim {
      position: absolute;
      inset: 0;
      background: rgb(19 40 93 / 0.55);
    }
    .dlg {
      position: relative;
      width: min(560px, 100%);
      max-height: calc(100vh - 32px);
      overflow-y: auto;
      background: #fff;
      border-radius: 12px;
      border-top: 6px solid var(--teal);
      padding: 22px 26px 24px;
      box-shadow: 0 24px 60px rgb(19 40 93 / 0.35);
    }
    header {
      display: flex;
      justify-content: space-between;
      align-items: start;
      gap: 12px;
      margin-bottom: 14px;
      h2 {
        font-size: 28px;
        margin: 0;
      }
      p {
        margin: 2px 0 0;
      }
    }
    .x {
      background: none;
      border: 0;
      font-size: 30px;
      line-height: 1;
      cursor: pointer;
      color: var(--muted);
      padding: 0 6px;
    }
    .leave {
      color: var(--amber) !important;
      font-weight: 600;
    }
    .plain {
      background: var(--band);
      border-left: 4px solid var(--teal);
      border-radius: 6px;
      padding: 10px 14px;
      margin: 0 0 4px;
    }
  `,
})
export class AssignDialog implements OnInit {
  readonly requestId = input.required<string>();
  readonly clientName = input('');
  readonly done = output<RequestDetails>();
  readonly closed = output<void>();

  private readonly api = inject(CoordinatorApi);
  private readonly box = viewChild<ElementRef<HTMLElement>>('box');
  protected readonly formatDate = formatDate;
  protected readonly inspectors = signal<Inspector[]>([]);
  protected readonly inspectorId = signal('');
  protected readonly when = signal(defaultManDayStart());
  protected readonly note = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly picked = computed(() => this.inspectors().find((i) => i.id === this.inspectorId()) ?? null);

  ngOnInit(): void {
    this.api.inspectors().subscribe({ next: (i) => this.inspectors.set(i), error: (e: unknown) => this.error.set(apiMessage(e, 'The inspectors could not be loaded.')) });
    queueMicrotask(() => this.box()?.nativeElement.querySelector<HTMLElement>('select')?.focus());
  }

  protected label(i: Inspector): string {
    return `${i.fullName} · ${i.activeJobs} active${i.isOnLeave ? ' · on leave' + (i.leaveUntil ? ' until ' + formatDate(i.leaveUntil) : '') : ''}`;
  }

  protected confirm(): void {
    if (this.busy()) return;
    if (!this.inspectorId()) return void this.error.set('Choose an inspector.');
    if (!this.when() || isNaN(new Date(this.when()).getTime())) return void this.error.set('Choose the date and time of the inspection.');
    this.error.set(null);
    this.busy.set(true);
    this.api.assignWithoutQuotation(this.requestId(), this.inspectorId(), new Date(this.when()).toISOString(), this.note().trim()).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.done.emit(r);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(apiMessage(e));
      },
    });
  }
}
