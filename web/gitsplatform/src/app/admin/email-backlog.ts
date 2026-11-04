import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminApi } from '../core/admin-api.service';
import { AuthUser, CleanupEmailBacklogResponse } from '../core/api.models';
import { problemMessage } from '../core/problem';

/** Two steps on purpose: a dry run reports the count, and only then can the delete be confirmed. */
@Component({
  selector: 'app-email-backlog',
  imports: [FormsModule],
  template: `
    <h1>Email backlog</h1>
    <p class="muted lead">Remove email requests that were imported in bulk by mistake. Only requests nobody started working on are removed; anything a coordinator edited is kept.</p>
    <form class="box" (ngSubmit)="count()">
      <div class="row2">
        <div class="field">
          <label for="before">Imported before</label>
          <input id="before" name="before" type="date" [(ngModel)]="before" (ngModelChange)="reset()" required />
        </div>
        <div class="field">
          <label for="team">Company</label>
          <select id="team" name="team" [(ngModel)]="managerId" (ngModelChange)="reset()">
            <option value="">All companies</option>
            @for (m of managers(); track m.id) { <option [value]="m.id">{{ m.fullName }} ({{ m.email }})</option> }
          </select>
        </div>
      </div>
      @if (error(); as e) { <p class="notice error" role="alert">{{ e }}</p> }
      <button class="btn" type="submit" [disabled]="!before || busy()">{{ busy() && !preview() ? 'Counting…' : 'Count requests' }}</button>
    </form>

    @if (preview(); as p) {
      <section class="box" aria-live="polite">
        @if (done()) {
          <p class="notice ok">{{ p.deleted }} {{ p.deleted === 1 ? 'request was' : 'requests were' }} deleted.</p>
        } @else if (p.matched === 0) {
          <p>Nothing matches. No untouched requests were imported before that date.</p>
        } @else {
          <p><b>{{ p.matched }}</b> {{ p.matched === 1 ? 'request' : 'requests' }} would be deleted for good.</p>
          <div class="check field">
            <input id="sure" type="checkbox" [(ngModel)]="sure" name="sure" />
            <label for="sure">I understand this cannot be undone.</label>
          </div>
          <button class="btn danger" type="button" [disabled]="!sure || busy()" (click)="remove()">{{ busy() ? 'Deleting…' : 'Delete ' + p.matched + ' ' + (p.matched === 1 ? 'request' : 'requests') }}</button>
        }
      </section>
    }
  `,
  styles: `
    h1 { font-size: 44px; }
    .lead { max-width: 40em; margin: 8px 0 24px; }
    .box { background: #fff; border: 1.5px solid var(--ink); border-radius: 14px; padding: 22px; max-width: 640px; margin-bottom: 20px; }
    .danger { background: var(--bad); &:hover { background: #8c1d17; } }
  `,
})
export class EmailBacklog {
  private readonly api = inject(AdminApi);
  protected before = '';
  protected managerId = '';
  protected sure = false;

  protected readonly managers = signal<AuthUser[]>([]);
  protected readonly preview = signal<CleanupEmailBacklogResponse | null>(null);
  protected readonly done = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // Only a convenience for the company picker: if it fails, "All companies" still works.
    this.api.listUsers({ role: 'technical_manager' }).subscribe({ next: (list) => this.managers.set(list), error: () => undefined });
  }

  protected reset(): void {
    this.preview.set(null);
    this.done.set(false);
    this.sure = false;
    this.error.set(null);
  }

  protected count(): void { this.run(true); }

  protected remove(): void { if (this.sure) this.run(false); }

  private run(dryRun: boolean): void {
    if (!this.before) return;
    this.busy.set(true);
    this.error.set(null);
    this.api.cleanupEmailBacklog({
      createdBefore: new Date(`${this.before}T00:00:00Z`).toISOString(),
      ...(this.managerId ? { managerId: this.managerId } : {}),
      dryRun,
    }).subscribe({
      next: (result) => { this.preview.set(result); this.done.set(!dryRun); this.sure = false; this.busy.set(false); },
      error: (error: unknown) => { this.error.set(problemMessage(error)); this.busy.set(false); },
    });
  }
}
