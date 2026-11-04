import { ChangeDetectionStrategy, Component, OnInit, inject, input, model } from '@angular/core';
import { MachineKinds } from './machine-kinds';
import { MachineNamePicker } from './machine-name-picker';
import { Equipment, SaveRequestItem } from '../api/coordinator.models';

/** Edits the equipment list of a request: each item can be typed or taken from the client's registered items. */
@Component({
  selector: 'app-items-editor',
  imports: [MachineNamePicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (it of items(); track $index) {
      <div class="itemcard">
        <div class="head2">
          <b>Item {{ $index + 1 }}</b>
          @if (!disabled()) {
            <button type="button" class="btn warn small" (click)="remove($index)">Remove</button>
          }
        </div>
        @if (equipment().length && !disabled()) {
          <div class="field">
            <label [for]="'eq' + $index">Take it from the client's items</label>
            <select [id]="'eq' + $index" (change)="pick($index, $any($event.target).value)">
              <option value="">Type it yourself</option>
              @for (e of equipment(); track e.id) {
                <option [value]="e.id" [selected]="e.id === it.equipmentId">{{ e.name }}{{ e.serialNumber ? ' · ' + e.serialNumber : '' }}</option>
              }
            </select>
          </div>
        }
        @let gear = kinds.isGear(it.name);
        <div class="field">
          <app-machine-name-picker [inputId]="'n' + $index" [disabled]="disabled()" [value]="it.name" (valueChange)="set($index, { name: $event, type: null })" />
        </div>
        <div class="row2">
          @if (gear) {
            <div class="field">
              <label [for]="'q' + $index">Quantity</label>
              <input [id]="'q' + $index" type="number" min="1" max="10000" [disabled]="disabled()" [value]="it.quantity" (input)="set($index, { quantity: +$any($event.target).value || 1 })" />
              <small>The inspector divides it into certificates on site and writes the serial numbers then.</small>
            </div>
          } @else {
            <div class="field">
              <label [for]="'s' + $index">Serial number</label>
              <input [id]="'s' + $index" type="text" [disabled]="disabled()" [value]="it.serialNumber ?? ''" (input)="set($index, { serialNumber: $any($event.target).value })" />
            </div>
          }
          <div class="field">
            <label [for]="'c' + $index">SWL</label>
            <input [id]="'c' + $index" type="text" [disabled]="disabled()" [value]="it.capacity ?? ''" (input)="set($index, { capacity: $any($event.target).value })" />
          </div>
          <div class="field">
            <label [for]="'i' + $index">What the client says is wrong</label>
            <input [id]="'i' + $index" type="text" [disabled]="disabled()" [value]="it.issueDescription ?? ''" (input)="set($index, { issueDescription: $any($event.target).value })" />
          </div>
        </div>
      </div>
    } @empty {
      <p class="muted">No items yet.</p>
    }
    @if (!disabled()) {
      <button type="button" class="btn ghost" (click)="add()">Add an item</button>
    }
  `,
  styles: `
    .head2 {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
      b {
        color: var(--ink);
      }
    }
    .field {
      margin-bottom: 10px;
    }
  `,
})
export class ItemsEditor implements OnInit {
  protected readonly kinds = inject(MachineKinds);

  ngOnInit(): void {
    this.kinds.load();
  }

  readonly items = model.required<SaveRequestItem[]>();
  readonly equipment = input<Equipment[]>([]);
  readonly disabled = input(false);

  protected add(): void {
    this.items.update((l) => [...l, { name: '', quantity: 1 }]);
  }
  protected remove(i: number): void {
    this.items.update((l) => l.filter((_, x) => x !== i));
  }
  protected set(i: number, patch: Partial<SaveRequestItem>): void {
    this.items.update((l) => l.map((it, x) => (x === i ? this.normalise({ ...it, ...patch }) : it)));
  }
  /** A machine that is not a gear is one unit; a gear has no serial number at this level (the inspector writes them per certificate). */
  private normalise(it: SaveRequestItem): SaveRequestItem {
    return this.kinds.isGear(it.name) ? { ...it, serialNumber: null } : { ...it, quantity: 1 };
  }
  protected pick(i: number, id: string): void {
    const e = this.equipment().find((x) => x.id === id);
    this.set(
      i,
      e
        ? { equipmentId: e.id, name: e.name, type: e.category ?? null, capacity: e.capacity, serialNumber: e.serialNumber }
        : { equipmentId: null },
    );
  }
}
