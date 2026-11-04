import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { Client } from '../../api/coordinator.models';
import { problemMessage } from '../../core/problem';

@Component({
  selector: 'app-clients',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="head">
      <div>
        <h1>Clients</h1>
        <p class="muted">Your clients, their contacts, machines and certificates</p>
      </div>
      <a class="btn go" routerLink="/clients/new">New client</a>
    </div>

    <div class="toolbar">
      <div class="field">
        <label for="cs">Search</label>
        <input id="cs" type="text" placeholder="Name, location or contact" [value]="query()" (input)="query.set($any($event.target).value)" />
      </div>
      <label class="check-line"><input type="checkbox" [checked]="archived()" (change)="toggleArchived($any($event.target).checked)" /> Show archived clients</label>
    </div>

    <div aria-live="polite">
      @if (error(); as msg) {
        <div class="error" role="alert">{{ msg }}</div>
      } @else if (loading()) {
        <p class="muted empty">Loading clients…</p>
      } @else if (!shown().length) {
        <p class="empty">{{ query() ? 'No client matches your search.' : 'No clients yet. Add the first one.' }}</p>
      } @else {
        @for (c of shown(); track c.id) {
          <div class="job">
            <div>
              <b>{{ c.name }}</b>
              <div class="muted">
                {{ c.location || 'No location' }}
                @if (c.contacts.length) { · {{ c.contacts[0].name }}{{ c.contacts.length > 1 ? ' and ' + (c.contacts.length - 1) + ' more' : '' }} }
              </div>
            </div>
            <span class="rowline">
              @if (c.hasContract) { <span class="chip g">Contract</span> }
              @if (c.archivedAt) { <span class="chip">Archived</span> }
            </span>
            <a class="btn ghost" [routerLink]="['/clients', c.id]">Open</a>
          </div>
        }
      }
    </div>
  `,
})
export class Clients {
  private readonly api = inject(CoordinatorApi);
  protected readonly query = signal('');
  protected readonly archived = signal(false);
  protected readonly rows = signal<Client[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly shown = computed(() => {
    const q = this.query().trim().toLowerCase();
    return this.rows().filter((c) => !q || `${c.name} ${c.location ?? ''} ${c.contacts.map((x) => x.name).join(' ')}`.toLowerCase().includes(q));
  });

  constructor() {
    this.load();
  }

  protected toggleArchived(v: boolean): void {
    this.archived.set(v);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.clients(this.archived()).subscribe({
      next: (r) => {
        this.rows.set(r);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The clients could not be loaded.'));
        this.loading.set(false);
      },
    });
  }
}
