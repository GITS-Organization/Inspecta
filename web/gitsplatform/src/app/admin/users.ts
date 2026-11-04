import { Component, computed, inject, signal } from '@angular/core';
import { AdminApi } from '../core/admin-api.service';
import { AuthUser, UserRole } from '../core/api.models';
import { AuthService } from '../core/auth.service';
import { problemMessage } from '../core/problem';

const ROLE_LABEL: Record<UserRole, string> = {
  admin: 'Admin',
  technical_manager: 'Technical manager',
  coordinator: 'Coordinator',
  inspector: 'Inspector',
};

@Component({
  selector: 'app-users',
  template: `
    <div class="head">
      <div><h1>Users</h1><p class="muted">Everyone with an account on the platform</p></div>
      <div class="filter">
        <label for="role">Show</label>
        <select id="role" (change)="role.set($any($event.target).value)">
          <option value="">Everyone</option>
          @for (r of roles; track r) { <option [value]="r">{{ label(r) }}</option> }
        </select>
      </div>
    </div>
    @if (error(); as message) { <p class="notice error" role="alert">{{ message }}</p> }
    @if (loading()) { <p class="muted">Loading users…</p> }
    @else if (visible().length === 0 && !error()) { <p class="muted">No users match this filter.</p> }
    @else {
      <div class="table-scroll">
        <table>
          <thead><tr><th>Name</th><th>Role</th><th>Company</th><th>Status</th><th><span class="sr-only">Actions</span></th></tr></thead>
          <tbody>
            @for (u of visible(); track u.id) {
              <tr>
                <td><b>{{ u.fullName }}</b><br /><span class="muted">{{ u.email }}</span></td>
                <td>{{ label(u.role) }}</td>
                <td>{{ companyOf(u) }}</td>
                <td>@if (u.isActive) { <span class="chip">Active</span> } @else { <span class="chip g">Disabled</span> }</td>
                <td>
                  @if (u.id !== me()?.id) {
                    <button type="button" class="btn line small" [disabled]="busyId() === u.id" (click)="toggle(u)">
                      {{ u.isActive ? 'Disable' : 'Enable' }}<span class="sr-only"> {{ u.fullName }}</span>
                    </button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    .head { display: flex; justify-content: space-between; align-items: end; gap: 16px; flex-wrap: wrap; margin-bottom: 24px; }
    h1 { font-size: 44px; }
    .filter { min-width: 200px; }
  `,
})
export class Users {
  private readonly api = inject(AdminApi);
  protected readonly me = inject(AuthService).user;
  protected readonly roles: UserRole[] = ['technical_manager', 'coordinator', 'inspector', 'admin'];

  protected readonly users = signal<AuthUser[]>([]);
  protected readonly role = signal('');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly busyId = signal<string | null>(null);

  protected readonly visible = computed(() => this.users().filter((u) => !this.role() || u.role === this.role()));
  private readonly managerNames = computed(() => new Map(this.users().filter((u) => u.role === 'technical_manager').map((u) => [u.id, u.fullName])));

  constructor() {
    this.api.listUsers().subscribe({
      next: (users) => { this.users.set(users); this.loading.set(false); },
      error: (error: unknown) => { this.error.set(problemMessage(error)); this.loading.set(false); },
    });
  }

  protected label(role: UserRole): string { return ROLE_LABEL[role]; }

  protected companyOf(user: AuthUser): string {
    if (user.role === 'technical_manager') return 'Own team';
    return user.managerId ? `Team of ${this.managerNames().get(user.managerId) ?? 'a technical manager'}` : '—';
  }

  protected toggle(user: AuthUser): void {
    this.busyId.set(user.id);
    this.error.set(null);
    this.api.setUserActive(user.id, !user.isActive).subscribe({
      next: () => {
        this.users.update((list) => list.map((u) => (u.id === user.id ? { ...u, isActive: !u.isActive } : u)));
        this.busyId.set(null);
      },
      error: (error: unknown) => { this.error.set(problemMessage(error)); this.busyId.set(null); },
    });
  }
}
