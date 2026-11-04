import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { Quote, RequestDetails } from '../../api/coordinator.models';
import { RequestDetail } from '../requests/request-detail';
import { RequestNew } from '../requests/request-new';
import { QuoteEditor } from './quote-editor';

const api = environment.apiBase;
const manDayLine = (price: number | null) => ({
  requestItemId: null, description: 'Man day (inspector)', type: 'Man day', prNumber: null, quantity: 3, unitPricePiastres: price, pricingUnit: 'Per Day', totalPiastres: (price ?? 0) * 3,
});
const quote = (price: number | null, over: Partial<Quote> = {}): Quote => ({
  id: 'q1', requestId: 'r1', version: 1, amountPiastres: 0, validUntil: '2026-12-01T00:00:00Z', status: 'open', clientReply: null, createdAt: '2026-11-01T00:00:00Z',
  proposalNumber: 'QT-1', clientName: 'Al Noor', validForDays: 14, message: null,
  items: [
    { requestItemId: 'a', description: 'Crane', type: 'Lifting', prNumber: null, quantity: 2, unitPricePiastres: 100000, pricingUnit: 'visit', totalPiastres: 200000 },
    manDayLine(price),
  ],
  scopeOfWork: [], notes: [], missingPrices: [], totalUnits: 2, canEdit: true, isExpired: false, sentAt: null, updatedAt: '2026-11-01T00:00:00Z',
  supersededAt: null, emails: [], contactName: null, quotationNumber: null, clientReplies: [], ...over,
});

describe('Quote editor: the man day line', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  function open(q: Quote) {
    const f = TestBed.createComponent(QuoteEditor);
    f.componentRef.setInput('requestId', 'r1');
    f.componentRef.setInput('quoteId', 'q1');
    f.detectChanges();
    ctrl.expectOne(`${api}/requests/r1/quotes/q1`).flush(q);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const type = (sel: string, v: string) => {
      const i = el.querySelector<HTMLInputElement>(sel)!;
      i.value = v;
      i.dispatchEvent(new Event('input'));
      f.detectChanges();
    };
    const btn = (t: string) => [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === t) as HTMLButtonElement | undefined;
    return { f, el, type, btn, text: () => el.textContent ?? '' };
  }

  it('shows the man day after the machines with its days and a per-day price input, and counts it in the total', () => {
    const m = open(quote(25000));
    const rows = [...m.el.querySelectorAll('tbody tr')];
    expect(rows.length).toBe(2);
    expect(rows[1].textContent).toContain('Man day (inspector)');
    expect(rows[1].textContent).toContain('3 days of one inspector, priced per day');
    expect(rows[1].querySelector('a.lnk')?.getAttribute('href')).toBe('/requests/r1');
    expect(m.el.querySelector<HTMLInputElement>('#mdp')!.value).toBe('250');
    expect(rows[1].querySelector('td:last-child')?.textContent).toContain('EGP 750.00');
    expect(m.el.querySelector('tfoot')?.textContent).toContain('EGP 2,750.00');
    m.type('#mdp', '300');
    expect(m.el.querySelector('tfoot')?.textContent).toContain('EGP 2,900.00');
  });

  it('blocks sending while the man day has no price and says so, then sends the price per day, never an item id', () => {
    const m = open(quote(null));
    expect(m.text()).toContain('1 line has no price yet (the man day among them).');
    expect(m.btn('Prepare the email')!.disabled).toBe(true);
    m.type('#mdp', '1,250.5');
    expect(m.btn('Prepare the email')!.disabled).toBe(false);
    m.btn('Save the draft')!.click();
    const put = ctrl.expectOne(`${api}/requests/r1/quotes/q1`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body.manDayPricePiastres).toBe(125050);
    expect(put.request.body.items).toEqual([{ requestItemId: 'a', unitPricePiastres: 100000 }]);
    put.flush(quote(125050));
  });

  it('leaves the man day out of the payload when the quotation has none', () => {
    const q = quote(null);
    const m = open({ ...q, items: [q.items![0]] });
    m.btn('Save the draft')!.click();
    const put = ctrl.expectOne(`${api}/requests/r1/quotes/q1`);
    expect('manDayPricePiastres' in put.request.body).toBe(false);
    put.flush({ ...q, items: [q.items![0]] });
  });

  it('shows a sent quotation\'s man day as a normal line, "Man day × 3 days", with no input', () => {
    const m = open(quote(25000, { canEdit: false, sentAt: '2026-11-02T00:00:00Z', amountPiastres: 275000 }));
    expect(m.el.querySelector('#mdp')).toBeNull();
    expect(m.text()).toContain('Man day × 3 days');
    expect(m.el.querySelector('tfoot')?.textContent).toContain('EGP 2,750.00');
  });
});

