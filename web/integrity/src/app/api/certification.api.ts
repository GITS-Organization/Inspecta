import { HttpClient } from '@angular/common/http';
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { MachineList } from '../core/models';
import { SerialLimits } from '../shared/serials';

export interface ClientOption {
  id: string;
  name: string;
}

export interface MachineOption {
  id: string;
  name: string;
  serialNumber: string | null;
  manufacturer: string | null;
  /** `yyyy-MM-dd` of the last examination, when there was one. */
  lastExaminedOn: string | null;
  overdue: boolean;
}

/** One kind of machine of the catalogue (`GET /certifications/machine-types`). */
export interface MachineType {
  name: string;
  aliases: string[];
  standards: string[];
  templateKey: string;
  templateName: string;
  isGear: boolean;
}

export interface CreateMachine {
  name: string;
  quantity?: number;
  equipmentId?: string;
  type?: string;
  capacity?: string;
  serialNumber?: string;
  location?: string;
  issueDescription?: string;
}

export interface CreateCertificationRequest {
  clientId: string;
  location?: string;
  /** Admin only. */
  managerId?: string;
  machines: CreateMachine[];
}

/**
 * Everything the Create certification tab needs from the backend. `HttpCertificationApi` is the real one; tests (or a
 * demo build) can bind another implementation to `CERTIFICATION_API`.
 */
export interface CertificationApi {
  listClients(): Observable<ClientOption[]>;
  listMachines(clientId: string): Observable<MachineOption[]>;
  machineTypes(): Observable<MachineType[]>;
  serialLimits(): Observable<SerialLimits>;
  /** `POST /certifications`: the job starts in progress with the caller as inspector. */
  create(request: CreateCertificationRequest): Observable<MachineList>;
  /** `POST /quotation-queue/{requestId}/machines/{requestItemId}/split`: shares the serials over the certificates. */
  split(requestId: string, requestItemId: string, serialNumbers: string[], certificates?: number): Observable<MachineList>;
}

export const CERTIFICATION_API = new InjectionToken<CertificationApi>('CERTIFICATION_API');

interface EquipmentDto {
  id: string;
  name: string;
  serialNumber: string | null;
  manufacturer: string | null;
  lastInspectedOn: string | null;
  nextInspectionDueAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class HttpCertificationApi implements CertificationApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  listClients(): Observable<ClientOption[]> {
    return this.http.get<ClientOption[]>(`${this.base}/clients`).pipe(map((cs) => cs.map((c) => ({ id: c.id, name: c.name }))));
  }

  listMachines(clientId: string): Observable<MachineOption[]> {
    return this.http.get<EquipmentDto[]>(`${this.base}/equipment`, { params: { clientId } }).pipe(
      map((items) =>
        items.map((e) => ({
          id: e.id,
          name: e.name,
          serialNumber: e.serialNumber,
          manufacturer: e.manufacturer,
          lastExaminedOn: e.lastInspectedOn,
          overdue: !!e.nextInspectionDueAt && new Date(e.nextInspectionDueAt).getTime() < Date.now(),
        })),
      ),
    );
  }

  machineTypes(): Observable<MachineType[]> {
    return this.http.get<MachineType[]>(`${this.base}/certifications/machine-types`);
  }

  serialLimits(): Observable<SerialLimits> {
    return this.http.get<SerialLimits>(`${this.base}/certifications/serial-limits`);
  }

  create(request: CreateCertificationRequest): Observable<MachineList> {
    return this.http.post<MachineList>(`${this.base}/certifications`, request);
  }

  split(requestId: string, requestItemId: string, serialNumbers: string[], certificates?: number): Observable<MachineList> {
    return this.http.post<MachineList>(`${this.base}/quotation-queue/${requestId}/machines/${requestItemId}/split`, {
      serialNumbers,
      ...(certificates ? { certificates } : {}),
    });
  }
}

