import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../environments/environment';
import { DateEmpty } from '../shared/date-empty';
import { DateRangeBar } from '../shell/date-range-bar';
import { Queue } from '../features/queue/queue';
import { Quotations } from '../features/quotations/quotations';
import { ReviewList } from '../features/review/review-list';
import { Requests } from '../features/requests/requests';
import { DateRangeService, PRESETS, onRangePage, rangeProblem, shortDate } from './date-range.service';

const api = environment.apiBase;

@Component({ template: '' })
class Blank {}

describe('date range helpers', () => {
  it('builds the presets from today', () => {
    const t = new Date(2026, 10, 15); // 15 Nov 2026
    const by = Object.fromEntries(PRESETS.map((p) => [p.id, p.range(t)]));
    expect(by['today']).toEqual({ from: '2026-11-15', to: '2026-11-15' });
    expect(by['7d']).toEqual({ from: '2026-11-09', to: '2026-11-15' });
    expect(by['month']).toEqual({ from: '2026-11-01', to: '2026-11-15' });
    expect(by['last']).toEqual({ from: '2026-10-01', to: '2026-10-31' });
    expect(by['since']).toEqual({ from: '2026-11-01', to: '' });
  });
  it('writes years only when they are not the current one, and rejects an end before the start', () => {
    expect(shortDate('2026-11-03', new Date(2026, 5, 1))).toBe('3 Nov');
    expect(shortDate('2025-11-03', new Date(2026, 5, 1))).toBe('3 Nov 2025');
    expect(rangeProblem('2026-11-05', '2026-11-03')).toContain('cannot be before');
    expect(rangeProblem('2026-11-03', '2026-11-03')).toBe('');
    expect(rangeProblem('2026-11-03', '')).toBe('');
  });
  it('knows the pages the filter belongs to', () => {
    for (const u of ['/requests', '/requests?tab=all', '/queue', '/review/abc', '/quotations', '/overview']) expect(onRangePage(u)).toBe(true);
    for (const u of ['/clients', '/clients/c1', '/jobs', '/create', '/mailbox']) expect(onRangePage(u)).toBe(false);
  });
});

describe('DateRangeService and the URL', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: 'requests', component: Blank }, { path: 'clients', component: Blank }])] });
  });
  afterEach(() => localStorage.clear());

  it('takes the range from the URL, labels it, and remembers it in the browser', async () => {
    const svc = TestBed.inject(DateRangeService);
    await TestBed.inject(Router).navigateByUrl('/requests?from=2026-11-01&to=2026-11-03');
    expect(svc.from()).toBe('2026-11-01');
    expect(svc.to()).toBe('2026-11-03');
    expect(svc.active()).toBe(true);
    expect(svc.label()).toBe('Showing 1 Nov to 3 Nov');
    expect(JSON.parse(localStorage.getItem('integrity.dates')!)).toEqual({ from: '2026-11-01', to: '2026-11-03' });
  });

  it('writes a range chosen in the app into the URL, and removes it when cleared', async () => {
    const router = TestBed.inject(Router);
    const svc = TestBed.inject(DateRangeService);
    await router.navigateByUrl('/requests?tab=all');
    svc.set('2026-11-01', '');
    await new Promise((r) => setTimeout(r));
    expect(router.url).toContain('from=2026-11-01');
    expect(router.url).toContain('tab=all');
    expect(router.url).not.toContain('to=');
    expect(svc.label()).toBe('Showing from 1 Nov');
    svc.clear();
    await new Promise((r) => setTimeout(r));
    expect(router.url).toBe('/requests?tab=all');
    expect(svc.label()).toBe('Showing all dates');
  });

  it('puts the range it holds on a list page opened without one, but not on Clients', async () => {
    localStorage.setItem('integrity.dates', JSON.stringify({ from: '2026-10-01', to: '2026-10-31' }));
    const router = TestBed.inject(Router);
    TestBed.inject(DateRangeService);
    await router.navigateByUrl('/requests');
    await new Promise((r) => setTimeout(r));
    expect(router.url).toContain('from=2026-10-01');
    expect(router.url).toContain('to=2026-10-31');
    await router.navigateByUrl('/clients');
    await new Promise((r) => setTimeout(r));
    expect(router.url).toBe('/clients');
  });

  it('refuses an end date before the start date', () => {
    const svc = TestBed.inject(DateRangeService);
    svc.set('2026-11-05', '2026-11-03');
    expect(svc.active()).toBe(false);
  });
});

