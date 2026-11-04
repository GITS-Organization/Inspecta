import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GearDivision, divisionTotal } from './gear-division';
import { MachineDialog } from './machine-dialog';
import { BackendApi, openBlob, saveBlob } from '../../api/backend.api';
import { AuthService } from '../../core/auth.service';
import { MACHINES_OPEN, MachineList, MachineSummary } from '../../core/models';
import { apiMessage, problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { MACHINE_STATUS_LABEL, formatDate, machineChip, plural } from '../../shared/format';
import { KindBadge } from '../../shared/kind-badge';
import { MachineKinds } from '../../shared/machine-kinds';

/** The machines of one job: the inspector starts and continues here; the coordinator opens the approved certificates. */
@Component({
  selector: 'app-job',
  imports: [RouterLink, MachineDialog, GearDivision, KindBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="back muted" [routerLink]="backLink().path">{{ backLink().label }}</a>
    @if (error(); as msg) {
      <div class="error" role="alert">{{ msg }}</div>
    }
    @if (job(); as j) {
      <div class="head">
        <div>
          <app-kind-badge [kind]="j.request?.kind" />
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
          @if (canChange()) {
            <button type="button" class="btn" [class.ghost]="!j.machines.length" [disabled]="busy()" (click)="adding.set(true)">Add a machine</button>
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
        @for (g of groups(); track g.key) {
          @if (g.divided) {
            <div class="ghead">
              <div>
                <b>{{ g.members[0].name }}</b>, {{ plural(g.members.length, 'certificate') }}, {{ divisionTotal(g.members) }} in all
                @if (!g.open) { <div class="muted">A certificate of this group was started, so the division is locked.</div> }
              </div>
              @if (g.open && canChange()) {
                <button type="button" class="btn ghost small" [disabled]="busy()" (click)="dividing.set(g.members)">Change the division</button>
              }
            </div>
          }
          @for (m of g.members; track m.requestItemId) {
          <div class="mrow" [class.share]="g.divided">
            <div>
              <b>{{ m.name }}</b>
              <div class="muted">
                @if (gear(m)) { {{ m.quantity }} {{ m.quantity === 1 ? 'piece' : 'pieces' }}@if (m.serialNumber) { · <span class="sn">{{ m.serialNumber }}</span> } } @else if (m.serialNumber) { S/N {{ m.serialNumber }} }
                @if (m.certificateNumber) { · {{ m.certificateNumber }} }
              </div>
              @if (kindOf(m); as k) { <div class="muted std">{{ k }}@if (m.inspectionStandards?.length) { · {{ m.inspectionStandards!.join(' · ') }} }</div> }
              @if (m.reviewNote) { <div class="rn">{{ m.reviewNote }}</div> }
            </div>
            <span class="chip" [class]="chipClass(m.status)">{{ statusLabel(m.status) }}</span>
            <div class="macts">
              @if (isInspector()) {
                <a class="btn" [class.ghost]="!m.canEdit" [routerLink]="['/jobs', j.requestId, 'machines', m.requestItemId]">{{ m.canEdit ? 'Fill in' : 'Open' }}</a>
              } @else if (m.pdfUrl) {
                <button type="button" class="btn ghost" (click)="pdf(m.requestItemId)">Certificate PDF</button>
              }
              @if (canDivide(m) && !g.divided) {
                <button type="button" class="btn small" [disabled]="busy()" (click)="dividing.set([m])">Divide into certificates</button>
              }
              @if (canEdit(m)) {
                <button type="button" class="btn ghost small" [disabled]="busy()" (click)="editing.set(m)">Edit</button>
              }
              @if (canRemove(m)) {
                @if (confirmId() === m.requestItemId) {
                  <button type="button" class="btn warn small" [disabled]="busy()" (click)="remove(m)">Remove it</button>
                  <button type="button" class="btn ghost small" (click)="confirmId.set(null)">Keep</button>
                } @else {
                  <button type="button" class="btn ghost small" [disabled]="busy()" (click)="confirmId.set(m.requestItemId)" [attr.aria-label]="'Remove ' + m.name">Remove</button>
                }
              }
            </div>
          </div>
          }
        } @empty {
          <div class="emptyjob">
            <h2>No machines in this job yet.</h2>
            @if (canChange()) {
              <p class="muted">Add the machines found on site, registered ones or new ones. A new machine is registered for the client at once.</p>
              <button type="button" class="btn go" (click)="adding.set(true)">Add the first machine</button>
            } @else {
              <p class="muted">The machines are added once the inspector is on site.</p>
            }
          </div>
        }
      </div>
    } @else if (loading()) {
      <p class="muted empty" role="status">Loading the job…</p>
    }
    @if (dividing(); as d) {
      <app-gear-division [requestId]="requestId()" [members]="d" (done)="divided($event)" (closed)="dividing.set(null)" />
    }
    @if (adding() || editing()) {
      <app-machine-dialog [requestId]="requestId()" [editing]="editing()" [first]="!!job() && !job()!.machines.length" (done)="changed($event)" (closed)="closeDialog()" />
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
      margin: 0;
      padding: 0;
      border: 0;
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
    .macts {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      align-items: center;
    }
    .emptyjob {
      border: 1.5px dashed var(--line);
      border-radius: 12px;
      padding: 36px 24px;
      text-align: center;
      h2 {
        font-size: 28px;
        margin-bottom: 6px;
      }
      p {
        max-width: 460px;
        margin: 0 auto 18px;
      }
    }
    .ghead {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      background: var(--band);
      border-radius: 8px 8px 0 0;
      border-bottom: 2px solid var(--teal);
      padding: 10px 14px;
      margin-top: 14px;
      b {
        color: var(--ink);
        font-size: 17px;
      }
    }
    .share {
      padding-left: 14px;
      border-left: 4px solid var(--teal);
    }
    .sn {
      display: inline-block;
      max-width: 46ch;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      vertical-align: bottom;
    }
    .std {
      font-size: 13px;
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
  protected readonly kinds = inject(MachineKinds);
  protected readonly divisionTotal = divisionTotal;
  protected readonly dividing = signal<MachineSummary[] | null>(null);
  /** The machines in the order of the list; the certificates of one divided gear are one group. */
  protected readonly groups = computed(() => {
    const out: { key: string; divided: boolean; open: boolean; members: MachineSummary[] }[] = [];
    for (const m of this.job()?.machines ?? []) {
      const at = m.divisionGroupId ? out.find((g) => g.key === m.divisionGroupId) : undefined;
      if (at) at.members.push(m);
      else out.push({ key: m.divisionGroupId ?? m.requestItemId, divided: !!m.divisionGroupId, open: true, members: [m] });
    }
    for (const g of out) g.open = g.members.every((m) => m.status === 'not_started');
    return out;
  });
  protected readonly adding = signal(false);
  protected readonly editing = signal<MachineSummary | null>(null);
  protected readonly confirmId = signal<string | null>(null);
  /** Inspector, coordinator, technical manager and admin may change the machines while the job is under way. */
  protected readonly canChange = computed(() => !!this.auth.role() && MACHINES_OPEN.includes(this.job()?.requestStatus as never));
  protected readonly backLink = computed(() => {
    const r = this.auth.role();
    return r === 'inspector' ? { path: '/jobs', label: 'Back to jobs' } : r === 'coordinator' ? { path: '/queue', label: 'Back to jobs' } : { path: '/review', label: 'Back to review' };
  });

  protected readonly latestNote = computed(() => {
    const j = this.job();
    return j?.requestStatus === 'certificate_returned' ? (j.reviewHistory?.find((c) => !c.requestItemId)?.text ?? null) : null;
  });

  ngOnInit(): void {
    this.kinds.load();
    this.load();
  }

  /** A machine whose certificate was submitted is locked until the review. */
  protected canEdit(m: MachineSummary): boolean {
    return this.canChange() && (m.status === 'not_started' || m.status === 'draft' || m.status === 'returned');
  }
  /** Once a certificate was started (even as a draft) the machine stays. */
  protected canRemove(m: MachineSummary): boolean {
    return this.canChange() && m.status === 'not_started';
  }

  protected kindOf(m: MachineSummary): string {
    return this.kinds.kindLabel(m.kind ?? null);
  }
  protected gear(m: MachineSummary): boolean {
    return m.isGear ?? this.kinds.isGear(m.name, m.type);
  }
  /** A gear whose certificate was not started can be divided into certificates. */
  protected canDivide(m: MachineSummary): boolean {
    return this.canChange() && m.status === 'not_started' && this.gear(m);
  }
  protected divided(list: MachineList): void {
    this.job.set(list);
    this.dividing.set(null);
    this.toast.show('The certificates were saved.');
  }

  protected changed(list: MachineList): void {
    this.job.set(list);
    const wasEdit = !!this.editing();
    this.closeDialog();
    this.toast.show(wasEdit ? 'The machine was changed.' : 'The machine was added.');
  }
  protected closeDialog(): void {
    this.adding.set(false);
    this.editing.set(null);
  }

  protected remove(m: MachineSummary): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.removeMachine(this.requestId(), m.requestItemId).subscribe({
      next: (list) => {
        this.job.set(list);
        this.confirmId.set(null);
        this.busy.set(false);
        this.toast.show('The machine was removed from the job.');
      },
      error: (e: unknown) => {
        this.confirmId.set(null);
        this.busy.set(false);
        this.error.set(apiMessage(e));
      },
    });
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
