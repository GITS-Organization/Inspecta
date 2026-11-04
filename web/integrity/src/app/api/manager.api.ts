import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthUser } from '../core/models';

/** The mailbox of a coordinator as the API tells it. The password is never part of it. */
export interface MailboxConfig {
  configured: boolean;
  imapServer: string | null;
  imapPort: number | null;
  emailUsername: string | null;
  useSsl: boolean | null;
  smtpServer: string | null;
  smtpPort: number | null;
  smtpUseSsl: boolean | null;
  /** The date mail is imported from (ISO timestamp). */
  syncFrom: string;
  updatedAt: string | null;
}

const asParams = (managerId?: string): HttpParams => (managerId ? new HttpParams().set('managerId', managerId) : new HttpParams());

/** Technical manager and admin: the team and its coordinators' mailboxes. Admin adds `managerId`. */
@Injectable({ providedIn: 'root' })
export class ManagerApi {
  private readonly http = inject(HttpClient);
  private readonly b = environment.apiBase;

  team(managerId?: string): Observable<AuthUser[]> {
    return this.http.get<AuthUser[]>(`${this.b}/manager/team`, { params: asParams(managerId) });
  }
  mailbox(coordinatorId: string, managerId?: string): Observable<MailboxConfig> {
    return this.http.get<MailboxConfig>(`${this.b}/manager/team/${coordinatorId}/email-config`, { params: asParams(managerId) });
  }
  /** `date` is `yyyy-MM-dd`; it is sent as midnight UTC. */
  changeSyncFrom(coordinatorId: string, date: string, managerId?: string): Observable<MailboxConfig> {
    return this.http.put<MailboxConfig>(`${this.b}/manager/team/${coordinatorId}/email-config/sync-from`, { syncFrom: `${date}T00:00:00Z` }, { params: asParams(managerId) });
  }
}
