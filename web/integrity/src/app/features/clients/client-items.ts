import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { CoordinatorApi } from '../../api/coordinator.api';
import { Equipment, ItemType, SaveEquipment } from '../../api/coordinator.models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { formatDate } from '../../shared/format';

const empty = (clientId: string): SaveEquipment => ({ clientId, name: '', itemType: 'equipment' });

/** The machines and gear of one client: add, edit and delete. The server decides what may be deleted. */
@Component({
  selector: 'app-client-items',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="head2">
      <h2>Items</h2>
      <button type="button" class="btn ghost small" (click)="startNew()">Add an item</button>
    </div>
    @if (error(); as msg) { <div class="error" role="alert">{{ msg }}</div> }

    @if (form(); as f) {
      <div class="itemcard" role="group" [attr.aria-label]="editingId() ? 'Edit the item' : 'New item'">
        <div class="row3">
          <div class="field"><label for="in">Name</label><input id="in" type="text" [value]="f.name" (input)="set({ name: $any($event.target).value })" /></div>
          <div class="field">
            <label for="it">Kind</label>
            <select id="it" (change)="set({ itemType: $any($event.target).value })">
              <option value="equipment" [selected]="f.itemType === 'equipment'">Equipment</option>
              <option value="gear" [selected]="f.itemType === 'gear'">Lifting gear</option>
              <option value="non_lifting" [selected]="f.itemType === 'non_lifting'">Not a lifting item</option>
            </select>
          </div>
          <div class="field"><label for="ic">Category</label><input id="ic" type="text" [value]="f.category ?? ''" (input)="set({ category: $any($event.target).value })" /></div>
        </div>
        <div class="row3">
          <div class="field"><label for="im">Manufacturer</label><input id="im" type="text" [value]="f.manufacturer ?? ''" (input)="set({ manufacturer: $any($event.target).value })" /></div>
          <div class="field"><label for="imo">Model</label><input id="imo" type="text" [value]="f.model ?? ''" (input)="set({ model: $any($event.target).value })" /></div>
          <div class="field"><label for="is">Serial number</label><input id="is" type="text" [value]="f.serialNumber ?? ''" (input)="set({ serialNumber: $any($event.target).value })" /></div>
        </div>
        <div class="row2">
          <div class="field"><label for="ica">Capacity</label><input id="ica" type="text" [value]="f.capacity ?? ''" (input)="set({ capacity: $any($event.target).value })" /></div>
          <div class="field"><label for="ist">Inspection standard</label><input id="ist" type="text" [value]="f.inspectionStandard ?? ''" (input)="set({ inspectionStandard: $any($event.target).value })" /></div>
        </div>
        <div class="row2">
          <div class="field"><label for="itt">Type of test</label><input id="itt" type="text" [value]="f.testType ?? ''" (input)="set({ testType: $any($event.target).value })" /></div>
          <div class="field"><label for="ino">Notes</label><input id="ino" type="text" [value]="f.notes ?? ''" (input)="set({ notes: $any($event.target).value })" /></div>
        </div>
        <div class="rowline">
          <button type="button" class="btn go" [disabled]="busy()" (click)="save()">{{ editingId() ? 'Save the item' : 'Add the item' }}</button>
          <button type="button" class="btn ghost" (click)="form.set(null)">Cancel</button>
        </div>
      </div>
    }

    @if (loading()) {
      <p class="muted empty">Loading items…</p>
    } @else if (!items().length) {
      <p class="empty">This client has no items yet.</p>
    } @else {
      <table>
        <thead><tr><th>Item</th><th>Serial</th><th>Last examined</th><th>Next due</th><th><span class="sr">Actions</span></th></tr></thead>
        <tbody>
          @for (e of items(); track e.id) {
            <tr>
              <td><b>{{ e.name }}</b><div class="muted sm">{{ e.manufacturer }}{{ e.model ? ' ' + e.model : '' }}</div></td>
              <td>{{ e.serialNumber || '—' }}</td>
              <td>{{ formatDate(e.lastInspectedOn) || '—' }}</td>
              <td>
                @if (e.nextInspectionDueAt) {
                  <span class="chip" [class.r]="overdue(e)">{{ formatDate(e.nextInspectionDueAt) }}</span>
                } @else { — }
              </td>
              <td class="act">
                <button type="button" class="btn ghost small" (click)="edit(e)">Edit</button>
                @if (confirmId() === e.id) {
                  <button type="button" class="btn warn small" [disabled]="busy()" (click)="remove(e)">Delete for good</button>
                } @else {
                  <button type="button" class="btn warn small" (click)="confirmId.set(e.id)">Delete</button>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: `
    .head2 {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      margin: 18px 0 12px;
    }
    .sm {
      font-size: 13px;
    }
    .act {
      white-space: nowrap;
      text-align: right;
      .btn + .btn {
        margin-left: 6px;
      }
    }
  `,
})
export class ClientItems implements OnInit {
  readonly clientId = input.required<string>();
  private readonly api = inject(CoordinatorApi);
  private readonly toast = inject(ToastService);
  protected readonly formatDate = formatDate;

  protected readonly items = signal<Equipment[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = signal<SaveEquipment | null>(null);
  protected readonly editingId = signal<string | null>(null);
  protected readonly confirmId = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected overdue = (e: Equipment) => !!e.nextInspectionDueAt && new Date(e.nextInspectionDueAt).getTime() < Date.now();

  private load(): void {
    this.api.equipment(this.clientId()).subscribe({
      next: (r) => {
        this.items.set(r);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The items could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  protected startNew(): void {
    this.editingId.set(null);
    this.form.set(empty(this.clientId()));
  }

  protected edit(e: Equipment): void {
    this.editingId.set(e.id);
    this.form.set({
      clientId: e.clientId, name: e.name, itemType: e.itemType, category: e.category, manufacturer: e.manufacturer, model: e.model, serialNumber: e.serialNumber,
      capacity: e.capacity, inspectionStandard: e.inspectionStandard, testType: e.testType, notes: e.notes,
    });
  }

  protected set(p: Partial<SaveEquipment> & { itemType?: ItemType }): void {
    this.form.update((f) => (f ? { ...f, ...p } : f));
  }

  protected save(): void {
    const f = this.form();
    if (!f || this.busy()) return;
    if (!f.name.trim()) {
      this.error.set('Give the item a name.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const id = this.editingId();
    (id ? this.api.updateEquipment(id, f) : this.api.createEquipment(f)).subscribe({
      next: () => {
        this.busy.set(false);
        this.form.set(null);
        this.toast.show(id ? 'The item was saved.' : 'The item was added.');
        this.load();
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }

  protected remove(e: Equipment): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.deleteEquipment(e.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.confirmId.set(null);
        this.toast.show('The item was deleted.');
        this.load();
      },
      error: (err: unknown) => {
        this.busy.set(false);
        this.confirmId.set(null);
        this.error.set(problemMessage(err));
      },
    });
  }
}
