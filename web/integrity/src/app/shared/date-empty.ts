import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DateRangeService } from '../core/date-range.service';

/** The empty state to show instead of the usual one when the date filter may be the reason nothing is listed. */
@Component({
  selector: 'app-date-empty',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="de" role="status">
      <h2>Nothing in these dates.</h2>
      <p class="muted">The date filter is on. {{ range.label() }}. Clear the dates to see everything.</p>
      <button type="button" class="btn" (click)="range.clear()">Clear dates</button>
    </div>
  `,
  styles: `
    .de {
      border: 1.5px dashed var(--amber);
      background: var(--amber-bg);
      border-radius: 12px;
      padding: 28px 22px;
      text-align: center;
      margin-top: 18px;
      h2 {
        font-size: 26px;
        margin: 0 0 4px;
      }
      p {
        margin: 0 0 14px;
      }
    }
  `,
})
export class DateEmpty {
  protected readonly range = inject(DateRangeService);
}