const request = (over: Partial<RequestDetails> = {}): RequestDetails =>
  ({
    id: 'r1', clientId: 'c1', clientName: 'Al Noor', location: 'Cairo', subject: 'S', receivedAt: '2026-11-04T08:00:00Z', preferredDate: null, status: 'request_received', coordinatorId: null,
    inspectorId: null, scheduledAt: null, source: 'manual', emailFrom: null, emailPreview: null, items: [{ id: 'i1', equipmentId: null, name: 'Crane', type: null, capacity: null, quantity: 1, serialNumber: null, issueDescription: null, location: null }],
    history: [], managerId: null, emailFromName: null, accessNotes: null, missingFields: [], canEditDetails: true, updatedAt: '2026-11-04T09:00:00Z', triage: 'request', maybeExpiresAt: null,
    quotation: null, client: null, hasUnreadClientReply: false, manDayDays: null, ...over,
  }) as RequestDetails;

describe('Request screens: the man day option', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  async function openDetail(r: RequestDetails) {
    const f = TestBed.createComponent(RequestDetail);
    f.componentRef.setInput('requestId', 'r1');
    f.detectChanges();
    ctrl.expectOne(`${api}/clients`).flush([]);
    ctrl.expectOne(`${api}/requests/r1`).flush(r);
    ctrl.expectOne(`${api}/equipment?clientId=c1`).flush([]);
    ctrl.expectOne(`${api}/requests/r1/suggestions`).flush([]);
    await f.whenStable();
    f.detectChanges();
    ctrl.match(`${api}/certifications/machine-types`).forEach((x) => x.flush([]));
    const el = f.nativeElement as HTMLElement;
    return { f, el, c: f.componentInstance as unknown as { save(p: boolean): void } };
  }

  it('is off by default, turns on with 1 day, and saves the days (0 when off)', async () => {
    const d = await openDetail(request());
    const on = d.el.querySelector<HTMLInputElement>('#rmd-on')!;
    expect(on.checked).toBe(false);
    expect(d.el.textContent).toContain('Adds a man day line to the quotation; you price it per day there.');
    on.checked = true;
    on.dispatchEvent(new Event('change'));
    d.f.detectChanges();
    const days = d.el.querySelector<HTMLInputElement>('#rmd-n')!;
    expect(days.value).toBe('1');
    expect([days.min, days.max]).toEqual(['1', '365']);
    days.value = '400';
    days.dispatchEvent(new Event('input'));
    d.f.detectChanges();
    expect(d.el.querySelector<HTMLInputElement>('#rmd-n')!.value).toBe('365');
    days.value = '3';
    days.dispatchEvent(new Event('input'));
    d.c.save(false);
    const put = ctrl.expectOne((r) => r.method === 'PUT' && r.url === `${api}/requests/r1`);
    expect(put.request.body.manDayDays).toBe(3);
    put.flush(request({ manDayDays: 3 }));
    ctrl.expectOne(`${api}/equipment?clientId=c1`).flush([]);
    ctrl.expectOne(`${api}/requests/r1/suggestions`).flush([]);

    d.f.detectChanges();
    const off = d.el.querySelector<HTMLInputElement>('#rmd-on')!;
    expect(off.checked).toBe(true);
    off.checked = false;
    off.dispatchEvent(new Event('change'));
    d.c.save(false);
    const put2 = ctrl.expectOne((r) => r.method === 'PUT' && r.url === `${api}/requests/r1`);
    expect(put2.request.body.manDayDays).toBe(0);
    put2.flush(request({ manDayDays: null }));
    ctrl.expectOne(`${api}/equipment?clientId=c1`).flush([]);
    ctrl.expectOne(`${api}/requests/r1/suggestions`).flush([]);
  });

  it('is read-only once the request can no longer be edited', async () => {
    const d = await openDetail(request({ canEditDetails: false, status: 'quote_sent', manDayDays: 2 }));
    expect(d.el.querySelector('#rmd-on')).toBeNull();
    expect(d.el.textContent).toContain('2 days, priced per day in the quotation');
  });

  it('the new-request form sends manDayDays only when it is on', () => {
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const f = TestBed.createComponent(RequestNew);
    f.detectChanges();
    ctrl.expectOne(`${api}/clients`).flush([]);
    ctrl.match(`${api}/certifications/machine-types`).forEach((x) => x.flush([]));
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    expect(el.querySelector('#nmd-on')).not.toBeNull();
    const c = f.componentInstance as unknown as { clientName: { set(v: string): void }; location: { set(v: string): void }; manDay: { set(v: number): void }; create(): void };
    c.clientName.set('Al Noor');
    c.location.set('Cairo');
    c.create();
    const first = ctrl.expectOne((r) => r.method === 'POST' && r.url === `${api}/requests`);
    expect('manDayDays' in first.request.body).toBe(false);
    first.flush(request(), { status: 201, statusText: 'Created' });
    const g = TestBed.createComponent(RequestNew);
    g.detectChanges();
    ctrl.expectOne(`${api}/clients`).flush([]);
    ctrl.match(`${api}/certifications/machine-types`).forEach((x) => x.flush([]));
    const c2 = g.componentInstance as unknown as typeof c;
    c2.clientName.set('Al Noor');
    c2.location.set('Cairo');
    c2.manDay.set(2);
    c2.create();
    const second = ctrl.expectOne((r) => r.method === 'POST' && r.url === `${api}/requests`);
    expect(second.request.body.manDayDays).toBe(2);
    second.flush(request({ manDayDays: 2 }), { status: 201, statusText: 'Created' });
  });
});
