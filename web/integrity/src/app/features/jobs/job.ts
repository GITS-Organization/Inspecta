import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BackendApi, openBlob, saveBlob } from '../../api/backend.api';
import { AuthService } from '../../core/auth.service';
import { MachineList } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { MACHINE_STATUS_LABEL, formatDate, machineChip, plural } from '../../shared/format';

/** The machines of one job: the inspector starts and continues here; the coordinator opens the approved certificates. */
@Component({
  selector: 'app-job',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="back muted" routerLink="/jobs">Back to jobs</a>
    @if (error(); as msg) {
      <div class="error" role="alert">{{ msg }}</div>
    }
    @if (job(); as j) {
      <div class="head">
        <div>
          <h1>{{ j.clientName }}</h1>
          <p class="muted">
            {{ j.location }} · {{ plural(j.machines.length, 'machine') }}
            @if (j.scheduledAt) { · {{ formatDate(j.scheduledAt) }} }
          </p>
        </div>
        <div class="actions">
          @if (j.canStartInspection && isInspector()) {
            <button type="button" class="btn go" [disabled]="busy()" (click)="start()">Start inspection</button>
          }
          @if (isInspector() && j.machines.length) {
            <button type="button" class="btn ghost" [disabled]="busy()" (click)="zip()">Download previews (ZIP)</button>
          }
        </div>
      </div>

      @if (latestNote(); as note) {
        <div class="note-box" role="note"><b>Note from your technical manager</b><p>{{ note }}</p></div>
      }

      <div class="mlist">
        @for (m of j.machines; track m.requestItemId) {
          <div class="mrow">
            <div>
              <b>{{ m.name }}</b>
              <div class="muted">
                @if (m.quantity > 1) { {{ m.quantity }} pieces } @else if (m.serialNumber) { S/N {{ m.serialNumber }} }
                @if (m.certificateNumber) { · {{ m.certificateNumber }} }
              </div>
              @if (m.reviewNote) { <div class="rn">{{ m.reviewNote }}</div> }
            </div>
            <span class="chip" [class]="chipClass(m.status)">{{ statusLabel(m.status) }}</span>
            @if (isInspector()) {
              <a class="btn" [class.ghost]="!m.canEdit" [routerLink]="['/jobs', j.requestId, 'machines', m.requestItemId]">{{ m.canEdit ? 'Fill in' : 'Open' }}</a>
            } @else if (m.pdfUrl) {
              <button type="button" class="btn ghost" (click)="pdf(m.requestItemId)">Certificate PDF</button>
            }
          </div>
        } @empty {
          <p class="empty">This job has no machines.</p>
        }
      </div>
    } @else if (loading()) {
      <p class="muted empty" role="status">Loading the job…</p>
    }
  `,
  styles: `
    .back {
      display: inline-block;
      margin-bottom: 18px;
      text-decoration: underline;
    }
    .actions {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
    }
    .note-box {
      background: var(--amber-bg);
      border-radius: 8px;
      padding: 14px 18px;
      margin-bottom: 18px;
      color: var(--body);
      b {
        color: var(--amber);
      }
    }
    .mrow {
      display: grid;
      grid-template-columns: 1fr auto auto;
      gap: 18px;
      align-items: center;
      padding: 14px 0;
      border-bottom: 1px solid var(--line);
      b {
        color: var(--ink);
        font-size: 16px;
      }
    }
    .rn {
      color: var(--red);
      font-size: 13px;
      margin-top: 2px;
    }
    @media (max-width: 820px) {
      .mrow {
        grid-template-columns: 1fr;
        justify-items: start;
      }
    }
  `,
})
export class Job implements OnInit {
  readonly requestId = input.required<string>();

  private readonly api = inject(BackendApi);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  protected readonly plural = plural;
  protected readonly formatDate = formatDate;

  protected readonly job = signal<MachineList | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly isInspector = computed(() => this.auth.role() === 'inspector');

  protected readonly latestNote = computed(() => {
    const j = this.job();
    return j?.requestStatus === 'certificate_returned' ? (j.reviewHistory?.find((c) => !c.requestItemId)?.text ?? null) : null;
  });

  ngOnInit(): void {
    this.load();
  }

  protected statusLabel = (s: Parameters<typeof machineChip>[0]) => MACHINE_STATUS_LABEL[s];
  protected chipClass = (s: Parameters<typeof machineChip>[0]) => 'chip ' + machineChip(s);

  private load(): void {
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
  }

  protected start(): void {
    this.busy.set(true);
    this.api.startInspection(this.requestId()).subscribe({
      next: (j) => {
        this.job.set(j);
        this.busy.set(false);
        this.toast.show('Inspection started.');
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e));
        this.busy.set(false);
      },
    });
  }

  protected pdf(itemId: string): void {
    this.api.machinePdf(this.requestId(), itemId).subscribe({ next: (b) => openBlob(b), error: (e: unknown) => this.error.set(problemMessage(e)) });
  }

  protected zip(): void {
    this.api.previewZip(this.requestId()).subscribe({
      next: (b) => saveBlob(b, 'certificate-previews.zip'),
      error: (e: unknown) => this.error.set(problemMessage(e)),
    });
  }
}
