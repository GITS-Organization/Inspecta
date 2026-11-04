import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { RequestDetails } from '../../api/coordinator.models';
import { AssignDialog } from '../../shared/assign-dialog';
import { hasNoPrice } from '../../shared/kind-badge';
import { Requests } from './requests';

const api = environment.apiBase;
const req = (over: Partial<RequestDetails> = {}): RequestDetails =>
  ({
    id: 'r1', kind: 'standard', clientId: 'c1', clientName: 'Al Noor Steel', location: 'Cairo', subject: 'Annual inspection', receivedAt: '2026-11-04T08:00:00Z', preferredDate: null,
    status: 'request_received', coordinatorId: null, inspectorId: null, scheduledAt: null, source: 'email', emailFrom: 'a@b.c', emailPreview: null, items: [], history: [], managerId: null,
    emailFromName: null, accessNotes: null, missingFields: [], canEditDetails: true, updatedAt: '', triage: 'request', maybeExpiresAt: null, quotation: null, client: null,
    hasUnreadClientReply: false, canAssignWithoutQuotation: false, ...over,
  }) as RequestDetails;
const inspectors = [
  { id: 'i1', fullName: 'Omar Hassan', email: 'o@x', qualifications: [], isOnLeave: false, leaveUntil: null, activeJobs: 2 },
  { id: 'i2', fullName: 'Hana Fathy', email: 'h@x', qualifications: [], isOnLeave: true, leaveUntil: '2026-12-01', activeJobs: 0 },
];

describe('contract clients: assign without a quotation', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => {
    ctrl.verify();
    localStorage.clear();
  });

  it('offers the action in the list only on requests the API says can skip the quotation', async () => {
    const f = TestBed.createComponent(Requests);
    f.detectChanges();
    f.detectChanges();
    await f.whenStable();
    ctrl.expectOne(`${api}/requests/summary`).flush({ requests: 2, maybe: 0, byStatus: [] });
    ctrl.expectOne((r) => r.url === `${api}/requests`).flush([
      req({ id: 'r1', clientName: 'Contract Co', canAssignWithoutQuotation: true }),
      req({ id: 'r2', clientName: 'Plain Co' }),
    ]);
    f.detectChanges();
    const rows = Array.from((f.nativeElement as HTMLElement).querySelectorAll('li.row'));
    const label = (r: Element) => Array.from(r.querySelectorAll('button')).map((b) => b.textContent?.trim());
    expect(label(rows[0])).toContain('Assign an inspector (contract client)');
    expect(label(rows[1])).not.toContain('Assign an inspector (contract client)');
  });

  it('tags a contract job and never treats its price as money', () => {
    expect(hasNoPrice('contract')).toBe(true);
    expect(hasNoPrice('standard')).toBe(false);
    const f = TestBed.createComponent(Requests);
    f.detectChanges();
    f.detectChanges();
    ctrl.expectOne(`${api}/requests/summary`).flush({ requests: 1, maybe: 0, byStatus: [] });
    ctrl.expectOne((r) => r.url === `${api}/requests`).flush([req({ kind: 'contract', status: 'assigned', clientName: 'Contract Co' })]);
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('app-kind-badge')?.textContent?.trim()).toBe('Contract');
  });

  function dialog() {
    const f = TestBed.createComponent(AssignDialog);
    f.componentRef.setInput('requestId', 'r1');
    f.componentRef.setInput('clientName', 'Al Noor Steel');
    f.detectChanges();
    ctrl.expectOne(`${api}/inspectors`).flush(inspectors);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const select = el.querySelector<HTMLSelectElement>('#ad-i')!;
    const choose = (name: string) => {
      select.value = [...select.options].find((o) => o.text.includes(name))!.value;
      select.dispatchEvent(new Event('change'));
      f.detectChanges();
    };
    const btn = (t: string) => [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === t) as HTMLButtonElement;
    return { f, el, choose, btn, text: () => el.textContent ?? '' };
  }

  it('says no quotation will be sent, flags an inspector on leave, and asks for an inspector first', () => {
    const d = dialog();
    expect(d.text()).toContain('No quotation will be sent.');
    expect(d.text()).toContain('Hana Fathy · 0 active · on leave until 1 Dec 2026');
    d.btn('Assign without a quotation').click();
    d.f.detectChanges();
    expect(d.text()).toContain('Choose an inspector.');
    d.choose('Hana');
    expect(d.text()).toContain('is on leave until 1 Dec 2026');
  });

  it('posts the assignment, then reports the updated request', () => {
    const d = dialog();
    const done: RequestDetails[] = [];
    d.f.componentInstance.done.subscribe((r) => done.push(r));
    d.choose('Omar');
    const when = d.el.querySelector<HTMLInputElement>('#ad-w')!;
    when.value = '2026-11-10T09:30';
    when.dispatchEvent(new Event('input'));
    const note = d.el.querySelector<HTMLInputElement>('#ad-n')!;
    note.value = ' Gate 3 ';
    note.dispatchEvent(new Event('input'));
    d.btn('Assign without a quotation').click();
    const post = ctrl.expectOne(`${api}/requests/r1/assign-without-quotation`);
    expect(post.request.body).toEqual({ inspectorId: 'i1', scheduledAt: new Date('2026-11-10T09:30').toISOString(), note: 'Gate 3' });
    post.flush(req({ kind: 'contract', status: 'assigned', inspectorId: 'i1' }));
    expect(done[0].kind).toBe('contract');
  });

  it('shows the API sentence for no_contract and not_assignable', () => {
    const d = dialog();
    d.choose('Omar');
    d.btn('Assign without a quotation').click();
    ctrl.expectOne(`${api}/requests/r1/assign-without-quotation`).flush({ code: 'no_contract', detail: 'This client has no contract, so a quotation is needed.' }, { status: 409, statusText: 'Conflict' });
    d.f.detectChanges();
    expect(d.text()).toContain('This client has no contract, so a quotation is needed.');
    d.btn('Assign without a quotation').click();
    ctrl.expectOne(`${api}/requests/r1/assign-without-quotation`).flush({ code: 'not_assignable', detail: 'The request is not at the right stage.' }, { status: 409, statusText: 'Conflict' });
    d.f.detectChanges();
    expect(d.text()).toContain('The request is not at the right stage.');
    expect(TestBed.inject(Router)).toBeTruthy();
  });
});
