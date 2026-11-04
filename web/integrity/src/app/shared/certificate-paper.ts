import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { BrandingService } from '../core/branding.service';

/** The certificate as it prints: the signature element of the app. Pure display, driven by inputs. */
@Component({
  selector: 'app-certificate-paper',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="paper" role="group" aria-label="Certificate preview">
      <div class="ph">
        <img [src]="branding.logo()" alt="" (error)="branding.logoFailed()" />
        <b>CERTIFICATE OF<br />THOROUGH EXAMINATION</b>
      </div>
      <div class="grid">
        <div><span>Client</span><b>{{ client() || '—' }}</b></div>
        <div><span>Item</span><b>{{ item() || '—' }}</b></div>
        <div><span>Manufacturer</span><b>{{ manufacturer() || '—' }}</b></div>
        <div><span>{{ serialLabel() }}</span><b>{{ serial() || '—' }}</b></div>
        <div><span>Examination</span><b>{{ examined() || '—' }}</b></div>
        <div><span>Next examination</span><b>{{ next() || '—' }}</b></div>
      </div>
      @if (serialsText()) {
        <div class="serials">
          <span>Serial numbers ({{ serialsCount() }})</span>
          <span class="serial-list">{{ serialsText() }}</span>
        </div>
      }
      <div class="foot">
        <div>
          <div class="qr" aria-hidden="true"></div>
          <div class="nonum">{{ number() || 'Draft · no number yet' }}</div>
        </div>
        <div class="stamp" [class.no]="stampKind() === 'no'" [class.dash]="stampKind() === 'dash'">
          @switch (stampKind()) {
            @case ('no') { NOT<br />SAFE }
            @case ('dash') { NO<br />RESULT }
            @default { SAFE<br />TO USE }
          }
        </div>
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .serial-list {
      color: var(--body);
      font-size: 10px;
    }
  `,
})
export class CertificatePaper {
  protected readonly branding = inject(BrandingService);
  readonly client = input<string | null | undefined>('');
  readonly item = input<string | null | undefined>('');
  readonly manufacturer = input<string | null | undefined>('');
  readonly serialLabel = input('Serial number');
  readonly serial = input<string | null | undefined>('');
  readonly examined = input<string | null | undefined>('');
  readonly next = input<string | null | undefined>('');
  readonly number = input<string | null | undefined>(null);
  readonly stamp = input<'safe' | 'no' | 'dash'>('safe');
  readonly serialsText = input<string>('');
  readonly serialsCount = input(0);
  protected readonly stampKind = computed(() => this.stamp());
}
