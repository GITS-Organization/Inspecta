import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { DateRangeService, PRESETS, Preset, onRangePage, rangeProblem } from '../core/date-range.service';

/** The one date filter of the app, above the list pages. On a phone it is a compact chip that opens a sheet. */
@Component({
  selector: 'app-date-range-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'sheet.set(false)' },
  template: `
    @if (visible()) {
      <section class="bar" [class.on]="range.active()" aria-label="Date filter">
        <button type="button" class="chipbtn" aria-haspopup="dialog" [attr.aria-expanded]="sheet()" (click)="sheet.set(true)">
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="M4 7h16v13H4zM4 11h16M8 3v4M16 3v4" /></svg>
          <span>{{ range.active() ? short() : 'All dates' }}</span>
          @if (range.active()) { <i class="dot" aria-hidden="true"></i> }
        </button>

        @if (sheet()) { <div class="scrim" (click)="sheet.set(false)"></div> }
        <div class="ctl" [class.sheet]="sheet()" [attr.role]="sheet() ? 'dialog' : null" [attr.aria-label]="sheet() ? 'Dates' : null">
          <div class="sheethead"><b>Dates</b><button type="button" class="x" aria-label="Close" (click)="sheet.set(false)">×</button></div>
          <div class="row">
            <span class="lbl">Dates</span>
            <div class="presets" role="group" aria-label="Date presets">
              @for (p of presets; track p.id) {
                <button type="button" class="pre" [attr.aria-pressed]="presetId() === p.id" (click)="apply(p)">{{ p.label }}</button>
              }
              <button type="button" class="pre" [attr.aria-pressed]="showCustom()" (click)="toggleCustom()">Custom</button>
            </div>
          </div>
          @if (showCustom()) {
            <div class="cust">
              <div class="f"><label for="dr-from">From</label><input id="dr-from" type="date" [value]="cf()" (input)="cf.set($any($event.target).value)" /></div>
              <div class="f"><label for="dr-to">To</label><input id="dr-to" type="date" [value]="ct()" (input)="ct.set($any($event.target).value)" /></div>
              <button type="button" class="btn small" [disabled]="!!problem()" (click)="applyCustom()">Apply</button>
              @if (problem(); as p) { <span class="bad" role="alert">{{ p }}</span> }
            </div>
          }
          <div class="state" role="status">
            <b>{{ range.label() }}</b>
            @if (range.active()) { <button type="button" class="clear" (click)="clear()">Clear</button> }
          </div>
        </div>
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .bar {
      border: 1.5px solid var(--line);
      border-left: 5px solid var(--line);
      border-radius: 10px;
      background: #fff;
      padding: 8px 14px;
      margin-bottom: 22px;
      &.on {
        border-color: var(--amber);
        border-left-color: var(--amber);
        background: var(--amber-bg);
      }
    }
    .chipbtn {
      display: none;
    }
    .sheethead {
      display: none;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
    }
    .lbl {
      font-family: var(--display);
      font-weight: 600;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--ink);
      font-size: 14px;
    }
    .presets {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
    }
    .pre {
      border: 1.5px solid var(--line);
      background: #fff;
      border-radius: 99px;
      padding: 5px 13px;
      font-weight: 600;
      font-size: 14px;
      color: var(--ink);
      cursor: pointer;
      &:hover {
        border-color: var(--ink);
      }
      &[aria-pressed='true'] {
        background: var(--ink);
        border-color: var(--ink);
        color: #fff;
      }
    }
    .cust {
      display: flex;
      align-items: end;
      gap: 12px;
      flex-wrap: wrap;
      margin-top: 10px;
      .f {
        label {
          margin-bottom: 3px;
          font-size: 13px;
        }
        input {
          padding: 7px 10px;
          border: 1.5px solid var(--line);
          border-radius: 8px;
          background: #fff;
        }
      }
    }
    .bad {
      color: var(--red);
      font-weight: 600;
    }
    .state {
      display: flex;
      align-items: center;
      gap: 14px;
      margin-top: 8px;
      font-size: 14px;
      color: var(--muted);
      b {
        color: var(--ink);
      }
    }
    .on .state b {
      color: var(--amber);
    }
    .clear {
      background: none;
      border: 0;
      padding: 0;
      font-weight: 600;
      color: var(--ink);
      text-decoration: underline;
      cursor: pointer;
    }
    .dot {
      width: 9px;
      height: 9px;
      border-radius: 50%;
      background: var(--amber);
      display: inline-block;
    }
    @media (max-width: 700px) {
      .bar {
        padding: 0;
        border: 0;
        background: none !important;
        margin-bottom: 14px;
      }
      .chipbtn {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        border: 1.5px solid var(--line);
        background: #fff;
        border-radius: 99px;
        padding: 7px 14px;
        font-weight: 600;
        color: var(--ink);
        cursor: pointer;
      }
      .on .chipbtn {
        border-color: var(--amber);
        background: var(--amber-bg);
      }
      .scrim {
        position: fixed;
        inset: 0;
        z-index: 38;
        background: rgb(19 40 93 / 0.55);
      }
      .ctl {
        display: none;
      }
      .ctl.sheet {
        display: block;
        position: fixed;
        z-index: 39;
        left: 0;
        right: 0;
        bottom: 0;
        max-height: 85vh;
        overflow-y: auto;
        background: #fff;
        border-radius: 16px 16px 0 0;
        border-top: 6px solid var(--amber);
        padding: 14px 18px 24px;
        box-shadow: 0 -14px 40px rgb(19 40 93 / 0.3);
      }
      .sheethead {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 10px;
        b {
          font-family: var(--display);
          font-size: 24px;
          color: var(--ink);
        }
      }
      .x {
        background: none;
        border: 0;
        font-size: 30px;
        line-height: 1;
        color: var(--muted);
        cursor: pointer;
      }
      .lbl {
        display: none;
      }
    }
  `,
})
export class DateRangeBar {
  protected readonly range = inject(DateRangeService);
  private readonly router = inject(Router);
  protected readonly presets = PRESETS;
  protected readonly sheet = signal(false);
  private readonly customOpen = signal(false);
  private readonly url = signal(this.router.url);
  protected readonly visible = computed(() => onRangePage(this.url()));
  protected readonly cf = signal('');
  protected readonly ct = signal('');
  protected readonly problem = computed(() => rangeProblem(this.cf(), this.ct()) || (!this.cf() && !this.ct() ? 'Choose at least one date.' : ''));

