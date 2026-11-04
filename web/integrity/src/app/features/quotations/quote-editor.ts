import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { CoordinatorApi } from '../../api/coordinator.api';
import { openBlob } from '../../api/backend.api';
import { Quote, QuoteEmailPreview, QuoteItem, SendEmail, UpdateQuote } from '../../api/coordinator.models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { EmailComposer } from '../../shared/email-composer';
import { egpToPiastres, formatDate, formatEgp, lines } from '../../shared/format';

type Mode = 'send' | 'accept' | 'decide' | null;

const toPounds = (p: number | null) => (p === null ? '' : String(p / 100));

/** One quotation: prices, the offer text, the email to the client, and what comes back (replies, acceptance, rejection). */
@Component({
  selector: 'app-quote-editor',
  imports: [RouterLink, EmailComposer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './quote-editor.html',
  styleUrl: './quote-editor.scss',
})
export class QuoteEditor {
  readonly requestId = input.required<string>();
  readonly quoteId = input.required<string>();

  private readonly api = inject(CoordinatorApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly formatDate = formatDate;
  protected readonly egp = formatEgp;

  protected readonly quote = signal<Quote | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly prices = signal<Record<string, string>>({});
  /** Price of ONE man day in pounds, as typed. */
  protected readonly manDayPrice = signal('');
  protected readonly validFor = signal(14);
  protected readonly proposalNumber = signal('');
  protected readonly contact = signal('');
  protected readonly scope = signal('');
  protected readonly notes = signal('');
  protected readonly message = signal('');

  protected readonly mode = signal<Mode>(null);
  protected readonly preview = signal<QuoteEmailPreview | null>(null);
  protected readonly decision = signal<'accepted' | 'rejected'>('rejected');
  protected readonly decisionNote = signal('');
  protected readonly noteMissing = signal(false);

  protected readonly editable = computed(() => !!this.quote()?.canEdit);
  protected readonly isSent = computed(() => {
    const q = this.quote();
    return !!q && q.status === 'open' && !!q.sentAt && !q.canEdit;
  });
  protected readonly replies = computed(() => this.quote()?.clientReplies ?? []);
  /** The man day line has no request item: it is told apart by its type, never mapped by an id. */
  protected readonly isManDay = (it: QuoteItem): boolean => !it.requestItemId && it.type === 'Man day';
  protected readonly manDayLine = computed(() => (this.quote()?.items ?? []).find((i) => this.isManDay(i)) ?? null);
  protected readonly unpricedManDay = computed(() => !!this.manDayLine() && egpToPiastres(this.manDayPrice()) === null);
  /** The unit price of a line as it stands on screen (what was typed while the quotation is editable). */
  private unit(it: QuoteItem): number | null {
    if (this.isManDay(it)) return this.editable() ? egpToPiastres(this.manDayPrice()) : it.unitPricePiastres;
    return it.requestItemId ? egpToPiastres(this.prices()[it.requestItemId] ?? '') : it.unitPricePiastres;
  }
  protected readonly lineTotal = (it: QuoteItem): string => {
    const p = this.unit(it);
    return p === null ? '—' : formatEgp(p * it.quantity);
  };
  protected readonly total = computed(() => (this.quote()?.items ?? []).reduce((a, it) => a + (this.unit(it) ?? 0) * it.quantity, 0));
  /** Lines without a price: the machines and the man day line. */
  protected readonly missing = computed(() => (this.quote()?.items ?? []).filter((it) => this.unit(it) === null).length);

  constructor() {
    effect(() => {
      const r = this.requestId();
      const q = this.quoteId();
      untracked(() => this.load(r, q));
    });
  }

  private load(r: string, q: string): void {
    this.loading.set(true);
    this.mode.set(null);
    this.api.quote(r, q).subscribe({
      next: (x) => {
        this.apply(x);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The quotation could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  private apply(q: Quote): void {
    this.quote.set(q);
    this.prices.set(Object.fromEntries((q.items ?? []).filter((i) => i.requestItemId).map((i) => [i.requestItemId!, toPounds(i.unitPricePiastres)])));
    this.manDayPrice.set(toPounds((q.items ?? []).find((i) => !i.requestItemId && i.type === 'Man day')?.unitPricePiastres ?? null));
    this.validFor.set(q.validForDays);
    this.proposalNumber.set(q.proposalNumber);
    this.contact.set(q.contactName ?? '');
    this.scope.set((q.scopeOfWork ?? []).join('\n'));
    this.notes.set((q.notes ?? []).join('\n'));
    this.message.set(q.message ?? '');
  }

  protected setPrice(id: string | null, v: string): void {
    if (id) this.prices.update((p) => ({ ...p, [id]: v }));
  }

  private body(): UpdateQuote {
    const q = this.quote()!;
    return {
      ...(this.manDayLine() && egpToPiastres(this.manDayPrice()) !== null ? { manDayPricePiastres: egpToPiastres(this.manDayPrice()) } : {}),
      items: (q.items ?? []).filter((i) => i.requestItemId).map((i) => ({ requestItemId: i.requestItemId!, unitPricePiastres: egpToPiastres(this.prices()[i.requestItemId!] ?? '') })),
      validForDays: this.validFor(),
      scopeOfWork: lines(this.scope()),
      notes: lines(this.notes()),
      message: this.message() || null,
      proposalNumber: this.proposalNumber().trim() || null,
      contactName: this.contact().trim() || null,
    };
  }

  private call<T>(c: Observable<T>, ok: (v: T) => void): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    c.subscribe({
      next: (v) => {
        this.busy.set(false);
        ok(v);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }

  protected save(): void {
    this.call(this.api.saveQuote(this.requestId(), this.quoteId(), this.body()), (q) => {
      this.apply(q);
      this.toast.show('Saved.');
    });
  }

  protected pdf(): void {
    const open = () => this.call(this.api.quotePdf(this.requestId(), this.quoteId()), (b) => openBlob(b));
    if (!this.editable()) return open();
    this.call(this.api.saveQuote(this.requestId(), this.quoteId(), this.body()), (q) => {
      this.apply(q);
      open();
    });
  }

  protected compose(action: 'send' | 'accept'): void {
    const show = () => {
      this.mode.set(action);
      this.preview.set(null);
      this.api.quoteEmail(this.requestId(), this.quoteId(), action).subscribe({
        next: (p) => this.preview.set(p),
        error: (e: unknown) => this.error.set(problemMessage(e)),
      });
    };
    if (!this.editable()) return show();
    this.call(this.api.saveQuote(this.requestId(), this.quoteId(), this.body()), (q) => {
      this.apply(q);
      show();
    });
  }

  protected sendEmail(email: SendEmail): void {
    const action = this.mode();
    const c = action === 'accept' ? this.api.acceptQuote(this.requestId(), this.quoteId(), email) : this.api.sendQuote(this.requestId(), this.quoteId(), email);
    this.call(c, (q) => {
      this.mode.set(null);
      this.apply(q);
      this.toast.show(action === 'accept' ? 'Accepted. The job is waiting for an inspector.' : q.version > 1 ? 'The counter offer was sent.' : 'The quotation was sent.');
      void this.router.navigate([action === 'accept' ? '/queue' : '/quotations']);
    });
  }

  protected record(): void {
    if (this.decision() === 'rejected' && !this.decisionNote().trim()) {
      this.noteMissing.set(true);
      return;
    }
    this.call(this.api.decideQuote(this.requestId(), this.quoteId(), this.decision(), this.decisionNote().trim()), (q) => {
      this.mode.set(null);
      this.apply(q);
      this.toast.show(this.decision() === 'accepted' ? 'Recorded as accepted.' : 'Recorded as rejected.');
      void this.router.navigate(['/quotations']);
    });
  }

  protected reopen(): void {
    this.call(this.api.reopenQuote(this.requestId(), this.quoteId()), (q) => {
      this.toast.show('Reopened as a new revision. Change the offer and send it again.');
      void this.router.navigate(['/quotations', q.requestId, q.id]);
    });
  }

  protected dismiss(): void {
    this.call(this.api.dismissReply(this.requestId(), this.quoteId()), (q) => {
      this.apply(q);
      this.toast.show('Reply cleared. Waiting for the client.');
    });
  }
}
