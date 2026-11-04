import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResult, AuthUser } from './api.models';

const STORAGE_KEY = 'gits.session';

/** Thrown by `login` when the account is real but not an admin. */
export class NotAdminError extends Error {
  constructor() {
    super('This area is only for GITS admins. Companies sign in at their own address.');
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly base = `${environment.apiUrl}/auth`;

  private readonly session = signal<AuthResult | null>(readStored());
  private refreshing$: Observable<AuthResult> | null = null;

  readonly user = computed<AuthUser | null>(() => this.session()?.user ?? null);
  readonly isAdmin = computed(() => this.user()?.role === 'admin');
  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  /** Signs in; only an admin keeps the session. Any other role is refused and its tokens are revoked. */
  login(email: string, password: string): Observable<AuthUser> {
    return this.http.post<AuthResult>(`${this.base}/login`, { email, password }).pipe(
      tap((result) => {
        if (result.user.role === 'admin') this.store(result);
      }),
      map((result) => {
        if (result.user.role !== 'admin') {
          this.revoke(result);
          throw new NotAdminError();
        }
        return result.user;
      }),
    );
  }

  /** One refresh at a time: parallel 401s share the same request. */
  refresh(): Observable<AuthResult> {
    const current = this.session();
    if (!current) return throwError(() => new Error('No session'));
    this.refreshing$ ??= this.http
      .post<AuthResult>(`${this.base}/refresh`, { refreshToken: current.refreshToken })
      .pipe(
        tap((result) => this.store(result)),
        finalize(() => (this.refreshing$ = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.refreshing$;
  }

  logout(): void {
    const current = this.session();
    if (current) this.revoke(current);
    this.clear();
    void this.router.navigate(['/login']);
  }

  /** Drops the local session without asking the server (used when a refresh has already failed). */
  expire(): void {
    this.clear();
    void this.router.navigate(['/login']);
  }

  private revoke(result: AuthResult): void {
    this.http
      .post(`${this.base}/logout`, { refreshToken: result.refreshToken }, {
        headers: { Authorization: `Bearer ${result.accessToken}` },
      })
      .pipe(catchError(() => of(null)))
      .subscribe();
  }

  private store(result: AuthResult): void {
    this.session.set(result);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(result));
  }

  private clear(): void {
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }
}

function readStored(): AuthResult | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthResult) : null;
  } catch {
    return null;
  }
}
