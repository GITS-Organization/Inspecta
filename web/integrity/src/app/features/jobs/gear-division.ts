import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { BackendApi } from '../../api/backend.api';
import { CertificateShare, MachineList, MachineSummary } from '../../core/models';
import { apiMessage } from '../../core/problem';

/** One page holds about this many characters of serial numbers; the server's own answer replaces it. */
export const DEFAULT_CERTIFICATE_CHARACTERS = 1000;

interface Card {
  quantity: number;
  serials: string;
}

/** Cuts `text` to `limit` characters. `cut` says whether anything was lost. */
export function clampSerials(text: string, limit: number): { text: string; cut: boolean } {
  return text.length > limit ? { text: text.slice(0, limit), cut: true } : { text, cut: false };
}

/** The total the shares are compared with: what the gear was before it was divided, or its quantity now. */
export function divisionTotal(members: MachineSummary[]): number {
  return members[0]?.divisionTotal ?? members.reduce((a, m) => a + m.quantity, 0);
}

/**
 * The inspector divides a gear into certificates: how many, and for each one its Qty and its serial numbers written together.
 * A certificate is one page, so the serial text is limited by its number of characters and cannot go past it.
 */
@Component({
  selector: 'app-gear-division',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'closed.emit()' },
  template: `
    <div class="scrim" (click)="closed.emit()"></div>
    <div class="dlg" role="dialog" aria-modal="true" aria-labelledby="gd-title" #box>
      <header>
        <div>
          <h2 id="gd-title">Divide {{ members()[0].name }} into certificates</h2>
          <p class="muted">{{ total() }} in all. Each certificate is one page, with its own quantity and serial numbers.</p>
        </div>
        <button type="button" class="x" aria-label="Close" (click)="closed.emit()">×</button>
      </header>

      @if (error(); as msg) {
        <div class="error" role="alert">{{ msg }}</div>
      }

      <div class="field count">
        <label for="gd-n">How many certificates?</label>
        <input id="gd-n" type="number" min="1" step="1" [value]="cards().length" (input)="setCount($any($event.target).value)" />
      </div>

      @for (c of cards(); track $index) {
        <fieldset class="card">
          <legend>Certificate {{ $index + 1 }}</legend>
          <div class="field q">
            <label [for]="'gq' + $index">Qty</label>
            <input [id]="'gq' + $index" type="number" min="1" step="1" [value]="c.quantity" (input)="setQuantity($index, $any($event.target).value)" />
          </div>
          <div class="field s">
            <label [for]="'gs' + $index">Serial numbers</label>
            <textarea
              [id]="'gs' + $index"
              rows="3"
              placeholder="Written together, for example CS-0001-CS-0150, CS-0300"
              [value]="c.serials"
              [attr.aria-describedby]="'gc' + $index"
              (input)="setSerials($index, $event)"
            ></textarea>
            <div class="meter" [id]="'gc' + $index" [class.near]="c.serials.length >= limit() * 0.9" [class.full]="c.serials.length >= limit()">
              <span>{{ c.serials.length }} / {{ limit() }}</span>
              @if (cutAt() === $index) { <b role="status">The text was cut at {{ limit() }} characters: a certificate is one page.</b> }
            </div>
          </div>
        </fieldset>
      }

      <p class="sum" [class.diff]="sum() !== total()" role="status">
        <b>{{ sum() }}</b> of <b>{{ total() }}</b> added up.
        @if (sum() !== total()) { The quantities differ from the item's total. You can still save if that is what you found. }
      </p>

      <div class="actions">
        <button type="button" class="btn go" [disabled]="busy()" (click)="save()">{{ busy() ? 'Saving…' : 'Save certificates' }}</button>
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
      width: min(760px, 100%);
      max-height: calc(100vh - 32px);
      overflow-y: auto;
      background: #fff;
      border-radius: 12px;
      border-top: 6px solid var(--green);
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
        margin: 0 0 2px;
      }
      p {
        margin: 0;
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
    .count input {
      width: 120px;
    }
    .card {
      display: grid;
      grid-template-columns: 120px 1fr;
      gap: 0 16px;
      border: 1.5px solid var(--line);
      border-left: 5px solid var(--teal);
      border-radius: 8px;
      padding: 10px 16px 4px;
      margin: 0 0 12px;
      legend {
        font-family: var(--display);
        font-size: 20px;
        font-weight: 600;
        color: var(--ink);
        padding: 0 6px;
      }
      .field {
        margin-bottom: 10px;
      }
      textarea {
        min-height: 78px;
      }
    }
    .meter {
      display: flex;
      flex-wrap: wrap;
      gap: 4px 12px;
      margin-top: 4px;
      font-variant-numeric: tabular-nums;
      color: var(--muted);
      font-size: 13px;
      &.near {
        color: var(--amber);
        font-weight: 600;
      }
      &.full {
        color: var(--red);
        font-weight: 600;
      }
      b {
        color: var(--red);
      }
    }
    .sum {
      margin: 6px 0 0;
      padding: 10px 14px;
      border-radius: 8px;
      background: var(--green-bg);
      &.diff {
        background: var(--amber-bg);
        color: var(--amber);
      }
    }
    @media (max-width: 640px) {
      .card {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class GearDivision implements OnInit {
  readonly requestId = input.required<string>();
  /** The machines of the gear: one when it was never divided, or every certificate of its group. */
  readonly members = input.required<MachineSummary[]>();
  readonly done = output<MachineList>();
  readonly closed = output<void>();

  private readonly api = inject(BackendApi);
  private readonly box = viewChild<ElementRef<HTMLElement>>('box');

  protected readonly limit = signal(DEFAULT_CERTIFICATE_CHARACTERS);
  protected readonly cards = signal<Card[]>([]);
  protected readonly cutAt = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly total = computed(() => divisionTotal(this.members()));
  protected readonly sum = computed(() => this.cards().reduce((a, c) => a + c.quantity, 0));

  ngOnInit(): void {
    const ms = this.members();
    // A gear that was never divided starts as one certificate with all its pieces; a group starts as it was saved.
    this.cards.set(ms.length > 1 || ms[0].divisionGroupId ? ms.map((m) => ({ quantity: m.quantity, serials: m.serialNumber ?? '' })) : [{ quantity: ms[0].quantity, serials: ms[0].serialNumber ?? '' }]);
    this.api.serialLimits().subscribe({ next: (l) => l.maxCharactersPerCertificate > 0 && this.limit.set(l.maxCharactersPerCertificate), error: () => undefined });
    queueMicrotask(() => this.box()?.nativeElement.querySelector<HTMLElement>('#gd-n')?.focus());
  }

  protected setCount(v: string): void {
    const n = Math.max(1, Math.floor(Number(v)) || 1);
    this.cards.update((l) => (n <= l.length ? l.slice(0, n) : [...l, ...Array.from({ length: n - l.length }, () => ({ quantity: 1, serials: '' }))]));
  }

  protected setQuantity(i: number, v: string): void {
    const q = Math.max(1, Math.floor(Number(v)) || 1);
    this.cards.update((l) => l.map((c, x) => (x === i ? { ...c, quantity: q } : c)));
  }

  /** Typing or pasting past the limit is not possible: the extra text is cut off and the person is told. */
  protected setSerials(i: number, e: Event): void {
    const el = e.target as HTMLTextAreaElement;
    const { text, cut } = clampSerials(el.value, this.limit());
    if (cut) el.value = text;
    this.cutAt.set(cut ? i : this.cutAt() === i ? null : this.cutAt());
    this.cards.update((l) => l.map((c, x) => (x === i ? { ...c, serials: text } : c)));
  }

  protected save(): void {
    if (this.busy()) return;
    const shares: CertificateShare[] = this.cards().map((c) => ({ quantity: c.quantity, serialNumbers: c.serials.trim() }));
    const empty = shares.findIndex((s) => !s.serialNumbers);
    if (empty >= 0) return void this.error.set(`Write the serial numbers of certificate ${empty + 1}.`);
    this.error.set(null);
    this.busy.set(true);
    this.api.divideMachine(this.requestId(), this.members()[0].requestItemId, shares).subscribe({
      next: (list) => {
        this.busy.set(false);
        this.done.emit(list);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(apiMessage(e));
      },
    });
  }
}
