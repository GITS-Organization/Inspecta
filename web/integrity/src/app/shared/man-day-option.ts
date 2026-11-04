import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

/**
 * "Man day (one inspector)": one extra priced line in the quotation. `days` is 0 when it is off (the API removes the line when it
 * receives 0) and 1 to 365 when it is on.
 */
@Component({
  selector: 'app-man-day-option',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="md" [class.on]="days() > 0">
      @if (disabled()) {
        <div class="ro">
          <b>Man day (one inspector)</b>
          <span class="muted">{{ days() > 0 ? days() + (days() === 1 ? ' day' : ' days') + ', priced per day in the quotation' : 'Not asked for' }}</span>
        </div>
      } @else {
        <label class="tog" [for]="inputId() + '-on'">
          <input [id]="inputId() + '-on'" type="checkbox" [checked]="days() > 0" (change)="toggle($any($event.target).checked)" />
          <span><b>Man day (one inspector)</b></span>
        </label>
        @if (days() > 0) {
          <div class="days">
            <label [for]="inputId() + '-n'">Days</label>
            <input [id]="inputId() + '-n'" type="number" min="1" max="365" step="1" [value]="days()" (input)="setDays($any($event.target).value)" />
          </div>
        }
        <small>Adds a man day line to the quotation; you price it per day there.</small>
      }
    </div>
  `,
  styles: `
    .md {
      border: 1.5px dashed var(--line);
      border-radius: 10px;
      padding: 12px 16px;
      margin: 14px 0;
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 6px 16px;
      align-items: center;
      &.on {
        border: 1.5px solid var(--teal);
        background: rgb(27 143 166 / 0.06);
      }
      small {
        grid-column: 1 / -1;
        color: var(--muted);
      }
    }
    .tog {
      display: flex;
      align-items: center;
      gap: 10px;
      margin: 0;
      cursor: pointer;
      input {
        width: 20px;
        height: 20px;
        accent-color: var(--teal);
      }
    }
    .days {
      display: flex;
      align-items: center;
      gap: 8px;
      label {
        margin: 0;
      }
      input {
        width: 90px;
        padding: 8px 10px;
        border: 1.5px solid var(--line);
        border-radius: 8px;
      }
    }
    .ro {
      grid-column: 1 / -1;
      display: flex;
      justify-content: space-between;
      gap: 12px;
      flex-wrap: wrap;
    }
  `,
})
export class ManDayOption {
  readonly days = model(0);
  readonly disabled = input(false);
  readonly inputId = input('md');

  protected toggle(on: boolean): void {
    this.days.set(on ? Math.max(1, this.days()) : 0);
  }
  protected setDays(v: string): void {
    const n = Math.floor(Number(v));
    this.days.set(Number.isFinite(n) ? Math.min(365, Math.max(1, n)) : 1);
  }
}
