import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { Quote } from '../../api/coordinator.models';
import { egpToPiastres, formatEgp, lines } from '../../shared/format';
import { QuoteEditor } from './quote-editor';

const api = environment.apiBase;
const quote = (over: Partial<Quote> = {}): Quote => ({
  id: 'q1', requestId: 'r1', version: 1, amountPiastres: 0, validUntil: '2026-12-01T00:00:00Z', status: 'open', clientReply: null, createdAt: '2026-11-01T00:00:00Z',
  proposalNumber: 'QT-1', clientName: 'Al Noor', validForDays: 14, message: null,
  items: [
    { requestItemId: 'a', description: 'Crane', type: 'Lifting', prNumber: null, quantity: 2, unitPricePiastres: null, pricingUnit: 'visit', totalPiastres: 0 },
    { requestItemId: 'b', description: 'Hoist', type: 'Lifting', prNumber: null, quantity: 1, unitPricePiastres: 50000, pricingUnit: 'visit', totalPiastres: 50000 },
  ],
  scopeOfWork: ['Visual check'], notes: [], missingPrices: ['a'], totalUnits: 3, canEdit: true, isExpired: false, sentAt: null, updatedAt: '2026-11-01T00:00:00Z',
  supersededAt: null, emails: [], contactName: null, quotationNumber: null, clientReplies: [], ...over,
});

describe('money helpers', () => {
  it('converts pounds to piastres and back', () => {
    expect(egpToPiastres('1,250.5')).toBe(125050);
    expect(egpToPiastres('')).toBeNull();
    expect(egpToPiastres('abc')).toBeNull();
    expect(egpToPiastres('-5')).toBeNull();
    expect(formatEgp(125050)).toBe('EGP 1,250.50');
    expect(lines(' a \n\n b\r\n')).toEqual(['a', 'b']);
  });
});

describe('QuoteEditor', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  const open = async (q: Quote) => {
    const f = TestBed.createComponent(QuoteEditor);
    f.componentRef.setInput('requestId', 'r1');
    f.componentRef.setInput('quoteId', 'q1');
    f.detectChanges();
    await f.whenStable();
    ctrl.expectOne(`${api}/requests/r1/quotes/q1`).flush(q);
    f.detectChanges();
    return f;
  };
  type Inst = { setPrice(id: string, v: string): void; save(): void; reopen(): void; missing(): number };

  it('saves prices as piastres, one entry per item, and counts the missing ones', async () => {
    const f = await open(quote());
    const c = f.componentInstance as unknown as Inst;
    expect(c.missing()).toBe(0 + 0 + 1 - 0); // only item "a" has no price
    c.setPrice('a', '120.50');
    expect(c.missing()).toBe(0);
    c.save();
    const put = ctrl.expectOne((r) => r.method === 'PUT' && r.url === `${api}/requests/r1/quotes/q1`);
    expect(put.request.body.items).toEqual([
      { requestItemId: 'a', unitPricePiastres: 12050 },
      { requestItemId: 'b', unitPricePiastres: 50000 },
    ]);
    expect(put.request.body.scopeOfWork).toEqual(['Visual check']);
    put.flush(quote());
  });

  it('reopens a sent quotation as a new revision and opens it', async () => {
    const f = await open(quote({ canEdit: false, sentAt: '2026-11-02T00:00:00Z' }));
    const nav = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    (f.componentInstance as unknown as Inst).reopen();
    ctrl.expectOne(`${api}/requests/r1/quotes/q1/reopen`).flush(quote({ id: 'q2', version: 2 }));
    expect(nav).toHaveBeenCalledWith(['/quotations', 'r1', 'q2']);
  });

  it('shows the API sentence when sending is refused', async () => {
    const f = await open(quote({ canEdit: false, sentAt: '2026-11-02T00:00:00Z' }));
    (f.componentInstance as unknown as { dismiss(): void }).dismiss();
    ctrl.expectOne(`${api}/requests/r1/quotes/q1/dismiss-reply`).flush({ detail: 'There is no reply to clear.' }, { status: 409, statusText: 'Conflict' });
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('[role=alert]')?.textContent).toContain('There is no reply to clear.');
  });
});
