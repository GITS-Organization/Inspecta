import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RequestKind } from '../core/models';

export const KIND_LABEL: Record<RequestKind, string> = { standard: 'Standard', man_day: 'Man day', direct: 'Direct certificate', contract: 'Contract' };

/** True for the kinds that have no quotation of their own: the price 0 of their placeholder is not money. */
export const hasNoPrice = (k: RequestKind | null | undefined): boolean => k === 'man_day' || k === 'direct' || k === 'contract';

/** A small tag that tells a job apart from a standard one. Nothing is drawn for a standard job. */
@Component({
  selector: 'app-kind-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (label(); as l) { <span class="kind" [class.dc]="kind() === 'direct'">{{ l }}</span> }`,
  styles: `
    .kind {
      display: inline-block;
      padding: 1px 9px;
      border-radius: 4px;
      font-family: var(--display);
      font-weight: 600;
      font-size: 13px;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      white-space: nowrap;
      background: var(--ink);
      color: #fff;
      &.dc {
        background: var(--teal);
      }
    }
  `,
})
export class KindBadge {
  readonly kind = input<RequestKind | null | undefined>();
  protected readonly label = computed(() => {
    const k = this.kind();
    return k && k !== 'standard' ? KIND_LABEL[k] : null;
  });
}
