import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { BackendApi, openBlob, saveBlob } from '../../api/backend.api';
import { KIND_LABELS } from '../../api/certification.api';
import { MachineList, MachineSummary } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { CertificatePaper } from '../../shared/certificate-paper';
import { MACHINE_STATUS_LABEL, RESULT_LABEL, formatDate, plural, resultChip } from '../../shared/format';

/** The technical manager's review of one job: pick a machine, see the certificate as it prints, approve or send back. */
@Component({
  selector: 'app-review',
  imports: [RouterLink, CertificatePaper],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './review.html',
  styleUrl: './review.scss',
})
export class Review implements OnInit {
  protected readonly kindLabels = KIND_LABELS;
  readonly requestId = input.required<string>();

  private readonly api = inject(BackendApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly plural = plural;
  protected readonly formatDate = formatDate;
  protected readonly resultLabel = RESULT_LABEL;
  protected readonly resultChip = resultChip;
  protected readonly statusLabel = MACHINE_STATUS_LABEL;

  protected readonly job = signal<MachineList | null>(null);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly note = signal('');
  protected readonly busy = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly noteMissing = signal(false);

  protected readonly selected = computed<MachineSummary | null>(() => {
    const j = this.job();
    if (!j) return null;
    return j.machines.find((m) => m.requestItemId === this.selectedId()) ?? j.machines[0] ?? null;
  });

  protected readonly certificateCount = computed(() => this.job()?.machines.length ?? 0);

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
  }

  protected stamp(m: MachineSummary): 'safe' | 'no' | 'dash' {
    return m.result === 'unsatisfactory' ? 'no' : m.result ? 'safe' : 'dash';
  }

  protected serialText(m: MachineSummary): string {
    return m.quantity > 1 ? `${m.quantity} pieces` : (m.serialNumber ?? '');
  }

  protected pdf(m: MachineSummary): void {
    this.api.machinePdf(this.requestId(), m.requestItemId).subscribe({ next: (b) => openBlob(b), error: (e: unknown) => this.error.set(problemMessage(e)) });
  }

  protected zip(): void {
    this.api.previewZip(this.requestId()).subscribe({
      next: (b) => saveBlob(b, 'certificate-previews.zip'),
      error: (e: unknown) => this.error.set(problemMessage(e)),
    });
  }

  protected approve(): void {
    const j = this.job();
    if (!j?.canApprove || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.api.approveAll(this.requestId()).subscribe({
      next: (r) => {
        this.toast.show(`Approved. ${plural(r.machines.length, 'certificate')} numbered and filed.`);
        void this.router.navigate(['/review']);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e));
        this.busy.set(false);
      },
    });
  }

  protected sendBack(): void {
    const text = this.note().trim();
    if (!text) {
      this.noteMissing.set(true);
      return;
    }
    this.noteMissing.set(false);
    this.busy.set(true);
    this.error.set(null);
    this.api.returnToInspector(this.requestId(), { note: text }).subscribe({
      next: () => {
        this.toast.show('Sent back to the inspector with your note.');
        void this.router.navigate(['/review']);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e));
        this.busy.set(false);
      },
    });
  }
}
