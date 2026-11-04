import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { CoordinatorApi } from '../../api/coordinator.api';
import { DeliveryPreview, SendEmail } from '../../api/coordinator.models';
import { openBlob, saveBlob } from '../../api/backend.api';
import { MachineList, MachineSummary } from '../../core/models';
import { apiMessage, problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { ComposerPreview, EmailComposer } from '../../shared/email-composer';
import { RESULT_LABEL, formatDate, plural, resultChip } from '../../shared/format';

/** Hands the approved certificates to the client: preview the email, send a test copy, send it, or mark the job handed over by hand. */
@Component({
  selector: 'app-deliver',
  imports: [RouterLink, EmailComposer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="back" routerLink="/queue">Back to jobs</a>
    @if (error(); as msg) {
      <div class="error" role="alert">{{ msg }}</div>
    }
    @if (job(); as j) {
      <div class="head">
        <div>
          <span class="chip" [class.g]="delivered()">{{ delivered() ? 'Delivered' : 'Ready to hand over' }}</span>
          <h1 class="title">{{ j.clientName }}</h1>
          <p class="muted">{{ j.location }} · {{ plural(j.machines.length, 'certificate') }}</p>
        </div>
        <button type="button" class="btn ghost" [disabled]="busy()" (click)="zip()">Download all (ZIP)</button>
      </div>

      @if (delivered()) {
        <div class="callout ok">
          <b>The certificates were handed to the client.</b>
          @if (j.delivery; as d) {
            <p>
              {{ d.emailSent ? 'Emailed to ' + d.sentTo : 'Handed over without an email' + (d.sentTo ? ' to ' + d.sentTo : '') }}
              @if (d.sentAt) { on {{ formatDate(d.sentAt) }} }.
              @if (d.notice) { {{ d.notice }} }
            </p>
          }
        </div>
      }

      @if (pending(); as p) {
        <div class="callout warn" role="alert">
          <b>The certificate details have changes that are not saved.</b>
          <p>If you deliver now, the certificates go out with the numbers and dates they have today.</p>
          <div class="rowline">
            <button type="button" class="btn go" [disabled]="busy()" (click)="saveThen(p)">Save and deliver</button>
            <button type="button" class="btn ghost" [disabled]="busy()" (click)="deliverNow(p)">Deliver without saving</button>
            <button type="button" class="btn ghost" (click)="pending.set(null)">Cancel</button>
          </div>
        </div>
      }

      <div class="two">
        <div>
          @if (!delivered()) {
            <section class="panel first" aria-labelledby="dm">
              <h2 id="dm">The email to the client</h2>
              <app-email-composer
                [preview]="composer()"
                [busy]="busy()"
                [showTest]="!!preview()?.emailEnabled"
                attachLabel="certificates"
                sendLabel="Send the certificates"
                (send)="send($event)"
                (test)="test($event)"
              />
            </section>

            <section class="panel" aria-labelledby="mn">
              <h2 id="mn">Handed over another way</h2>
              <p class="muted">Given on paper, by chat or picked up? Mark the job as delivered. No email is sent.</p>
              <div class="row2">
                <div class="field">
                  <label for="dt">Who received them</label>
                  <input id="dt" type="text" [value]="deliveredTo()" (input)="deliveredTo.set($any($event.target).value)" />
                </div>
                <div class="field">
                  <label for="dn">Note</label>
                  <input id="dn" type="text" [value]="note()" (input)="note.set($any($event.target).value)" placeholder="For example: collected by the site manager" />
                </div>
              </div>
              <button type="button" class="btn ghost" [disabled]="busy()" (click)="manual()">Mark as delivered</button>
            </section>
          }
        </div>

        <aside class="stick">
          <section class="panel first">
            <h2>Certificates</h2>
            <p class="muted sm helper">Printed at the top of each certificate. The examination date is not changed.</p>
            @if (!editable()) {
              <p class="locked" role="note">Already handed over: the certificate details can no longer be changed.</p>
            }
            @if (detailError(); as msg) {
              <div class="error" role="alert">{{ msg }}</div>
            }
            @for (m of rows(); track m.requestItemId) {
              @let d = draft(m);
              @let bad = problem(m);
              <div class="cert" [class.changed]="changed(m)">
                <div class="top">
                  <div>
                    <b>{{ m.name }}</b>
                    @if (changed(m)) { <span class="chip a mark">Changed</span> }
                    @let c = preCert(m);
                    @if (c) {
                      <div class="muted sm">{{ formatDate(c.examinationDate) }} → {{ formatDate(c.nextExaminationDate) }}@if (c.result) { · {{ resultLabel[c.result] }} }</div>
                    }
                  </div>
                  <button type="button" class="btn ghost small" [disabled]="!m.pdfUrl" (click)="pdf(m.pdfUrl!)">Preview</button>
                </div>
                <div class="fields">
                  <div class="field">
                    <label [for]="'cn' + m.requestItemId">Certificate No.</label>
                    <input [id]="'cn' + m.requestItemId" type="text" maxlength="64" [readOnly]="!editable()" [value]="d.number" [attr.aria-invalid]="bad ? 'true' : null" (input)="setNumber(m, $any($event.target).value)" />
                  </div>
                  <div class="field">
                    <label [for]="'cd' + m.requestItemId">Certificate Date</label>
                    <input [id]="'cd' + m.requestItemId" type="date" min="2020-01-01" [max]="today" [readOnly]="!editable()" [value]="d.date" [attr.aria-invalid]="bad ? 'true' : null" (input)="setDate(m, $any($event.target).value)" />
                  </div>
                </div>
                @if (bad && editable()) { <div class="bad" role="status">{{ bad }}</div> }
              </div>
            } @empty {
              <p class="muted">The certificates appear once the job is loaded.</p>
            }
            @if (editable()) {
              <div class="savebar">
                <button type="button" class="btn go" [disabled]="busy() || !dirtyCount() || invalid()" (click)="saveDetails()">Save certificate details</button>
                @if (dirtyCount()) { <span class="muted sm">{{ plural(dirtyCount(), 'certificate') }} changed</span> }
              </div>
            }
          </section>
        </aside>
      </div>
    } @else if (loading()) {
      <p class="muted empty" role="status">Loading the job…</p>
    }
  `,
  styles: `
    .title {
      margin-top: 8px;
    }
    .first {
      margin-top: 0;
    }
    .sm {
      font-size: 13px;
    }
    .cert {
      padding: 12px 0;
      border-bottom: 1px solid var(--line);
      .top {
        display: flex;
        justify-content: space-between;
        gap: 10px;
        align-items: start;
      }
      .fields {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 10px;
        margin-top: 8px;
      }
      .field {
        margin: 0;
      }
      input[readonly] {
        background: var(--band);
      }
      &.changed {
        box-shadow: inset 4px 0 0 var(--amber);
        padding-left: 10px;
      }
    }
    .mark {
      margin-left: 6px;
    }
    .helper {
      margin: 0 0 6px;
    }
    .locked {
      background: var(--band);
      border-radius: 8px;
      padding: 8px 12px;
      margin: 0 0 8px;
    }
    .bad {
      color: var(--red);
      font-weight: 600;
      font-size: 13px;
      margin-top: 6px;
    }
    .savebar {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
      margin-top: 14px;
    }
    .callout.warn {
      border-left-color: var(--amber);
      background: var(--amber-bg);
    }
    @media (max-width: 520px) {
      .cert .fields {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class Deliver implements OnInit {
  readonly requestId = input.required<string>();

  private readonly api = inject(CoordinatorApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly formatDate = formatDate;
  protected readonly plural = plural;
  protected readonly resultLabel = RESULT_LABEL;
  protected readonly resultChip = resultChip;

  protected readonly job = signal<(MachineList & { delivery?: { emailSent: boolean; sentTo: string | null; notice: string | null; sentAt: string | null } | null }) | null>(null);
  protected readonly preview = signal<DeliveryPreview | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly deliveredTo = signal('');
  protected readonly note = signal('');
  protected readonly today = new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 10);
  protected readonly detailError = signal<string | null>(null);
  /** What the coordinator typed, by certificate; a certificate not in here shows what the API sent. */
  private readonly edits = signal<Record<string, { number?: string; date?: string }>>({});
  /** A delivery that waits for the answer to "you have unsaved changes". */
  protected readonly pending = signal<(() => void) | null>(null);

  /** The approved certificates of the job (the machines that have a certificate number). */
  protected readonly rows = computed(() => (this.job()?.machines ?? []).filter((m) => !!m.certificateNumber));
  protected readonly editable = computed(() => !!this.job()?.canEditCertificateDetails && !this.delivered());
  protected draft(m: MachineSummary): { number: string; date: string } {
    const e = this.edits()[m.requestItemId];
    return { number: e?.number ?? m.certificateNumber ?? '', date: e?.date ?? m.certificateDate ?? '' };
  }
  protected changed(m: MachineSummary): boolean {
    const d = this.draft(m);
    return d.number.trim() !== (m.certificateNumber ?? '') || d.date !== (m.certificateDate ?? '');
  }
  protected readonly dirty = computed(() => this.rows().filter((m) => this.changed(m)));
  protected readonly dirtyCount = computed(() => this.dirty().length);
  /** Why a certificate cannot be saved as it stands; empty when it is fine. */
  protected problem(m: MachineSummary): string {
    const d = this.draft(m);
    const n = d.number.trim();
    if (!n) return 'Write the certificate number.';
    if (n.length > 64) return 'The number can have 64 characters at most.';
    if (this.rows().some((o) => o.requestItemId !== m.requestItemId && this.draft(o).number.trim().toLowerCase() === n.toLowerCase())) return 'Another certificate of this job has the same number.';
    if (!d.date) return 'Choose the certificate date.';
    if (d.date < '2020-01-01') return 'The date cannot be before 2020.';
    if (d.date > this.today) return 'The date cannot be in the future.';
    return '';
  }
  protected readonly invalid = computed(() => this.rows().some((m) => !!this.problem(m)));
  protected preCert(m: MachineSummary) {
    return this.certs().find((c) => c.requestItemId === m.requestItemId);
  }
  protected setNumber(m: MachineSummary, v: string): void {
    this.edits.update((e) => ({ ...e, [m.requestItemId]: { ...e[m.requestItemId], number: v } }));
  }
  protected setDate(m: MachineSummary, v: string): void {
    this.edits.update((e) => ({ ...e, [m.requestItemId]: { ...e[m.requestItemId], date: v } }));
  }

  protected readonly delivered = computed(() => this.job()?.requestStatus === 'sent_to_client');
  protected readonly certs = computed(() => this.preview()?.certificates ?? []);
  protected readonly composer = computed<ComposerPreview | null>(() => {
    const p = this.preview();
    return p ? { ...p, attachmentNames: p.attachments } : null;
  });

  ngOnInit(): void {
    this.api.machines(this.requestId()).subscribe({
      next: (j) => {
        this.job.set(j);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The job could not be loaded.'));
        this.loading.set(false);
      },
    });
    this.api.deliveryPreview(this.requestId()).subscribe({ next: (p) => this.preview.set(p), error: (e: unknown) => this.error.set(problemMessage(e)) });
  }

  /** Saves the numbers and dates that changed, in one call; the server draws each changed certificate again, so the previews are asked for again. */
  protected saveDetails(after?: () => void): void {
    if (this.busy() || this.invalid() || !this.dirtyCount()) return;
    this.busy.set(true);
    this.detailError.set(null);
    const body = this.dirty().map((m) => {
      const d = this.draft(m);
      return { requestItemId: m.requestItemId, certificateNumber: d.number.trim(), certificateDate: d.date };
    });
    this.api.saveCertificateDetails(this.requestId(), body).subscribe({
      next: (list) => {
        this.busy.set(false);
        this.job.set(list);
        this.edits.set({});
        this.api.deliveryPreview(this.requestId()).subscribe({ next: (p) => this.preview.set(p), error: () => undefined });
        this.toast.show('Saved. The certificates were updated.');
        after?.();
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.detailError.set(apiMessage(e));
      },
    });
  }

  /** Delivering with unsaved certificate details asks first. */
  private guard(action: () => void): void {
    if (this.dirtyCount() > 0) this.pending.set(action);
    else action();
  }
  protected saveThen(action: () => void): void {
    this.pending.set(null);
    this.saveDetails(action);
  }
  protected deliverNow(action: () => void): void {
    this.pending.set(null);
    action();
  }

  protected pdf(url: string): void {
    this.api.file(url).subscribe({ next: (b) => openBlob(b), error: (e: unknown) => this.error.set(problemMessage(e)) });
  }

  protected zip(): void {
    this.api.certificatesZip(this.requestId()).subscribe({ next: (b) => saveBlob(b, 'certificates.zip'), error: (e: unknown) => this.error.set(problemMessage(e)) });
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

  protected send(email: SendEmail): void {
    this.guard(() =>
      this.call(this.api.deliver(this.requestId(), email), () => {
        this.toast.show('The certificates were sent to the client.');
        void this.router.navigate(['/queue']);
      }),
    );
  }

  protected test(email: SendEmail): void {
    this.call(this.api.deliverTest(this.requestId(), email), (r) => this.toast.show(`A test copy was sent to ${r.sentTo}.`));
  }

  protected manual(): void {
    this.guard(() =>
      this.call(this.api.deliverManually(this.requestId(), this.deliveredTo().trim(), this.note().trim()), () => {
        this.toast.show('Marked as delivered.');
        void this.router.navigate(['/queue']);
      }),
    );
  }
}
