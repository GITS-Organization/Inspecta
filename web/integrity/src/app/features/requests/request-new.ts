import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { Client, Equipment, SaveRequestItem } from '../../api/coordinator.models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { ItemsEditor } from '../../shared/items-editor';

/** A request the coordinator enters by hand (a phone call, a visit). It opens the same request screen afterwards. */
@Component({
  selector: 'app-request-new',
  imports: [RouterLink, ItemsEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="back" routerLink="/requests">Back to requests</a>
    <div class="head">
      <div>
        <h1>New request</h1>
        <p class="muted">Enter a request that did not come by email. You can prepare its quotation on the next screen.</p>
      </div>
    </div>
    @if (error(); as msg) {
      <div class="error" role="alert">{{ msg }}</div>
    }
    <div class="narrow">
      <div class="row2">
        <div class="field">
          <label for="cl">Client</label>
          <select id="cl" (change)="pickClient($any($event.target).value)">
            <option value="">Another client (type the name)</option>
            @for (c of clients(); track c.id) {
              <option [value]="c.id" [selected]="c.id === clientId()">{{ c.name }}</option>
            }
          </select>
        </div>
        <div class="field">
          <label for="cn">Client name</label>
          <input id="cn" type="text" [disabled]="!!clientId()" [value]="clientName()" (input)="clientName.set($any($event.target).value)" />
        </div>
      </div>
      <div class="row2">
        <div class="field">
          <label for="lo">Location</label>
          <input id="lo" type="text" [value]="location()" (input)="location.set($any($event.target).value)" />
        </div>
        <div class="field">
          <label for="pd">Preferred date</label>
          <input id="pd" type="date" [value]="preferred()" (input)="preferred.set($any($event.target).value)" />
        </div>
      </div>
      <div class="field">
        <label for="su">Subject</label>
        <input id="su" type="text" placeholder="For example: Annual inspection of cranes" [value]="subject()" (input)="subject.set($any($event.target).value)" />
      </div>
      <div class="field">
        <label for="an">Access notes</label>
        <textarea id="an" [value]="access()" (input)="access.set($any($event.target).value)"></textarea>
      </div>
      <h2 class="h2">Items</h2>
      <app-items-editor [(items)]="items" [equipment]="equipment()" />
      <div class="actions">
        <button type="button" class="btn go" [disabled]="busy()" (click)="create()">Create the request</button>
      </div>
    </div>
  `,
  styles: `
    .narrow {
      max-width: 820px;
    }
    .h2 {
      font-size: 24px;
      margin: 12px 0;
    }
  `,
})
export class RequestNew implements OnInit {
  private readonly api = inject(CoordinatorApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly clients = signal<Client[]>([]);
  protected readonly equipment = signal<Equipment[]>([]);
  protected readonly clientId = signal<string | null>(null);
  protected readonly clientName = signal('');
  protected readonly location = signal('');
  protected readonly preferred = signal('');
  protected readonly subject = signal('');
  protected readonly access = signal('');
  protected readonly items = signal<SaveRequestItem[]>([{ name: '', quantity: 1 }]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.clients().subscribe({ next: (c) => this.clients.set(c), error: (e: unknown) => this.error.set(problemMessage(e, 'The clients could not be loaded.')) });
  }

  protected pickClient(id: string): void {
    const c = this.clients().find((x) => x.id === id);
    this.clientId.set(c?.id ?? null);
    if (c) {
      this.clientName.set(c.name);
      if (!this.location().trim() && c.location) this.location.set(c.location);
      this.api.equipment(c.id).subscribe({ next: (e) => this.equipment.set(e), error: () => undefined });
    } else this.equipment.set([]);
  }

  protected create(): void {
    if (this.busy()) return;
    if (!this.clientName().trim() || !this.location().trim()) {
      this.error.set('Fill in the client and the location.');
      return;
    }
    this.error.set(null);
    this.busy.set(true);
    this.api
      .createRequest({
        clientId: this.clientId(),
        clientName: this.clientName().trim(),
        location: this.location().trim(),
        subject: this.subject().trim() || null,
        preferredDate: this.preferred() ? this.preferred() + 'T00:00:00Z' : null,
        accessNotes: this.access().trim() || null,
        items: this.items().filter((i) => i.name.trim()),
      })
      .subscribe({
        next: (r) => {
          this.toast.show('Request created.');
          void this.router.navigate(['/requests', r.id]);
        },
        error: (e: unknown) => {
          this.busy.set(false);
          this.error.set(problemMessage(e));
        },
      });
  }
}
