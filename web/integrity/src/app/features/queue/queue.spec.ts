import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { QueueRow } from '../../core/models';
import { Deliver } from './deliver';
import { Queue, toLocalInput } from './queue';

const api = environment.apiBase;
const row = (over: Partial<QueueRow> = {}): QueueRow => ({
  id: 'q1', requestId: 'r1', requestStatus: 'unassigned', quotationNumber: 'Q-1', proposalNumber: 'P1', clientName: 'Al Noor', location: 'Cairo', totalUnits: 2, updatedAt: '2026-11-01T00:00:00Z', ...over,
});

describe('Queue', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  it('gives a job to an inspector with the chosen date, then reloads the list', async () => {
    const f = TestBed.createComponent(Queue);
    f.detectChanges();
    ctrl.expectOne(`${api}/quotation-queue?status=unassigned&pageSize=100`).flush([row()]);
    const c = f.componentInstance as unknown as {
      open(r: QueueRow, p: 'assign'): void; inspectorId: { set(v: string): void }; when: { set(v: string): void }; note: { set(v: string): void }; submit(r: QueueRow): void;
    };
    c.open(row(), 'assign');
    ctrl.expectOne(`${api}/inspectors`).flush([{ id: 'i1', fullName: 'Omar', email: 'o@x', qualifications: [], isOnLeave: false, leaveUntil: null, activeJobs: 2 }]);

    c.submit(row()); // nothing chosen yet
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).textContent).toContain('Choose the date and time of the inspection.');
    ctrl.expectNone(`${api}/requests/r1/assignment`);

    c.when.set('2026-11-10T09:30');
    c.submit(row());
    expect((f.nativeElement as HTMLElement).textContent).toBeDefined();
    c.inspectorId.set('i1');
    c.note.set('Bring the gauge');
    c.submit(row());
    const post = ctrl.expectOne(`${api}/requests/r1/assignment`);
    expect(post.request.body).toEqual({ inspectorId: 'i1', scheduledAt: new Date('2026-11-10T09:30').toISOString(), note: 'Bring the gauge' });
    post.flush({});
    ctrl.expectOne(`${api}/quotation-queue?status=unassigned&pageSize=100`).flush([]);
  });

  it('tags man days, never shows money, and links the machines of a job that is under way', () => {
    const f = TestBed.createComponent(Queue);
    f.detectChanges();
    ctrl.expectOne(`${api}/quotation-queue?status=unassigned&pageSize=100`).flush([
      row({ kind: 'man_day', requestStatus: 'assigned', quotationNumber: 'MD-0001', totalUnits: 0, requestId: 'r7' }),
      row({ id: 'q2', requestId: 'r8', requestStatus: 'certificate_approved' }),
    ]);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    expect(el.querySelectorAll('app-kind-badge').length).toBe(2);
    expect(el.querySelector('app-kind-badge')?.textContent?.trim()).toBe('Man day');
    expect(el.textContent).toContain('MD-0001');
    expect(el.textContent).toContain('no machines yet');
    expect(el.textContent).not.toContain('EGP');
    const hrefs = Array.from(el.querySelectorAll('a')).map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('/jobs/r7');
    expect(hrefs).not.toContain('/jobs/r8');
    expect(hrefs).toContain('/requests/man-day');
  });

  it('round-trips a timestamp through the datetime-local value', () => {
    expect(new Date(toLocalInput('2026-11-10T07:30:00Z')).toISOString()).toBe('2026-11-10T07:30:00.000Z');
  });
});

describe('Deliver', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  it('sends the edited email, and shows the sentence when the mail server refuses', async () => {
    const f = TestBed.createComponent(Deliver);
    f.componentRef.setInput('requestId', 'r1');
    f.detectChanges();
    ctrl.expectOne(`${api}/quotation-queue/r1/machines`).flush({ requestId: 'r1', requestStatus: 'certificate_approved', clientName: 'Al Noor', location: 'Cairo', machines: [] });
    ctrl.expectOne(`${api}/quotation-queue/r1/deliver-to-client/preview`).flush({
      emailEnabled: true, to: 'certs@alnoor.com', cc: [], subject: 'Inspection Certificates - Al Noor', body: 'Hello', attachments: [], repliesToOriginalEmail: true, canSend: true, alreadyDelivered: false, certificates: [],
    });
    await f.whenStable();
    f.detectChanges();

    (f.componentInstance as unknown as { send(e: object): void }).send({ sentTo: 'certs@alnoor.com', cc: [], subject: 'S', message: 'M' });
    const post = ctrl.expectOne(`${api}/quotation-queue/r1/deliver-to-client`);
    expect(post.request.body).toEqual({ sentTo: 'certs@alnoor.com', cc: [], subject: 'S', message: 'M' });
    post.flush({ title: 'Email could not be sent', code: 'email_delivery_failed' }, { status: 502, statusText: 'Bad Gateway' });
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('[role=alert]')?.textContent).toContain('Nothing was changed, so you can try again.');
    expect(TestBed.inject(Router).url).toBe('/');
  });
});
