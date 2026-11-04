import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AddJobMachine,
  CertificateShare,
  DashboardOverview,
  JobMachineDetails,
  MachineForm,
  MachineList,
  QueueFilter,
  QueueRow,
  ReturnJobRequest,
  SaveMachineFormRequest,
} from '../core/models';
import { Equipment } from './coordinator.models';
import { MachineType } from './certification.api';
import { SerialLimits } from '../shared/serials';

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

  // ---- changing the machines of a job that is under way
  availableMachines(requestId: string): Observable<Equipment[]> {
    return this.http.get<Equipment[]>(`${this.base}/quotation-queue/${requestId}/available-machines`);
  }

  /** A registered machine (`equipmentId`) or a new one by its details. Answers 201 with the new list. */
  addMachine(requestId: string, body: AddJobMachine): Observable<MachineList> {
    return this.http.post<MachineList>(`${this.base}/quotation-queue/${requestId}/machines`, body);
  }

  editMachine(requestId: string, itemId: string, body: JobMachineDetails): Observable<MachineList> {
    return this.http.put<MachineList>(`${this.base}/quotation-queue/${requestId}/machines/${itemId}/details`, body);
  }

  removeMachine(requestId: string, itemId: string): Observable<MachineList> {
    return this.http.delete<MachineList>(`${this.base}/quotation-queue/${requestId}/machines/${itemId}`);
  }

  /** Divides a gear into certificates (replaces the group until one of them is started). */
  divideMachine(requestId: string, itemId: string, certificates: CertificateShare[]): Observable<MachineList> {
    return this.http.put<MachineList>(`${this.base}/quotation-queue/${requestId}/machines/${itemId}/certificates`, { certificates });
  }

  serialLimits(): Observable<SerialLimits> {
    return this.http.get<SerialLimits>(`${this.base}/certifications/serial-limits`);
  }

  machineTypes(): Observable<MachineType[]> {
    return this.http.get<MachineType[]>(`${this.base}/certifications/machine-types`);
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
