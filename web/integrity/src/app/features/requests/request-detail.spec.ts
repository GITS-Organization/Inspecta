import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { RequestDetails } from '../../api/coordinator.models';
import { RequestDetail } from './request-detail';

const api = environment.apiBase;
const base: RequestDetails = {
  id: 'r1', clientId: 'c1', clientName: 'Al Noor', location: 'Cairo', subject: 'Cranes', receivedAt: '2026-11-01T08:00:00Z', preferredDate: null,
  status: 'request_received', coordinatorId: null, inspectorId: null, scheduledAt: null, source: 'email', emailFrom: 'buyer@alnoor.com', emailPreview: null,
  items: [{ id: 'i1', equipmentId: 'e1', name: 'Crane', type: null, capacity: null, quantity: 2, serialNumber: 'C-1', issueDescription: null, location: null }],
  history: [], managerId: null, emailFromName: null, accessNotes: null, missingFields: [], canEditDetails: true, updatedAt: '2026-11-01T09:00:00Z',
  triage: 'request', maybeExpiresAt: null, quotation: null, client: null, hasUnreadClientReply: false,
};

describe('RequestDetail', () => {
  let ctrl: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  const open = async () => {
    const f = TestBed.createComponent(RequestDetail);
    f.componentRef.setInput('requestId', 'r1');
    f.detectChanges();
    ctrl.expectOne(`${api}/clients`).flush([]);
    ctrl.expectOne(`${api}/requests/r1`).flush(base);
    ctrl.expectOne(`${api}/equipment?clientId=c1`).flush([]);
    ctrl.expectOne(`${api}/requests/r1/suggestions`).flush([]);
    ctrl.expectOne(`${api}/requests/r1/email`).flush({ requestId: 'r1', textBody: 'Please inspect', attachments: [], to: [], cc: [] });
    await f.whenStable();
    f.detectChanges();
    return f;
  };

  it('shows the original email as text and saves with the updatedAt it read', async () => {
    const f = await open();
    expect((f.nativeElement as HTMLElement).textContent).toContain('Please inspect');

    (f.componentInstance as unknown as { save(p: boolean): void }).save(false);
    const put = ctrl.expectOne((r) => r.method === 'PUT' && r.url === `${api}/requests/r1`);
    expect(put.request.body.expectedUpdatedAt).toBe('2026-11-01T09:00:00Z');
    expect(put.request.body.prepareQuotation).toBe(false);
    expect(put.request.body.items).toEqual([expect.objectContaining({ id: 'i1', name: 'Crane', quantity: 2, equipmentId: 'e1' })]);
    put.flush(base);
    ctrl.expectOne(`${api}/equipment?clientId=c1`).flush([]);
    ctrl.expectOne(`${api}/requests/r1/suggestions`).flush([]);
  });

  it('shows the API sentence when the save is refused', async () => {
    const f = await open();
    (f.componentInstance as unknown as { save(p: boolean): void }).save(true);
    ctrl.expectOne((r) => r.method === 'PUT').flush({ title: 'Conflict', code: 'request_incomplete' }, { status: 409, statusText: 'Conflict' });
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('[role=alert]')?.textContent).toContain('Fill in the client, the location and at least one item first.');
  });
});
