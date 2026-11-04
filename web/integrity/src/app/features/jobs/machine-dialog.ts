import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { BackendApi } from '../../api/backend.api';
import { Equipment } from '../../api/coordinator.models';
import { JobMachineDetails, MachineList, MachineSummary } from '../../core/models';
import { apiMessage } from '../../core/problem';
import { MachineKinds } from '../../shared/machine-kinds';
import { MachineNamePicker } from '../../shared/machine-name-picker';

/** The text of an optional input as the API wants it: trimmed, and null when empty. */
const opt = (v: string): string | null => v.trim() || null;

/**
 * One dialog to add a machine to a job that is under way (a registered machine of the client, or a new one that is registered
 * for the client at once) or to edit the details of a machine already in it.
 */
@Component({
  selector: 'app-machine-dialog',
  imports: [MachineNamePicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'closed.emit()' },
  template: `
    <div class="scrim" (click)="closed.emit()"></div>
    <div class="dlg" role="dialog" aria-modal="true" aria-labelledby="md-title" #box>
      <header>
        <h2 id="md-title">{{ editing() ? 'Edit ' + editing()!.name : first() ? 'Add the first machine' : 'Add a machine' }}</h2>
        <button type="button" class="x" aria-label="Close" (click)="closed.emit()">×</button>
      </header>

      @if (!editing()) {
        <div class="seg" role="tablist" aria-label="Kind of machine">
          <button type="button" role="tab" [attr.aria-selected]="tab() === 'registered'" (click)="tab.set('registered')">
            Registered machines @if (available() !== null) { <span class="n">{{ available()!.length }}</span> }
          </button>
          <button type="button" role="tab" [attr.aria-selected]="tab() === 'new'" (click)="tab.set('new')">New machine</button>
        </div>
      }

      @if (error(); as msg) {
        <div class="error" role="alert">{{ msg }}</div>
      }

      @if (!editing() && tab() === 'registered') {
        <p class="muted lead">The client's machines that are not in this job yet. Pick one to certify it on this visit.</p>
        <label class="sr" for="md-q">Search the client's machines</label>
        <input id="md-q" type="search" autocomplete="off" placeholder="Search by name, serial number or model" [value]="query()" (input)="query.set($any($event.target).value)" />
        <ul class="reg" aria-label="Registered machines">
          @if (available() === null) {
            <li class="muted none">Loading the client's machines…</li>
          } @else {
            @for (e of matches(); track e.id) {
              <li>
                <div>
                  <b>{{ e.name }}</b>
                  <span class="muted">{{ describe(e) }}</span>
                </div>
                <button type="button" class="btn small go" [disabled]="busy()" (click)="addRegistered(e)">Add</button>
              </li>
            } @empty {
              <li class="none muted">
                {{ query() ? 'No registered machine matches.' : 'Every registered machine of this client is already in the job, or none is registered yet.' }}
                <button type="button" class="lnk" (click)="tab.set('new')">Add a new machine</button>
              </li>
            }
          }
        </ul>
      } @else {
        <form (submit)="$event.preventDefault(); save()" novalidate>
          @if (!editing()) {
            <p class="muted lead">A new machine is registered for the client at once, so its certificate can be made now.</p>
          }
          <div class="field">
            <app-machine-name-picker inputId="md-n" [value]="name()" (valueChange)="name.set($event)" />
          </div>
          @if (gear()) {
            <p class="gear" role="note">
              <b>{{ name().trim() }} is a gear.</b> A gear is inspected in numbers: give the quantity here, then divide it into certificates and write their serial numbers on the job screen. It is not added to the client's list of machines.
            </p>
          }
          <div class="row2">
            @if (gear()) {
              <div class="field">
                <label for="md-q2">Quantity</label>
                <input id="md-q2" type="number" min="1" max="10000" [value]="quantity()" (input)="quantity.set(+$any($event.target).value || 1)" />
              </div>
            } @else {
              <div class="field">
                <label for="md-s">Serial number</label>
                <input id="md-s" type="text" maxlength="600" [value]="serial()" (input)="serial.set($any($event.target).value)" />
              </div>
            }
            <div class="field">
              <label for="md-c">SWL</label>
              <input id="md-c" type="text" maxlength="200" [value]="capacity()" (input)="capacity.set($any($event.target).value)" />
            </div>
          </div>
          <div class="row2">
            <div class="field">
              <label for="md-m">Manufacturer</label>
              <input id="md-m" type="text" maxlength="200" [value]="manufacturer()" (input)="manufacturer.set($any($event.target).value)" />
            </div>
            <div class="field">
              <label for="md-o">Model</label>
              <input id="md-o" type="text" maxlength="200" [value]="model()" (input)="model.set($any($event.target).value)" />
            </div>
          </div>
          <div class="field">
            <label for="md-i">What is wrong, if the client said</label>
            <input id="md-i" type="text" maxlength="4000" [value]="issue()" (input)="issue.set($any($event.target).value)" />
            @if (editing()) {
              <small>This text is not shown in the list. What you leave empty here is cleared.</small>
            }
          </div>
          <div class="actions">
            <button type="submit" class="btn go" [disabled]="busy()">{{ busy() ? 'Saving…' : editing() ? 'Save the machine' : 'Add and register it' }}</button>
            <button type="button" class="btn ghost" (click)="closed.emit()">Cancel</button>
          </div>
        </form>
      }
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
      width: min(720px, 100%);
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
        font-size: 30px;
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
      &:hover {
        color: var(--ink);
      }
    }
    .seg {
      display: flex;
      gap: 4px;
      background: var(--band);
      border-radius: 9px;
      padding: 4px;
      margin-bottom: 14px;
      button {
        flex: 1;
        border: 0;
        background: none;
        padding: 9px 12px;
        border-radius: 6px;
        font-weight: 600;
        color: var(--muted);
        cursor: pointer;
        &[aria-selected='true'] {
          background: #fff;
          color: var(--ink);
          box-shadow: 0 1px 3px rgb(19 40 93 / 0.18);
        }
      }
      .n {
        font-family: var(--display);
        margin-left: 6px;
        color: var(--green-d);
      }
    }
    .lead {
      margin: 0 0 12px;
    }
    .reg {
      list-style: none;
      margin: 10px 0 0;
      padding: 0;
      max-height: 340px;
      overflow-y: auto;
      border: 1.5px solid var(--line);
      border-radius: 8px;
      li {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 14px;
        padding: 10px 14px;
        border-bottom: 1px solid var(--line);
        &:last-child {
          border-bottom: 0;
        }
        b {
          display: block;
          color: var(--ink);
        }
      }
      .none {
        display: block;
      }
    }
    .lnk {
      background: none;
      border: 0;
      color: var(--ink);
      text-decoration: underline;
      font-weight: 600;
      cursor: pointer;
      display: block;
      margin-top: 6px;
      padding: 0;
    }
    .gear {
      background: var(--amber-bg);
      border-radius: 8px;
      padding: 10px 14px;
      margin: 0 0 16px;
      b {
        color: var(--amber);
      }
    }
    @media (max-width: 640px) {
      .row2,
      .row3 {
        grid-template-columns: 1fr;
        gap: 0;
      }
    }
  `,
})
export class MachineDialog implements OnInit {
  readonly requestId = input.required<string>();
  /** The machine to edit; empty to add one. */
  readonly editing = input<MachineSummary | null>(null);
  /** The job has no machine yet: the title says so. */
  readonly first = input(false);
  readonly done = output<MachineList>();
  readonly closed = output<void>();

