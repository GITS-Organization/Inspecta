import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { CoordinatorApi } from '../../api/coordinator.api';
import { DeliveryPreview, SendEmail } from '../../api/coordinator.models';
import { openBlob, saveBlob } from '../../api/backend.api';
import { MachineList } from '../../core/models';
import { problemMessage } from '../../core/problem';
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
            @for (c of certs(); track c.fileName) {
              <div class="cert">
                <div>
                  <b>{{ c.itemName }}</b>
                  <div class="muted sm">{{ c.certificateNumber }} · {{ formatDate(c.examinationDate) }} → {{ formatDate(c.nextExaminationDate) }}</div>
                </div>
                <div class="rowline">
                  @if (c.result) { <span class="chip" [class]="resultChip(c.result)">{{ resultLabel[c.result] }}</span> }
                  <button type="button" class="btn ghost small" (click)="pdf(c.pdfUrl)">PDF</button>
                </div>
              </div>
            } @empty {
              <p class="muted">The list appears once the email preview is loaded.</p>
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
      display: flex;
      justify-content: space-between;
      gap: 10px;
      align-items: center;
      padding: 10px 0;
      border-bottom: 1px solid var(--line);
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
    this.call(this.api.deliver(this.requestId(), email), () => {
      this.toast.show('The certificates were sent to the client.');
      void this.router.navigate(['/queue']);
    });
  }

  protected test(email: SendEmail): void {
    this.call(this.api.deliverTest(this.requestId(), email), (r) => this.toast.show(`A test copy was sent to ${r.sentTo}.`));
  }

  protected manual(): void {
    this.call(this.api.deliverManually(this.requestId(), this.deliveredTo().trim(), this.note().trim()), () => {
      this.toast.show('Marked as delivered.');
      void this.router.navigate(['/queue']);
    });
  }
}
