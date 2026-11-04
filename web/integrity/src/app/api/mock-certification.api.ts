import { Injectable } from '@angular/core';
import { Observable, delay, of } from 'rxjs';
import {
  CertificateKind,
  CertificationApi,
  ChecklistItemDef,
  ClientOption,
  CreateCertificationRequest,
  CreateCertificationResult,
  MachineOption,
} from './certification.api';

const CLIENTS: ClientOption[] = [
  { id: 'c-alnoor', name: 'Al Noor Industries' },
  { id: 'c-delta', name: 'Delta Steel' },
  { id: 'c-nile', name: 'Nile Logistics' },
];

const MACHINES: Record<string, MachineOption[]> = {
  'c-alnoor': [
    { id: 'm1', name: 'Air compressor', serialNumber: 'AC-2291', manufacturer: 'Atlas Copco', kind: 'standard', lastExaminedOn: '2025-05-14', overdue: false },
    { id: 'm2', name: 'Overhead crane 10t', serialNumber: 'OC-118', manufacturer: 'Demag', kind: 'standard', lastExaminedOn: '2025-10-03', overdue: true },
    { id: 'm3', name: 'Chain sling', serialNumber: null, manufacturer: 'Crosby', kind: 'gear', lastExaminedOn: null, overdue: false },
  ],
  'c-delta': [{ id: 'm4', name: 'Forklift FD30', serialNumber: 'FK-902', manufacturer: 'Toyota', kind: 'standard', lastExaminedOn: '2026-01-20', overdue: false }],
  'c-nile': [],
};

/** In-memory stand-in until the backend endpoints for direct certificates exist. Nothing is sent anywhere. */
@Injectable()
export class MockCertificationApi implements CertificationApi {
  listClients(): Observable<ClientOption[]> {
    return of(CLIENTS).pipe(delay(150));
  }

  listMachines(clientId: string): Observable<MachineOption[]> {
    return of(MACHINES[clientId] ?? []).pipe(delay(150));
  }

  checklistFor(_kind: CertificateKind): ChecklistItemDef[] {
    return [
      { key: 'guards', label: 'Guards and safety devices fitted' },
      { key: 'condition', label: 'No visible cracks, leaks or corrosion' },
      { key: 'valve', label: 'Safety valve tested' },
    ];
  }

  create(request: CreateCertificationRequest): Observable<CreateCertificationResult> {
    const count = request.gear ? request.gear.certificates.length : 1;
    return of({
      requestId: 'mock-' + Date.now().toString(36),
      certificateCount: count,
      status: request.submit ? ('submitted' as const) : ('draft' as const),
    }).pipe(delay(400));
  }
}
