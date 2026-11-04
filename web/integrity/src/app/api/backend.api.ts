import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  DashboardOverview,
  MachineForm,
  MachineList,
  QueueFilter,
  QueueRow,
  ReturnJobRequest,
  SaveMachineFormRequest,
} from '../core/models';

/** Typed wrappers of the endpoints the screens use. Paths are relative to `/api/v1`. */
@Injectable({ providedIn: 'root' })
export class BackendApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  // ---- dashboard (technical manager, admin)
  overview(): Observable<DashboardOverview> {
    return this.http.get<DashboardOverview>(`${this.base}/dashboard/overview`);
  }

  // ---- quotation queue
  queue(status?: QueueFilter): Observable<QueueRow[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    return this.http.get<QueueRow[]>(`${this.base}/quotation-queue`, { params });
  }

  toReview(): Observable<QueueRow[]> {
    return this.http.get<QueueRow[]>(`${this.base}/quotation-queue/to-review`);
  }

  // ---- machines of a job
  machines(requestId: string): Observable<MachineList> {
    return this.http.get<MachineList>(`${this.base}/quotation-queue/${requestId}/machines`);
  }

  startInspection(requestId: string): Observable<MachineList> {
    return this.http.post<MachineList>(`${this.base}/quotation-queue/${requestId}/start-inspection`, {});
  }

  machine(requestId: string, itemId: string): Observable<MachineForm> {
    return this.http.get<MachineForm>(`${this.base}/quotation-queue/${requestId}/machines/${itemId}`);
  }

  saveMachine(requestId: string, itemId: string, body: SaveMachineFormRequest): Observable<MachineForm> {
    return this.http.put<MachineForm>(`${this.base}/quotation-queue/${requestId}/machines/${itemId}`, body);
  }

  submitMachine(requestId: string, itemId: string): Observable<MachineForm> {
    return this.http.post<MachineForm>(`${this.base}/quotation-queue/${requestId}/machines/${itemId}/submit`, {});
  }

  approveAll(requestId: string): Observable<MachineList> {
    return this.http.post<MachineList>(`${this.base}/quotation-queue/${requestId}/approve-all`, {});
  }

  returnToInspector(requestId: string, body: ReturnJobRequest): Observable<MachineList> {
    return this.http.post<MachineList>(`${this.base}/quotation-queue/${requestId}/return-to-inspector`, body);
  }

  // ---- files (need the bearer token, so they go through HttpClient and open from an object URL)
  machinePdf(requestId: string, itemId: string): Observable<Blob> {
    return this.http.get(`${this.base}/quotation-queue/${requestId}/machines/${itemId}/pdf`, { responseType: 'blob' });
  }

  previewZip(requestId: string): Observable<Blob> {
    return this.http.get(`${this.base}/quotation-queue/${requestId}/certificates/preview.zip`, { responseType: 'blob' });
  }
}

/** Opens a PDF blob in a new tab. */
export function openBlob(blob: Blob, type = 'application/pdf'): void {
  const url = URL.createObjectURL(new Blob([blob], { type }));
  window.open(url, '_blank', 'noopener');
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/** Saves a blob as a file. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
