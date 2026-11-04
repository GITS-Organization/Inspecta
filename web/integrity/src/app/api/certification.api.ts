import { InjectionToken } from '@angular/core';
import { Observable } from 'rxjs';
import { ChecklistValue, InspectionResult } from '../core/models';

/**
 * The Create certification tab talks to this interface only. Today it is bound to `MockCertificationApi`; when the backend
 * contract for direct certificates and gears is final, bind `HttpCertificationApi` in `CERTIFICATION_API` (see app.config.ts).
 */
export type CertificateKind = 'standard' | 'gear';

export interface ClientOption {
  id: string;
  name: string;
}

export interface MachineOption {
  id: string;
  name: string;
  serialNumber: string | null;
  manufacturer: string | null;
  kind: CertificateKind;
  /** `yyyy-MM-dd` of the last examination, when there was one. */
  lastExaminedOn: string | null;
  overdue: boolean;
}

export interface NewMachine {
  name: string;
  manufacturer: string;
  serialNumber: string;
  kind: CertificateKind;
}

export interface ChecklistItemDef {
  key: string;
  label: string;
}

export interface GearDetails {
  quantity: number;
  /** Every serial number of the batch. */
  serials: string[];
  /** The serial numbers split over the certificates; one entry per certificate. */
  certificates: string[][];
}

export interface CreateCertificationRequest {
  clientId: string;
  /** An existing machine, or null when `newMachine` is given. */
  machineId: string | null;
  newMachine: NewMachine | null;
  examinedOn: string;
  nextExaminationOn: string;
  result: InspectionResult;
  checklist: Record<string, ChecklistValue>;
  gear: GearDetails | null;
  /** false saves a draft only; true sends the certificate(s) to the technical manager. */
  submit: boolean;
}

export interface CreateCertificationResult {
  requestId: string;
  certificateCount: number;
  status: 'draft' | 'submitted';
}

export interface CertificationApi {
  listClients(): Observable<ClientOption[]>;
  listMachines(clientId: string): Observable<MachineOption[]>;
  /** The questions the standard checklist asks. */
  checklistFor(kind: CertificateKind): ChecklistItemDef[];
  create(request: CreateCertificationRequest): Observable<CreateCertificationResult>;
}

export const CERTIFICATION_API = new InjectionToken<CertificationApi>('CERTIFICATION_API');