describe('the lists send the range', () => {
  let ctrl: HttpTestingController;
  let svc: DateRangeService;
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
    svc = TestBed.inject(DateRangeService);
    svc.set('2026-11-01', '2026-11-03');
  });
  afterEach(() => {
    ctrl.verify();
    localStorage.clear();
  });

  it('Requests sends it with the list and with the tab counts, and again when it changes', async () => {
    const f = TestBed.createComponent(Requests);
    f.detectChanges();
    f.detectChanges();
    await f.whenStable();
    const sum = ctrl.expectOne((r) => r.url === `${api}/requests/summary`);
    expect(sum.request.params.get('from')).toBe('2026-11-01');
    expect(sum.request.params.get('to')).toBe('2026-11-03');
    sum.flush({ requests: 0, maybe: 0, byStatus: [] });
    const list = ctrl.expectOne((r) => r.url === `${api}/requests`);
    expect(list.request.params.get('from')).toBe('2026-11-01');
    expect(list.request.params.get('to')).toBe('2026-11-03');
    expect(list.request.params.get('status')).toBe('request_received');
    list.flush([]);
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).textContent).toContain('Nothing in these dates.');
    expect((f.nativeElement as HTMLElement).textContent).toContain('Showing 1 Nov to 3 Nov');

    svc.set('2026-10-01', '');
    f.detectChanges();
    await f.whenStable();
    expect(ctrl.expectOne((r) => r.url === `${api}/requests/summary`).request.params.get('from')).toBe('2026-10-01');
    const again = ctrl.expectOne((r) => r.url === `${api}/requests`);
    expect(again.request.params.get('from')).toBe('2026-10-01');
    expect(again.request.params.has('to')).toBe(false);
    again.flush([]);
    // The empty state offers to clear the dates, and clearing reloads everything unfiltered.
    f.detectChanges();
    ([...(f.nativeElement as HTMLElement).querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Clear dates') as HTMLButtonElement).click();
    f.detectChanges();
    await f.whenStable();
    expect(ctrl.expectOne((r) => r.url === `${api}/requests/summary`).request.params.has('from')).toBe(false);
    const cleared = ctrl.expectOne((r) => r.url === `${api}/requests`);
    expect(cleared.request.params.has('from')).toBe(false);
    cleared.flush([]);
  });

  it('the job queue, the quotations and To review send it too', () => {
    const q = TestBed.createComponent(Queue);
    q.detectChanges();
    const queue = ctrl.expectOne((r) => r.url === `${api}/quotation-queue`);
    expect([queue.request.params.get('from'), queue.request.params.get('to')]).toEqual(['2026-11-01', '2026-11-03']);
    queue.flush([]);
    q.detectChanges();
    expect((q.nativeElement as HTMLElement).textContent).toContain('Nothing in these dates.');

    const n = TestBed.createComponent(Quotations);
    n.detectChanges();
    const quotes = ctrl.expectOne((r) => r.url === `${api}/quotations`);
    expect([quotes.request.params.get('from'), quotes.request.params.get('to')]).toEqual(['2026-11-01', '2026-11-03']);
    quotes.flush([]);

    const r = TestBed.createComponent(ReviewList);
    r.detectChanges();
    const review = ctrl.expectOne((x) => x.url === `${api}/quotation-queue/to-review`);
    expect([review.request.params.get('from'), review.request.params.get('to')]).toEqual(['2026-11-01', '2026-11-03']);
    review.flush([]);
  });

  it('the empty state is the usual one when no date filter is on', () => {
    svc.clear();
    const r = TestBed.createComponent(ReviewList);
    r.detectChanges();
    const review = ctrl.expectOne((x) => x.url === `${api}/quotation-queue/to-review`);
    expect(review.request.params.has('from')).toBe(false);
    review.flush([]);
    r.detectChanges();
    expect((r.nativeElement as HTMLElement).textContent).toContain('Nothing is waiting for your review.');
    expect(DateEmpty).toBeTruthy();
  });
});

describe('DateRangeBar', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: 'requests', component: Blank }, { path: 'clients', component: Blank }])] });
  });
  afterEach(() => localStorage.clear());

  async function open(url: string) {
    await TestBed.inject(Router).navigateByUrl(url);
    const f = TestBed.createComponent(DateRangeBar);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const btn = (t: string) => [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === t) as HTMLButtonElement;
    return { f, el, btn };
  }

  it('shows on the list pages and not on Clients', async () => {
    expect((await open('/requests')).el.querySelector('.bar')).not.toBeNull();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideRouter([{ path: 'clients', component: Blank }])] });
    expect((await open('/clients')).el.querySelector('.bar')).toBeNull();
  });

  it('applies a preset, shows the label and the active state, and clears', async () => {
    const svc = TestBed.inject(DateRangeService);
    const b = await open('/requests');
    expect(b.el.querySelector('.bar.on')).toBeNull();
    expect(b.el.textContent).toContain('Showing all dates');
    b.btn('Since 1 Nov 2026').click();
    b.f.detectChanges();
    expect(svc.from()).toBe('2026-11-01');
    expect(svc.to()).toBe('');
    expect(b.el.querySelector('.bar.on')).not.toBeNull();
    expect(b.el.textContent).toContain('Showing from 1 Nov');
    expect(b.btn('Since 1 Nov 2026').getAttribute('aria-pressed')).toBe('true');
    b.btn('Clear').click();
    b.f.detectChanges();
    expect(svc.active()).toBe(false);
  });

  it('takes two dates from Custom and will not apply an end before the start', async () => {
    const svc = TestBed.inject(DateRangeService);
    const b = await open('/requests');
    b.btn('Custom').click();
    b.f.detectChanges();
    const set = (id: string, v: string) => {
      const i = b.el.querySelector<HTMLInputElement>('#' + id)!;
      i.value = v;
      i.dispatchEvent(new Event('input'));
      b.f.detectChanges();
    };
    set('dr-from', '2026-11-05');
    set('dr-to', '2026-11-03');
    expect(b.el.textContent).toContain('The end date cannot be before the start date.');
    expect(b.btn('Apply').disabled).toBe(true);
    set('dr-to', '2026-11-07');
    b.btn('Apply').click();
    expect([svc.from(), svc.to()]).toEqual(['2026-11-05', '2026-11-07']);
  });
});
