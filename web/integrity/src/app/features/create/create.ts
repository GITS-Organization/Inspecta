import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CERTIFICATION_API, ClientOption, MachineOption, MachineType } from '../../api/certification.api';
import { InspectionResult } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { CertificatePaper } from '../../shared/certificate-paper';
import { formatDate, plural } from '../../shared/format';
import { DEFAULT_LIMITS, SerialLimits, fillRatio, generateSerials, parseSerials, splitSerials, suggestedCertificateCount } from '../../shared/serials';
import { DirectCertification, DirectPlan } from './direct-certification';

const today = () => new Date().toISOString().slice(0, 10);
const inAYear = () => {
  const d = new Date();
  d.setFullYear(d.getFullYear() + 1);
  return d.toISOString().slice(0, 10);
};
const norm = (s: string) => s.trim().toLowerCase();

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
  private readonly flow = inject(DirectCertification);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly formatDate = formatDate;

  // backend lookups
  protected readonly types = signal<MachineType[]>([]);
  protected readonly limits = signal<SerialLimits>(DEFAULT_LIMITS);
  // 1. client
  protected readonly clients = signal<ClientOption[]>([]);
  protected readonly clientId = signal('');
  protected readonly clientsError = signal<string | null>(null);
  // 2. machine
  protected readonly source = signal<'ex' | 'new'>('ex');
  protected readonly query = signal('');
  protected readonly machines = signal<MachineOption[]>([]);
  protected readonly machineId = signal<string | null>(null);
  protected readonly newName = signal('');
  protected readonly newManufacturer = signal('');
  protected readonly newSerial = signal('');
  // 3. examination
  protected readonly examinedOn = signal(today());
  protected readonly nextOn = signal(inAYear());
  protected readonly result = signal<InspectionResult>('satisfactory');
  // gears
  protected readonly quantity = signal(1000);
  protected readonly prefix = signal('CS-26-');
  protected readonly serialsText = signal('');
  private readonly countOverride = signal<number | null>(null);
  protected readonly page = signal(0);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly selected = computed(() => this.machines().find((m) => m.id === this.machineId()) ?? null);
  protected readonly machineName = computed(() => (this.source() === 'ex' ? (this.selected()?.name ?? '') : this.newName()));
  protected readonly matchedType = computed(() => {
    const n = norm(this.machineName());
    return n ? this.types().find((t) => norm(t.name) === n || t.aliases.some((a) => norm(a) === n)) : undefined;
  });
  protected readonly isGear = computed(() => this.matchedType()?.isGear ?? false);
  protected readonly manufacturer = computed(() => (this.source() === 'ex' ? (this.selected()?.manufacturer ?? '') : this.newManufacturer()));
  protected readonly clientName = computed(() => this.clients().find((c) => c.id === this.clientId())?.name ?? '');
  protected readonly filtered = computed(() => {
    const q = norm(this.query());
    return this.machines().filter((m) => !q || `${m.name} ${m.serialNumber ?? ''} ${m.manufacturer ?? ''}`.toLowerCase().includes(q));
  });

  protected readonly parsed = computed(() => parseSerials(this.serialsText(), this.limits()));
  protected readonly serials = computed(() => this.parsed().serials);
  protected readonly count = computed(() => {
    const n = Math.max(1, this.serials().length);
    return Math.max(1, Math.min(this.countOverride() ?? suggestedCertificateCount(this.serials(), this.limits()), n));
  });
  protected readonly maxCount = computed(() => Math.max(2, Math.min(this.serials().length || 1, 200)));
  protected readonly parts = computed(() => splitSerials(this.serials(), this.count()));
  protected readonly fill = computed(() => fillRatio(this.parts(), this.limits()));
  protected readonly part = computed(() => this.parts()[Math.min(this.page(), this.parts().length - 1)] ?? []);
  protected readonly min100 = (n: number) => Math.min(100, n * 100);
  protected readonly round = Math.round;

  protected readonly serialPreview = computed(() =>
    this.isGear() ? (this.serials().length ? `${this.part().length} of ${this.serials().length}` : '') : this.source() === 'ex' ? (this.selected()?.serialNumber ?? '') : this.newSerial(),
  );
  protected readonly summary = computed(() =>
    this.isGear() ? `${plural(this.count(), 'certificate')} for ${plural(this.serials().length, 'item')}` : '1 certificate',
  );

  constructor() {
    this.regenerate();
    this.api.machineTypes().subscribe({ next: (t) => this.types.set(t), error: () => undefined });
    this.api.serialLimits().subscribe({ next: (l) => this.limits.set(l), error: () => undefined });
    this.api.listClients().subscribe({
      next: (c) => {
        this.clients.set(c);
        if (c.length) this.pickClient(c[0].id);
      },
      error: (e: unknown) =>
        this.clientsError.set(problemMessage(e, 'The clients could not be loaded.') + ' If you are an inspector, ask your technical manager to open the client list for you.'),
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
        else this.source.set('new');
      },
      error: (e: unknown) => this.error.set(problemMessage(e, 'The machines could not be loaded.')),
    });
  }

  // ---- gears
  protected setQuantity(v: string): void {
    this.quantity.set(Math.max(0, Math.min(this.limits().maxSerialNumbers, parseInt(v, 10) || 0)));
    this.regenerate();
  }
  protected setPrefix(v: string): void {
    this.prefix.set(v);
    this.regenerate();
  }
  private regenerate(): void {
    this.serialsText.set(generateSerials(this.prefix(), this.quantity(), this.limits()).join('\n'));
    this.countOverride.set(null);
    this.page.set(0);
  }
  protected editSerials(text: string): void {
    this.serialsText.set(text);
    this.quantity.set(parseSerials(text, this.limits()).serials.length);
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
  private plan(submit: boolean): DirectPlan | string {
    if (!this.clientId()) return 'Choose a client.';
    if (this.source() === 'ex' && !this.machineId()) return 'Choose a machine or add a new one.';
    if (this.source() === 'new' && !this.newName().trim()) return 'Give the new machine a name.';
    if (this.isGear() && !this.serials().length) return 'Add the serial numbers of the batch.';
    if (this.isGear() && this.parsed().tooLong) return `A serial number is longer than ${this.limits().maxLengthPerSerialNumber} characters.`;
    if (this.isGear() && this.fill() > 1) return 'The serial numbers do not fit on that many certificates. Add certificates.';
    if (!this.examinedOn() || !this.nextOn()) return 'Fill in both examination dates.';
    const sel = this.selected();
    return {
      clientId: this.clientId(),
      machine: {
        name: this.machineName().trim(),
        isGear: this.isGear(),
        ...(this.source() === 'ex' && sel ? { equipmentId: sel.id } : {}),
        ...(this.matchedType() ? { type: this.matchedType()!.name } : {}),
        ...(this.source() === 'new' && this.newSerial().trim() ? { serialNumber: this.newSerial().trim() } : {}),
      },
      serials: this.isGear() ? this.serials() : [],
      certificates: this.count(),
      examinedOn: this.examinedOn(),
      nextExaminationOn: this.nextOn(),
      result: this.result(),
      submit,
    };
  }

  protected send(submit: boolean): void {
    if (this.busy()) return;
    const plan = this.plan(submit);
    if (typeof plan === 'string') {
      this.error.set(plan);
      return;
    }
    this.error.set(null);
    this.notice.set(null);
    this.busy.set(true);
    this.flow.run(plan).subscribe({
      next: (o) => {
        this.busy.set(false);
        if (o.pending.length) {
          // Something is still missing (usually the checklist): continue on the machine's own form.
          this.toast.show(submit ? 'Saved. Finish the checklist to send it.' : 'Draft saved.');
          void this.router.navigate(['/jobs', o.requestId, 'machines', o.pending[0].requestItemId]);
        } else if (submit) {
          this.toast.show('Sent to your technical manager for review.');
          this.notice.set(`${plural(o.submitted, 'certificate')} sent for review.`);
        } else {
          this.toast.show('Draft saved.');
          void this.router.navigate(['/jobs', o.requestId, 'machines', o.machines[0].requestItemId]);
        }
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }
}
