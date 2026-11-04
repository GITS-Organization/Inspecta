import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { GearDivision, divisionTotal } from './gear-division';
import { MachineDialog } from './machine-dialog';
import { BackendApi, saveBlob } from '../../api/backend.api';
import { AuthService } from '../../core/auth.service';
import { MACHINES_OPEN, MachineList, MachineSummary } from '../../core/models';
import { apiMessage, apiMessageAsync, problemMessage } from '../../core/problem';
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
            <button type="button" class="btn" [class.go]="!canSubmit()" [class.ghost]="canSubmit()" [disabled]="busy()" (click)="adding.set(true)">Add a certificate</button>
          }
          @if (canPreviewAll() && j.machines.length) {
            <button type="button" class="btn ghost" [disabled]="busy() || fetching() !== null" (click)="zip()">Preview all (ZIP)</button>
          }
          @if (canSubmit()) {
            <button type="button" class="btn go" [class.soft]="unsaved() > 0" [disabled]="busy()" [attr.title]="unsaved() > 0 ? unsaved() + ' still need a saved certificate' : null" (click)="askSubmit()">Submit certificates</button>
          }
        </div>
      </div>
      @if (canSubmit() && unsaved() > 0) {
        <p class="reason muted">{{ plural(unsaved(), 'certificate') }} not saved yet. Fill {{ unsaved() === 1 ? 'it' : 'them' }} in first; if you submit now the API lists what is missing.</p>
      }
      @if (problems().length) {
        <div class="problems" role="alert">
          <b>Nothing was submitted.</b> The certificates are all-or-nothing: fix these first, then submit again.
          <ul>
            @for (p of problems(); track $index) {
              <li>
                <button type="button" class="lnk" (click)="goTo(p.id)">{{ p.text }}</button>
                @if (p.id && isInspector()) { <a class="open" [routerLink]="['/jobs', j.requestId, 'machines', p.id]">Open</a> }
              </li>
            }
          </ul>
        </div>
      }

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
          <div class="mrow" [id]="'m-' + m.requestItemId" [class.share]="g.divided" [class.flash]="highlight() === m.requestItemId">
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
              }
              @if (m.pdfUrl || canPreviewAll()) {
                <button type="button" class="btn ghost small" [disabled]="!m.pdfUrl || fetching() !== null" [attr.title]="m.pdfUrl ? null : 'Fill the certificate first'" (click)="preview(m)">{{ fetching() === m.requestItemId ? 'Opening…' : 'Preview' }}</button>
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
            <h2>No certificates in this job yet.</h2>
            @if (canChange()) {
              <p class="muted">Add the machines found on site, registered ones or new ones, and each becomes a certificate. A new machine is registered for the client at once.</p>
              <button type="button" class="btn go" (click)="adding.set(true)">Add the first certificate</button>
            } @else {
              <p class="muted">The machines are added once the inspector is on site.</p>
            }
          </div>
        }
      </div>
    } @else if (loading()) {
      <p class="muted empty" role="status">Loading the job…</p>
    }
    @if (confirming()) {
      <div class="scrim" (click)="confirming.set(false)"></div>
      <div class="dlg" role="alertdialog" aria-modal="true" aria-labelledby="sa-t">
        <h2 id="sa-t">Submit the certificates?</h2>
        <p>{{ plural(toSubmit(), 'certificate') }} will go to your technical manager. Nothing is sent unless every certificate is complete.</p>
        <div class="rowbtns">
          <button type="button" class="btn go" [disabled]="busy()" (click)="submit()">Submit certificates</button>
          <button type="button" class="btn ghost" (click)="confirming.set(false)">Cancel</button>
        </div>
      </div>
    }
    @if (viewer(); as v) {
      <div class="scrim" (click)="closeViewer()"></div>
      <div class="dlg viewer" role="dialog" aria-modal="true" aria-labelledby="pv-t">
        <div class="vhead">
          <h2 id="pv-t">{{ v.name }}</h2>
          <button type="button" class="x" aria-label="Close the preview" (click)="closeViewer()">×</button>
        </div>
        @if (v.draft) {
          <p class="draftnote" role="note">This is the certificate as it will print. It is marked <b>DRAFT</b> and the QR box is empty until the technical manager approves it.</p>
        }
        <iframe class="pdf" [src]="v.url" [title]="'Certificate preview of ' + v.name"></iframe>
        <div class="rowbtns">
          <button type="button" class="btn ghost small" (click)="openTab(v)">Open in a new tab</button>
          <button type="button" class="btn small" (click)="closeViewer()">Close</button>
        </div>
      </div>
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
    .soft {
      opacity: 0.6;
    }
    .reason {
      margin: -14px 0 14px;
    }
    .problems {
      background: var(--red-bg);
      border-left: 5px solid var(--red);
      border-radius: 8px;
      padding: 14px 18px;
      margin-bottom: 18px;
      color: var(--red);
      ul {
        margin: 8px 0 0;
        padding-left: 20px;
        color: var(--body);
      }
      li {
        margin-bottom: 4px;
      }
      .lnk {
        background: none;
        border: 0;
        padding: 0;
        text-align: left;
        font: inherit;
        color: var(--body);
        text-decoration: underline;
        cursor: pointer;
      }
      .open {
        margin-left: 8px;
        font-weight: 600;
        color: var(--ink);
      }
    }
    .flash {
      animation: flash 2.4s ease-out;
    }
    @keyframes flash {
      0%,
      60% {
        background: var(--green-bg);
        box-shadow: inset 4px 0 0 var(--green);
      }
      100% {
        background: transparent;
        box-shadow: none;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .flash {
        animation: none;
        background: var(--green-bg);
      }
    }
    .scrim {
      position: fixed;
      inset: 0;
      z-index: 40;
      background: rgb(19 40 93 / 0.55);
    }
    .dlg {
      position: fixed;
      z-index: 41;
      left: 50%;
      top: 50%;
      transform: translate(-50%, -50%);
      width: min(520px, calc(100vw - 32px));
      background: #fff;
      border-radius: 12px;
      border-top: 6px solid var(--green);
      padding: 22px 26px;
      box-shadow: 0 24px 60px rgb(19 40 93 / 0.35);
      h2 {
        font-size: 28px;
        margin: 0 0 8px;
      }
      &.viewer {
        width: min(960px, calc(100vw - 32px));
        height: calc(100vh - 40px);
        display: flex;
        flex-direction: column;
        gap: 10px;
      }
    }
    .vhead {
      display: flex;
      justify-content: space-between;
      align-items: center;
      h2 {
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
    }
    .draftnote {
      margin: 0;
      padding: 8px 12px;
      background: var(--amber-bg);
      border-radius: 8px;
    }
    .pdf {
      flex: 1;
      width: 100%;
      border: 1.5px solid var(--line);
      border-radius: 8px;
      min-height: 0;
    }
    .rowbtns {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
      margin-top: 8px;
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
  private readonly sanitizer = inject(DomSanitizer);
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
  protected readonly confirming = signal(false);
  protected readonly problems = signal<{ text: string; id: string | null }[]>([]);
  protected readonly highlight = signal<string | null>(null);
  protected readonly fetching = signal<string | null>(null);
  protected readonly viewer = signal<{ raw: string; url: SafeResourceUrl; name: string; draft: boolean } | null>(null);
  /** The assigned inspector sends the whole job while it is under way. */
  protected readonly canSubmit = computed(() => {
    const j = this.job();
    return this.isInspector() && !!j && (j.requestStatus === 'in_progress' || j.requestStatus === 'certificate_returned') && j.machines.length > 0;
  });
  /** Machines whose certificate was not saved yet. */
  protected readonly unsaved = computed(() => (this.job()?.machines ?? []).filter((m) => m.status === 'not_started').length);
  /** Certificates that would go to the technical manager now. */
  protected readonly toSubmit = computed(() => (this.job()?.machines ?? []).filter((m) => m.status === 'not_started' || m.status === 'draft' || m.status === 'returned').length);
  /** The inspector, the technical manager and the admin may preview before approval (the coordinator only gets approved files). */
  protected readonly canPreviewAll = computed(() => !!this.auth.role() && this.auth.role() !== 'coordinator');
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
    const before = new Set((this.job()?.machines ?? []).map((m) => m.requestItemId));
    this.job.set(list);
    const wasEdit = !!this.editing();
    this.closeDialog();
    this.toast.show(wasEdit ? 'The certificate was changed.' : 'The certificate was added.');
    const added = list.machines.find((m) => !before.has(m.requestItemId));
    if (!wasEdit && added) this.reveal(added.requestItemId);
  }

  /** Scrolls to a row and lights it for a moment, so nobody has to look for it. */
  protected reveal(id: string): void {
    this.highlight.set(id);
    setTimeout(() => document.getElementById('m-' + id)?.scrollIntoView?.({ block: 'center', behavior: 'smooth' }));
    setTimeout(() => this.highlight.set(null), 2600);
  }
  protected goTo(id: string | null): void {
    if (id) this.reveal(id);
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

  /** Fetches the certificate as it would print and shows it in the viewer (the file needs the bearer token, so it goes through HttpClient). */
  protected preview(m: MachineSummary): void {
    if (!m.pdfUrl || this.fetching() !== null) return;
    this.fetching.set(m.requestItemId);
    this.error.set(null);
    this.api.certificateFile(m.pdfUrl).subscribe({
      next: (b) => {
        this.fetching.set(null);
        const raw = URL.createObjectURL(new Blob([b], { type: 'application/pdf' }));
        this.viewer.set({ raw, url: this.sanitizer.bypassSecurityTrustResourceUrl(raw), name: m.name, draft: m.status !== 'approved' && m.status !== 'delivered' });
      },
      error: async (e: unknown) => {
        this.fetching.set(null);
        this.error.set(await apiMessageAsync(e));
      },
    });
  }
  protected closeViewer(): void {
    const v = this.viewer();
    if (v) URL.revokeObjectURL(v.raw);
    this.viewer.set(null);
  }
  protected openTab(v: { raw: string }): void {
    window.open(v.raw, '_blank', 'noopener');
  }

  protected zip(): void {
    this.fetching.set('zip');
    this.error.set(null);
    this.api.previewZipFile(this.requestId()).subscribe({
      next: (r) => {
        this.fetching.set(null);
        saveBlob(r.blob, r.fileName);
      },
      error: async (e: unknown) => {
        this.fetching.set(null);
        this.error.set(await apiMessageAsync(e));
      },
    });
  }

  /** With every certificate saved the inspector confirms first; with unsaved ones the API is asked at once so it names what is missing. */
  protected askSubmit(): void {
    this.problems.set([]);
    if (this.unsaved() > 0) this.submit();
    else this.confirming.set(true);
  }

  protected submit(): void {
    if (this.busy()) return;
    this.confirming.set(false);
    this.busy.set(true);
    this.error.set(null);
    this.problems.set([]);
    this.api.submitAll(this.requestId()).subscribe({
      next: (list) => {
        this.job.set(list);
        this.busy.set(false);
        this.toast.show('The certificates were sent to your technical manager.');
      },
      error: (e: unknown) => {
        this.busy.set(false);
        const list = e instanceof HttpErrorResponse && e.status === 409 ? (e.error as { problems?: unknown } | null)?.problems : null;
        if (Array.isArray(list) && list.length) this.problems.set(this.link(list.map(String)));
        else this.error.set(apiMessage(e));
      },
    });
  }

  /** Each problem names its machine first ("Forklift Truck: ..."): match it to a row so it can be opened. */
  private link(texts: string[]): { text: string; id: string | null }[] {
    const used = new Set<string>();
    return texts.map((text) => {
      const hit = (this.job()?.machines ?? [])
        .filter((m) => !used.has(m.requestItemId) && text.startsWith(m.name))
        .sort((a, b) => b.name.length - a.name.length)[0];
      if (hit) used.add(hit.requestItemId);
      return { text, id: hit?.requestItemId ?? null };
    });
  }
}
