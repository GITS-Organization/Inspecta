import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AuthUser, CleanupEmailBacklogRequest, CleanupEmailBacklogResponse, CompanyProfile, CompanyRow,
  CreateTechnicalManagerRequest, SaveCompanyProfileRequest, UserRole,
} from './api.models';

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;

  listUsers(filter: { role?: UserRole; managerId?: string; isActive?: boolean } = {}): Observable<AuthUser[]> {
    let params = new HttpParams();
    if (filter.role) params = params.set('role', filter.role);
    if (filter.managerId) params = params.set('managerId', filter.managerId);
    if (filter.isActive !== undefined) params = params.set('isActive', filter.isActive);
    return this.http.get<AuthUser[]>(`${this.api}/users`, { params });
  }

  setUserActive(userId: string, isActive: boolean): Observable<void> {
    return this.http.put<void>(`${this.api}/users/${userId}/status`, { isActive });
  }

  /** `null` when the team has no profile yet (404): it then uses the built-in identity. */
  getCompanyProfile(managerId: string): Observable<CompanyProfile | null> {
    return this.http.get<CompanyProfile>(`${this.api}/admin/managers/${managerId}/company-profile`).pipe(
      catchError((error: unknown) =>
        error instanceof HttpErrorResponse && error.status === 404 ? of(null) : throwError(() => error),
      ),
    );
  }

  /** There is no "list companies" endpoint: technical managers + everyone else (for team sizes) + one profile each. */
  listCompanies(): Observable<CompanyRow[]> {
    return this.listUsers().pipe(
      switchMap((users) => {
        const managers = users.filter((user) => user.role === 'technical_manager');
        if (managers.length === 0) return of([] as CompanyRow[]);
        return forkJoin(
          managers.map((manager) =>
            this.getCompanyProfile(manager.id).pipe(
              catchError(() => of(null)), // one broken profile must not hide the other companies
              map((profile): CompanyRow => {
                const team = users.filter((user) => user.managerId === manager.id);
                return {
                  manager,
                  profile,
                  inspectors: team.filter((user) => user.role === 'inspector').length,
                  coordinators: team.filter((user) => user.role === 'coordinator').length,
                };
              }),
            ),
          ),
        );
      }),
    );
  }

  createTechnicalManager(request: CreateTechnicalManagerRequest): Observable<AuthUser> {
    return this.http.post<AuthUser>(`${this.api}/technical-managers`, request);
  }

  saveCompanyProfile(managerId: string, request: SaveCompanyProfileRequest): Observable<CompanyProfile> {
    return this.http.put<CompanyProfile>(`${this.api}/admin/managers/${managerId}/company-profile`, request);
  }

  cleanupEmailBacklog(request: CleanupEmailBacklogRequest): Observable<CleanupEmailBacklogResponse> {
    return this.http.post<CleanupEmailBacklogResponse>(`${this.api}/admin/email-backlog/cleanup`, request);
  }
}
