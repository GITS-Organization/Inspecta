import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, of, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResult, AuthUser, Role, Session } from './models';

const STORAGE_KEY = 'integrity.session';
const ROLES: readonly Role[] = ['admin', 'technical_manager', 'coordinator', 'inspector'];

/** Reads the `role` claim of a JWT without verifying it (the server verifies; this only drives the UI). */
export function roleFromToken(token: string | null | undefined): Role | null {
  if (!token) return null;
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(payload)
        .split('')
        .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
        .join(''),
    );
    const claims = JSON.parse(json) as Record<string, unknown>;
    const role = String(claims['role'] ?? '').toLowerCase();
    return (ROLES as readonly string[]).includes(role) ? (role as Role) : null;
  } catch {
    return null;
  }
}

/** Where each role starts after signing in. */
export function homeFor(role: Role | null): string {
  switch (role) {
    case 'admin':
    case 'technical_manager':
      return '/overview';
    case 'inspector':
      return '/jobs';
    case 'coordinator':
      return '/requests';
    default:
      return '/login';
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly base = environment.apiBase;

  private readonly _session = signal<Session | null>(this.load());
  readonly session = this._session.asReadonly();
  readonly user = computed<AuthUser | null>(() => this._session()?.user ?? null);
  readonly isSignedIn = computed(() => this._session() !== null);
  readonly role = computed<Role | null>(() => {
    const s = this._session();
    return s ? (roleFromToken(s.accessToken) ?? s.user.role) : null;
  });

  private refreshing$: Observable<Session> | null = null;

  accessToken(): string | null {
    return this._session()?.accessToken ?? null;
  }

  login(email: string, password: string): Observable<Session> {
    return this.http.post<AuthResult>(`${this.base}/auth/login`, { email, password }).pipe(tap((r) => this.store(r)));
  }

  /** One refresh at a time: concurrent callers share the same request (the refresh token rotates on use). */
  refresh(): Observable<Session> {
    const current = this._session();
    if (!current) return throwError(() => new Error('Not signed in'));
    if (!this.refreshing$) {
      this.refreshing$ = this.http
        .post<AuthResult>(`${this.base}/auth/refresh`, { refreshToken: current.refreshToken })
        .pipe(
          tap((r) => this.store(r)),
          finalize(() => (this.refreshing$ = null)),
          shareReplay({ bufferSize: 1, refCount: false }),
        );
    }
    return this.refreshing$;
  }

  logout(redirect = true): void {
    const s = this._session();
    this.clear();
    if (s) {
      // Best effort: ask the server to revoke the refresh token.
      this.http
        .post(
          `${this.base}/auth/logout`,
          { refreshToken: s.refreshToken },
          { headers: { Authorization: `Bearer ${s.accessToken}` } },
        )
        .pipe(catchError(() => of(null)))
        .subscribe();
    }
    if (redirect) void this.router.navigate(['/login']);
  }

  /** Clears local state without calling the server. */
  clear(): void {
    this._session.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* storage unavailable */
    }
  }

  private store(r: AuthResult): void {
    this._session.set(r);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(r));
    } catch {
      /* storage unavailable: the session lives in memory only */
    }
  }

  private load(): Session | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as Session) : null;
    } catch {
      return null;
    }
  }
}
