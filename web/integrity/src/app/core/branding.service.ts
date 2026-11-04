import { Injectable, computed, inject, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export const BUILT_IN_LOGO = 'assets/integrity-logo.png';

/**
 * The identity shown in the app. It starts with the built-in Integrity logo and, for people who belong to a team, tries the
 * team's own logo from the public `GET /branding/{managerId}/logo.png`; when that is missing (404) it falls back to the built-in one.
 * Colours stay on the built-in tokens: the backend has no public endpoint for them yet.
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly auth = inject(AuthService);
  private readonly failed = signal<string | null>(null);

  private readonly teamLogo = computed(() => {
    const u = this.auth.user();
    if (!u) return null;
    const teamId = u.role === 'technical_manager' ? u.id : u.role === 'admin' ? null : u.managerId;
    return teamId ? `${environment.apiBase}/branding/${teamId}/logo.png` : null;
  });

  readonly logo = computed(() => {
    const team = this.teamLogo();
    return team && this.failed() !== team ? team : BUILT_IN_LOGO;
  });

  /** Call from the `(error)` of an `<img>` showing the logo. */
  logoFailed(): void {
    this.failed.set(this.teamLogo());
  }
}
