import { Injectable, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

const STORE = 'integrity.dates';
const DAY = /^\d{4}-\d{2}-\d{2}$/;
const valid = (v: unknown): v is string => typeof v === 'string' && DAY.test(v) && !isNaN(new Date(v + 'T00:00').getTime());

/** The pages whose lists the date range filters (the API takes `from` and `to` there). */
export const RANGE_PAGES = ['/requests', '/quotations', '/queue', '/review', '/overview'];
export const onRangePage = (url: string): boolean => RANGE_PAGES.some((p) => url === p || url.startsWith(p + '/') || url.startsWith(p + '?'));

const iso = (d: Date): string => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const shift = (d: Date, days: number): Date => new Date(d.getFullYear(), d.getMonth(), d.getDate() + days);

export interface Preset {
  id: string;
  label: string;
  range: (today: Date) => { from: string; to: string };
}

export const PRESETS: Preset[] = [
  { id: 'today', label: 'Today', range: (t) => ({ from: iso(t), to: iso(t) }) },
  { id: '7d', label: 'Last 7 days', range: (t) => ({ from: iso(shift(t, -6)), to: iso(t) }) },
  { id: 'month', label: 'This month', range: (t) => ({ from: iso(new Date(t.getFullYear(), t.getMonth(), 1)), to: iso(t) }) },
  { id: 'last', label: 'Last month', range: (t) => ({ from: iso(new Date(t.getFullYear(), t.getMonth() - 1, 1)), to: iso(new Date(t.getFullYear(), t.getMonth(), 0)) }) },
  { id: 'since', label: 'Since 1 Nov 2026', range: () => ({ from: '2026-11-01', to: '' }) },
];

/** `d MMM`, with the year when it is not the current one. */
export function shortDate(day: string, today = new Date()): string {
  const d = new Date(day + 'T00:00');
  const base = d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' });
  return d.getFullYear() === today.getFullYear() ? base : `${base} ${d.getFullYear()}`;
}

/** What is wrong with a range, as a sentence; empty when it is fine. */
export function rangeProblem(from: string, to: string): string {
  return from && to && to < from ? 'The end date cannot be before the start date.' : '';
}

/**
 * One date range for every list of the app. It lives in the URL (`?from=&to=`), so refresh, back and sharing keep it, and in the
 * browser's storage, so the next page opened has it too. Empty strings mean "no limit" on that side.
 */
@Injectable({ providedIn: 'root' })
export class DateRangeService {
  private readonly router = inject(Router);
  readonly from = signal('');
  readonly to = signal('');
  readonly active = computed(() => !!this.from() || !!this.to());
  /** "Showing 1 Nov to 3 Nov", or "Showing all dates". */
  readonly label = computed(() => {
    const f = this.from();
    const t = this.to();
    if (f && t) return f === t ? `Showing ${shortDate(f)}` : `Showing ${shortDate(f)} to ${shortDate(t)}`;
    if (f) return `Showing from ${shortDate(f)}`;
    if (t) return `Showing up to ${shortDate(t)}`;
    return 'Showing all dates';
  });

  constructor() {
    const saved = this.read();
    this.from.set(saved.from);
    this.to.set(saved.to);
    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd)).subscribe((e) => this.follow(e.urlAfterRedirects));
  }

  /** Sets the range (either side may be empty) and puts it in the URL of the current page. */
  set(from: string, to: string): void {
    if (rangeProblem(from, to)) return;
    this.from.set(valid(from) ? from : '');
    this.to.set(valid(to) ? to : '');
    this.write();
    if (onRangePage(this.router.url)) void this.sync();
  }
  clear(): void {
    this.set('', '');
  }

  /** The URL is the truth when it carries a range (shared link, refresh, back); otherwise the page gets the range we hold. */
  private follow(url: string): void {
    const q = new URL(url, 'http://x').searchParams;
    const hasUrl = q.has('from') || q.has('to');
    if (hasUrl) {
      const f = q.get('from') ?? '';
      const t = q.get('to') ?? '';
      const nf = valid(f) ? f : '';
      const nt = valid(t) ? t : '';
      if (nf !== this.from() || nt !== this.to()) {
        this.from.set(nf);
        this.to.set(nt);
        this.write();
      }
    } else if (this.active() && onRangePage(url)) void this.sync();
  }

  private sync(): Promise<boolean> {
    return this.router.navigate([], { queryParams: { from: this.from() || null, to: this.to() || null }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  private read(): { from: string; to: string } {
    try {
      const v = JSON.parse(localStorage.getItem(STORE) ?? '{}') as { from?: string; to?: string };
      return { from: valid(v.from) ? v.from : '', to: valid(v.to) ? v.to : '' };
    } catch {
      return { from: '', to: '' };
    }
  }
  private write(): void {
    try {
      localStorage.setItem(STORE, JSON.stringify({ from: this.from(), to: this.to() }));
    } catch {
      /* storage unavailable */
    }
  }
}