  private readonly api = inject(BackendApi);
  private readonly box = viewChild<ElementRef<HTMLElement>>('box');

  protected readonly tab = signal<'registered' | 'new'>('registered');
  protected readonly available = signal<Equipment[] | null>(null);
  private readonly kinds = inject(MachineKinds);
  protected readonly query = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly name = signal('');
  protected readonly type = signal('');
  protected readonly quantity = signal(1);
  protected readonly serial = signal('');
  protected readonly capacity = signal('');
  protected readonly manufacturer = signal('');
  protected readonly model = signal('');
  protected readonly issue = signal('');

  protected readonly gear = computed(() => this.kinds.isGear(this.name()));
  protected readonly matches = computed(() => {
    const q = this.query().trim().toLowerCase();
    return (this.available() ?? []).filter((e) =>
      !q || [e.name, e.serialNumber, e.model, e.manufacturer, e.category].some((v) => (v ?? '').toLowerCase().includes(q)),
    );
  });

  ngOnInit(): void {
    const m = this.editing();
    if (m) {
      this.name.set(m.name);
      this.type.set(m.type ?? '');
      this.quantity.set(m.quantity);
      this.serial.set(m.serialNumber ?? '');
      this.capacity.set(m.capacity ?? '');
      this.manufacturer.set(m.manufacturer ?? '');
      this.model.set(m.model ?? '');
    } else {
      this.api.availableMachines(this.requestId()).subscribe({
        next: (list) => {
          this.available.set(list);
          if (!list.length) this.tab.set('new');
        },
        error: (e: unknown) => {
          this.available.set([]);
          this.tab.set('new');
          this.error.set(apiMessage(e, "The client's machines could not be loaded."));
        },
      });
    }
    this.kinds.load();
    queueMicrotask(() => this.box()?.nativeElement.querySelector<HTMLElement>('input')?.focus());
  }

  protected describe(e: Equipment): string {
    return [e.serialNumber && 'S/N ' + e.serialNumber, e.model, e.capacity].filter(Boolean).join(' · ') || 'No details on file';
  }

  protected addRegistered(e: Equipment): void {
    this.send(this.api.addMachine(this.requestId(), { equipmentId: e.id }));
  }

  protected save(): void {
    if (this.busy()) return;
    if (!this.name().trim()) return void this.error.set('Choose the name of the machine.');
    const body: JobMachineDetails = {
      name: this.name().trim(),
      quantity: this.gear() ? Math.max(1, Math.min(10000, Math.round(this.quantity()))) : 1,
      type: null,
      capacity: opt(this.capacity()),
      serialNumber: this.gear() ? null : opt(this.serial()),
      manufacturer: opt(this.manufacturer()),
      model: opt(this.model()),
      issueDescription: opt(this.issue()),
    };
    const m = this.editing();
    this.send(m ? this.api.editMachine(this.requestId(), m.requestItemId, body) : this.api.addMachine(this.requestId(), body));
  }

  private send(call: ReturnType<BackendApi['addMachine']>): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
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
