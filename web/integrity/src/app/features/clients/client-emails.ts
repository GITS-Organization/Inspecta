import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { CoordinatorApi } from '../../api/coordinator.api';
import { MailRule } from '../../api/coordinator.models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';

/** The sender addresses and domains that count as this client. Emails from them become requests straight away. */
@Component({
  selector: 'app-client-emails',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="h">Senders</h2>
    <p class="muted">Emails from these addresses or domains become requests of this client straight away. Adding one also moves its waiting unknown-sender emails into requests.</p>
    @if (error(); as msg) { <div class="error" role="alert">{{ msg }}</div> }
    @if (promoted(); as n) { <div class="callout ok" role="status"><b>{{ n }} waiting {{ n === 1 ? 'email became a request' : 'emails became requests' }}.</b></div> }

    <div class="toolbar">
      <div class="field grow">
        <label for="pt">Email address or domain</label>
        <input id="pt" type="text" placeholder="buyer@alnoor.com or alnoor.com" [value]="pattern()" (input)="pattern.set($any($event.target).value)" (keydown.enter)="add()" />
      </div>
      <button type="button" class="btn go" [disabled]="busy()" (click)="add()">Add the sender</button>
    </div>

    @if (loading()) {
      <p class="muted empty">Loading…</p>
    } @else if (!rules().length) {
      <p class="empty">No sender is set for this client yet.</p>
    } @else {
      @for (r of rules(); track r.id) {
        <div class="job">
          <div><b>{{ r.pattern }}</b><div class="muted">{{ r.isDomain ? 'Every address of this domain' : 'This address' }}</div></div>
          <span></span>
          <button type="button" class="btn warn small" [disabled]="busy()" (click)="remove(r)">Remove</button>
        </div>
      }
    }
  `,
  styles: `
    .h {
      margin: 18px 0 6px;
    }
    .grow {
      flex: 1;
      min-width: 260px;
    }
  `,
})
export class ClientEmails implements OnInit {
  readonly clientId = input.required<string>();
  private readonly api = inject(CoordinatorApi);
  private readonly toast = inject(ToastService);

  protected readonly rules = signal<MailRule[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly pattern = signal('');
  protected readonly promoted = signal(0);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.api.clientEmails(this.clientId()).subscribe({
      next: (r) => {
        this.rules.set(r);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The senders could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  protected add(): void {
    const p = this.pattern().trim();
    if (!p || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.api.addClientEmail(this.clientId(), p).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.pattern.set('');
        this.promoted.set(r.promotedRequests ?? 0);
        this.toast.show('The sender was added.');
        this.load();
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }

  protected remove(r: MailRule): void {
    this.busy.set(true);
    this.error.set(null);
    this.api.deleteClientEmail(this.clientId(), r.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.show('The sender was removed.');
        this.load();
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }
}
