import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  CERTIFICATION_API,
  CertificateKind,
  ClientOption,
  CreateCertificationRequest,
  MachineOption,
  NewMachine,
} from '../../api/certification.api';
import { ChecklistValue, InspectionResult } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { CertificatePaper } from '../../shared/certificate-paper';
import { formatDate } from '../../shared/format';
import {
  SERIAL_BOX_CAPACITY,
  fillRatio,
  generateSerials,
  parseSerials,
  splitSerials,
  suggestedCertificateCount,
} from '../../shared/serials';

const today = () => new Date().toISOString().slice(0, 10);
const inAYear = () => {
  const d = new Date();
  d.setFullYear(d.getFullYear() + 1);
  return d.toISOString().slice(0, 10);
};

/** Create certification: a certificate without a quotation, sent straight to the technical manager. */
@Component({
  selector: 'app-create',
  imports: [CertificatePaper],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './create.html',
  styleUrl: './create.scss',
})
export class Create {
  private readonly api = inject(CERTIFICATION_API);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly formatDate = formatDate;

  // 1. client
  protected readonly clients = signal<ClientOption[]>([]);
  protected readonly clientId = signal('');
  // 2. machine
  protected readonly source = signal<'ex' | 'new'>('ex');
  protected readonly query = signal('');
  protected readonly machines = signal<MachineOption[]>([]);
  protected readonly machineId = signal<string | null>(null);
  protected readonly nm = signal<NewMachine>({ name: '', manufacturer: '', serialNumber: '', kind: 'standard' });
  // 3. examination
  protected readonly examinedOn = signal(today());
  protected readonly nextOn = signal(inAYear());
  protected readonly result = signal<InspectionResult>('satisfactory');
  protected readonly checks = signal<Record<string, ChecklistValue>>({});
  // gears
  protected readonly quantity = signal(1000);
  protected readonly prefix = signal('CS-26-');
  protected readonly serialsText = signal('');
  protected readonly parsed = computed(() => parseSerials(this.serialsText()));
  protected readonly serials = computed(() => this.parsed().serials);
  private readonly countOverride = signal<number | null>(null);
  protected readonly page = signal(0);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly submitted = signal<string | null>(null);

