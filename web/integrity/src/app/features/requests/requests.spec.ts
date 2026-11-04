import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { RequestDetails, RequestSummary } from '../../api/coordinator.models';
import { relativeTime } from '../../shared/format';
import { Requests, describe as describeRequest, tabCounts } from './requests';

const api = environment.apiBase;
const req = (over: Partial<RequestDetails> = {}): RequestDetails =>
  ({
    id: 'r' + Math.random(), clientId: null, clientName: 'Al Noor', location: '', subject: 'Cranes', receivedAt: '2026-11-01T08:00:00Z', preferredDate: null, status: 'request_received',
    coordinatorId: null, inspectorId: null, scheduledAt: null, source: 'email', emailFrom: 'a@b.c', emailPreview: null, items: [], history: [], managerId: null, emailFromName: null,
    accessNotes: null, missingFields: [], canEditDetails: true, updatedAt: '', triage: 'request', maybeExpiresAt: null, quotation: null, client: null, hasUnreadClientReply: false, ...over,
  }) as RequestDetails;
const summary: RequestSummary = {
  requests: 30, maybe: 2,
  byStatus: [{ status: 'request_received', count: 5 }, { status: 'quote_draft', count: 4 }, { status: 'unassigned', count: 3 }, { status: 'in_progress', count: 6 }, { status: 'quote_rejected', count: 2 }],
};

describe('helpers', () => {
  it('builds the badge counts from the summary', () => {
    expect(tabCounts(summary, 3)).toEqual({ new: 5, details: 3, progress: 9, closed: 2, maybe: 2, all: 30 });
    expect(tabCounts(null, null).all).toBeNull();
  });

  it('says what a request needs next', () => {
    expect(describeRequest(req({ missingFields: ['location'] })).label).toBe('Needs details');
    expect(describeRequest(req()).label).toBe('New request');
    expect(describeRequest(req({ status: 'client_countered' })).label).toBe('Client replied');
    expect(describeRequest(req({ triage: 'maybe' })).label).toBe('Unknown sender');
  });

  it('writes relative times', () => {
    const now = new Date('2026-11-04T12:00:00Z');
    expect(relativeTime('2026-11-04T11:59:30Z', now)).toBe('just now');
    expect(relativeTime('2026-11-04T09:00:00Z', now)).toBe('3 hours ago');
    expect(relativeTime('2026-11-03T09:00:00Z', now)).toBe('yesterday');
    expect(relativeTime('2026-10-30T09:00:00Z', now)).toBe('5 days ago');
    expect(relativeTime('', now)).toBe('');
  });
});

describe('Requests page', () => {
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

  const open = async (inputs: Record<string, string> = {}) => {
    const f = TestBed.createComponent(Requests);
    for (const [k, v] of Object.entries(inputs)) f.componentRef.setInput(k, v);
    f.detectChanges();
    await f.whenStable();
    ctrl.expectOne(`${api}/requests/summary`).flush(summary);
    return f;
  };

  it('opens on new requests, asks the API for that status and shows live counts', async () => {
    const f = await open();
    ctrl.expectOne((r) => r.url === `${api}/requests` && r.params.get('triage') === 'request' && r.params.get('status') === 'request_received' && r.params.get('page') === '1' && r.params.get('pageSize') === '20').flush([req({ missingFields: ['location', 'items'] })]);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const tabs = Array.from(el.querySelectorAll('[role=tab]'));
    expect(tabs.map((t) => t.querySelector('.l')?.textContent)).toEqual(['New requests', 'Needs details', 'Accepted', 'Closed', 'Unknown senders', 'All']);
    expect(tabs[0].querySelector('.n')?.textContent).toBe('5');
    expect(tabs[4].querySelector('.n')?.textContent).toBe('2');
    expect(tabs[0].getAttribute('aria-selected')).toBe('true');
    expect(tabs[0].getAttribute('tabindex')).toBe('0');
    expect(tabs[1].getAttribute('tabindex')).toBe('-1');
    expect(el.textContent).toContain('Missing: location, items');
  });

  it('asks for unknown senders with triage=maybe and shows Confirm as the primary action', async () => {
    const f = await open({ tab: 'maybe' });
    ctrl.expectOne((r) => r.url === `${api}/requests` && r.params.get('triage') === 'maybe' && !r.params.has('status')).flush([req({ triage: 'maybe', clientName: '' })]);
    f.detectChanges();
    const btn = Array.from((f.nativeElement as HTMLElement).querySelectorAll('.act .btn')).map((b) => b.textContent?.trim());
    expect(btn).toEqual(['Confirm']);
  });

  it('filters the accepted tab here, from the default list', async () => {
    const f = await open({ tab: 'progress' });
    ctrl.expectOne((r) => r.url === `${api}/requests` && r.params.get('pageSize') === '100' && !r.params.has('status')).flush([
      req({ status: 'in_progress', clientName: 'Accepted one' }),
      req({ status: 'quote_rejected', clientName: 'Closed one' }),
    ]);
    f.detectChanges();
    const text = (f.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Accepted one');
    expect(text).not.toContain('Closed one');
  });

  it('moves between tabs with the arrow keys by writing the tab to the URL', async () => {
    const f = await open();
    ctrl.expectOne((r) => r.url === `${api}/requests`).flush([]);
    const nav = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    f.detectChanges();
    const first = (f.nativeElement as HTMLElement).querySelector('[role=tab]') as HTMLElement;
    first.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(nav).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: expect.objectContaining({ tab: 'details' }) }));
    first.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
    expect(nav).toHaveBeenLastCalledWith([], expect.objectContaining({ queryParams: expect.objectContaining({ tab: 'all' }) }));
  });

  it('debounces the search before it reaches the URL, and an empty search shows the clear hint', async () => {
    vi.useFakeTimers();
    try {
      const f = await open({ q: 'noor', tab: 'all' });
      ctrl.expectOne((r) => r.url === `${api}/requests` && r.params.get('q') === 'noor').flush([]);
      const nav = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
      f.detectChanges();
      expect((f.nativeElement as HTMLElement).textContent).toContain('Nothing matches');
      (f.componentInstance as unknown as { typing(v: string): void }).typing('delta');
      expect(nav).not.toHaveBeenCalled();
      vi.advanceTimersByTime(310);
      expect(nav).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: expect.objectContaining({ q: 'delta', page: null }) }));
    } finally {
      vi.useRealTimers();
    }
  });
});
