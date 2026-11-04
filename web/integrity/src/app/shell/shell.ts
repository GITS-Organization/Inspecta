import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BackendApi } from '../api/backend.api';
import { AuthService } from '../core/auth.service';
import { BrandingService } from '../core/branding.service';
import { Role } from '../core/models';
import { DateRangeBar } from './date-range-bar';
import { ToastService } from '../core/toast.service';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  roles: Role[];
  badge?: boolean;
}

const NAV: NavItem[] = [
  { path: '/overview', label: 'Overview', icon: 'M4 13h6V4H4zM14 20h6v-9h-6zM14 8h6V4h-6zM4 20h6v-4H4z', roles: ['admin', 'technical_manager'] },
  { path: '/review', label: 'To review', icon: 'M5 12l4 4 10-10', roles: ['admin', 'technical_manager'], badge: true },
  { path: '/jobs', label: 'My jobs', icon: 'M9 5h10v14H9zM5 9v10h10', roles: ['inspector'] },
  { path: '/requests', label: 'Requests', icon: 'M4 6h16v12H4zM4 7l8 6 8-6', roles: ['coordinator'] },
  { path: '/quotations', label: 'Quotations', icon: 'M7 3h8l4 4v14H7zM15 3v4h4M10 12h6M10 16h6', roles: ['coordinator'] },
  { path: '/queue', label: 'Jobs', icon: 'M9 5h10v14H9zM5 9v10h10', roles: ['coordinator'] },
  { path: '/clients', label: 'Clients', icon: 'M16 11a4 4 0 1 0-8 0 4 4 0 0 0 8 0zM4 21c0-4 3.6-6 8-6s8 2 8 6', roles: ['coordinator'] },
  { path: '/mailbox', label: 'Mailbox', icon: 'M4 6h16v12H4zM4 7l8 6 8-6', roles: ['admin', 'technical_manager'] },
  { path: '/create', label: 'Create certification', icon: 'M12 5v14M5 12h14', roles: ['admin', 'technical_manager', 'inspector'] },
];

const ROLE_LABEL: Record<Role, string> = {
  admin: 'Admin',
  technical_manager: 'Technical manager',
  coordinator: 'Coordinator',
  inspector: 'Inspector',
};

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, DateRangeBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly branding = inject(BrandingService);
  protected readonly toast = inject(ToastService);
  private readonly api = inject(BackendApi);

  protected readonly items = computed(() => NAV.filter((n) => this.auth.role() && n.roles.includes(this.auth.role()!)));
  protected readonly roleLabel = computed(() => (this.auth.role() ? ROLE_LABEL[this.auth.role()!] : ''));
  protected readonly reviewCount = signal<number | null>(null);

  constructor() {
    effect(() => {
      const r = this.auth.role();
      if (r === 'technical_manager' || r === 'admin') {
        this.api.toReview().subscribe({
          next: (rows) => this.reviewCount.set(rows.length),
          error: () => this.reviewCount.set(null),
        });
      }
    });
  }
}