  protected readonly selected = computed(() => this.machines().find((m) => m.id === this.machineId()) ?? null);
  protected readonly kind = computed<CertificateKind>(() => (this.source() === 'ex' ? (this.selected()?.kind ?? 'standard') : this.nm().kind));
  protected readonly isGear = computed(() => this.kind() === 'gear');
  protected readonly checklistDefs = computed(() => this.api.checklistFor(this.kind()));
  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    return this.machines().filter((m) => !q || `${m.name} ${m.serialNumber ?? ''} ${m.manufacturer ?? ''}`.toLowerCase().includes(q));
  });

  protected readonly count = computed(() => {
    const n = this.serials().length;
    const c = this.countOverride() ?? suggestedCertificateCount(this.serials());
    return Math.max(1, Math.min(c, Math.max(1, n)));
  });
  protected readonly maxCount = computed(() => Math.max(2, Math.min(this.serials().length || 1, 200)));
  protected readonly parts = computed(() => splitSerials(this.serials(), this.count()));
  protected readonly fill = computed(() => fillRatio(this.parts()));
  protected readonly part = computed(() => this.parts()[Math.min(this.page(), this.parts().length - 1)] ?? []);
  protected readonly capacity = SERIAL_BOX_CAPACITY;
  protected readonly min100 = (n: number) => Math.min(100, n * 100);
  protected readonly round = Math.round;

  protected readonly unsafe = computed(() => this.result() === 'unsatisfactory' || Object.values(this.checks()).includes('no'));
  protected readonly machineName = computed(() => (this.source() === 'ex' ? (this.selected()?.name ?? '') : this.nm().name));
  protected readonly manufacturer = computed(() => (this.source() === 'ex' ? (this.selected()?.manufacturer ?? '') : this.nm().manufacturer));
  protected readonly clientName = computed(() => this.clients().find((c) => c.id === this.clientId())?.name ?? '');
  protected readonly serialPreview = computed(() =>
    this.isGear() ? (this.serials().length ? `${this.part().length} of ${this.serials().length}` : '') : this.source() === 'ex' ? (this.selected()?.serialNumber ?? '') : this.nm().serialNumber,
  );
  protected readonly summary = computed(() =>
    this.isGear() ? `${this.count()} certificates for ${this.serials().length} items` : '1 certificate',
  );

  constructor() {
    this.regenerate();
    this.api.listClients().subscribe({
      next: (c) => {
        this.clients.set(c);
        if (c.length) this.pickClient(c[0].id);
      },
      error: (e: unknown) => this.error.set(problemMessage(e, 'The clients could not be loaded.')),
    });
  }

  protected pickClient(id: string): void {
    this.clientId.set(id);
    this.machineId.set(null);
    this.machines.set([]);
    this.api.listMachines(id).subscribe({
      next: (m) => {
        if (this.clientId() !== id) return;
        this.machines.set(m);
        if (m.length) this.machineId.set(m[0].id);
      },
      error: (e: unknown) => this.error.set(problemMessage(e, 'The machines could not be loaded.')),
    });
  }

  protected patchNew(p: Partial<NewMachine>): void {
    this.nm.update((m) => ({ ...m, ...p }));
  }

  protected setCheck(key: string, v: ChecklistValue): void {
    this.checks.update((c) => ({ ...c, [key]: v }));
  }

  // ---- gears
  protected setQuantity(v: string): void {
    this.quantity.set(Math.max(0, Math.min(5000, parseInt(v, 10) || 0)));
    this.regenerate();
  }
  protected setPrefix(v: string): void {
    this.prefix.set(v);
    this.regenerate();
  }
  private regenerate(): void {
    this.serialsText.set(generateSerials(this.prefix(), this.quantity()).join('\n'));
    this.countOverride.set(null);
    this.page.set(0);
  }
  protected editSerials(text: string): void {
    this.serialsText.set(text);
    this.quantity.set(parseSerials(text).serials.length);
    this.countOverride.set(null);
    this.page.set(0);
  }
  protected setCount(v: string): void {
    this.countOverride.set(parseInt(v, 10) || 1);
    this.page.set(0);
  }
  protected turn(d: number): void {
    this.page.update((p) => Math.max(0, Math.min(this.parts().length - 1, p + d)));
  }

  // ---- send
  private build(submit: boolean): CreateCertificationRequest | string {
    if (!this.clientId()) return 'Choose a client.';
    if (this.source() === 'ex' && !this.machineId()) return 'Choose a machine or add a new one.';
    if (this.source() === 'new' && !this.nm().name.trim()) return 'Give the new machine a name.';
    if (this.isGear() && !this.serials().length) return 'Add the serial numbers of the batch.';
    if (this.isGear() && this.fill() > 1) return 'The serial numbers do not fit on that many certificates. Add certificates.';
    if (!this.examinedOn() || !this.nextOn()) return 'Fill in both examination dates.';
    return {
      clientId: this.clientId(),
      machineId: this.source() === 'ex' ? this.machineId() : null,
      newMachine: this.source() === 'new' ? { ...this.nm(), name: this.nm().name.trim() } : null,
      examinedOn: this.examinedOn(),
      nextExaminationOn: this.nextOn(),
      result: this.result(),
      checklist: this.isGear() ? {} : this.checks(),
      gear: this.isGear() ? { quantity: this.serials().length, serials: this.serials(), certificates: this.parts() } : null,
      submit,
    };
  }

  protected send(submit: boolean): void {
    if (this.busy()) return;
    const req = this.build(submit);
    if (typeof req === 'string') {
      this.error.set(req);
      return;
    }
    this.error.set(null);
    this.busy.set(true);
    this.api.create(req).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.toast.show(submit ? 'Sent to your technical manager for review.' : 'Draft saved.');
        this.submitted.set(submit ? `${r.certificateCount} ${r.certificateCount === 1 ? 'certificate' : 'certificates'} sent for review.` : null);
        if (submit) void this.router.navigate(['/create'], { onSameUrlNavigation: 'reload' });
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }
}