  /** The preset the current range equals, if any. */
  protected readonly presetId = computed(() => {
    if (!this.range.active()) return null;
    const today = new Date();
    return PRESETS.find((p) => {
      const r = p.range(today);
      return r.from === this.range.from() && r.to === this.range.to();
    })?.id ?? null;
  });
  protected readonly showCustom = computed(() => this.customOpen() || (this.range.active() && !this.presetId()));
  /** Short text for the chip on a phone. */
  protected readonly short = computed(() => this.range.label().replace('Showing ', ''));

  constructor() {
    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd)).subscribe((e) => {
      this.url.set(e.urlAfterRedirects);
      this.sheet.set(false);
    });
    // The custom pickers start from the range in force, also when it was changed elsewhere (a shared link).
    effect(() => {
      const f = this.range.from();
      const t = this.range.to();
      untracked(() => {
        this.cf.set(f);
        this.ct.set(t);
      });
    });
  }

  protected apply(p: Preset): void {
    const r = p.range(new Date());
    this.customOpen.set(false);
    this.range.set(r.from, r.to);
    this.sheet.set(false);
  }
  protected toggleCustom(): void {
    this.customOpen.update((o) => !o);
  }
  protected applyCustom(): void {
    if (this.problem()) return;
    this.range.set(this.cf(), this.ct());
    this.customOpen.set(false);
    this.sheet.set(false);
  }
  protected clear(): void {
    this.customOpen.set(false);
    this.range.clear();
    this.sheet.set(false);
  }
}
