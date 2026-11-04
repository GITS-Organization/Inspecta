import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { ManagerApi, MailboxConfig } from '../../api/manager.api';
import { AuthService } from '../../core/auth.service';
import { AuthUser } from '../../core/models';
import { apiMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { formatDate } from '../../shared/format';

/** The earliest date the API accepts, and how far ahead it lets the date go. */
export const SYNC_MIN = '2020-01-01';
export const SYNC_MAX_DAYS = 30;

const iso = (d: Date): string => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

/** The latest date a person may choose: 30 days from `now`. */
export function syncMax(now = new Date()): string {
  const d = new Date(now);
  d.setDate(d.getDate() + SYNC_MAX_DAYS);
  return iso(d);
}

/** What is wrong with a chosen date, in a sentence; empty when it is fine. */
export function syncProblem(date: string, now = new Date()): string {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || isNaN(new Date(date + 'T00:00').getTime())) return 'Choose a date.';
  if (date < SYNC_MIN) return 'The date cannot be before 1 January 2020.';
  if (date > syncMax(now)) return `The date cannot be more than ${SYNC_MAX_DAYS} days ahead.`;
  return '';
}

/** One coordinator's mailbox: what is configured and the date mail is imported from, with a confirmed way to change that date. */
@Component({
  selector: 'app-sync-from',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card" [attr.aria-label]="'Mailbox of ' + coordinator().fullName">
      <header>
        <div>
          <h2>{{ coordinator().fullName }}</h2>
          <span class="muted">{{ coordinator().email }}</span>
        </div>
        @if (config(); as c) {
          <span class="chip" [class.g]="c.configured" [class.a]="!c.configured">{{ c.configured ? 'Mailbox connected' : 'No mailbox set yet' }}</span>
        }
      </header>

      @if (loadError(); as msg) {
        <div class="error" role="alert">{{ msg }}</div>
      } @else if (config(); as c) {
        @if (c.configured) {
          <p class="muted srv">{{ c.emailUsername }} · {{ c.imapServer }}:{{ c.imapPort }}{{ c.useSsl ? ' (SSL)' : '' }}</p>
        }
        <div class="since">
          <span class="lbl">Mail is imported from</span>
          <b class="date">{{ show(c.syncFrom) }}</b>
          @if (!open()) { <button type="button" class="btn ghost small" (click)="start(c)">Change the date</button> }
        </div>

        @if (open()) {
          <div class="panel2" role="group" [attr.aria-label]="'Change the import date of ' + coordinator().fullName">
            @if (error(); as msg) { <div class="error" role="alert">{{ msg }}</div> }
            @if (!confirming()) {
              <div class="field">
                <label [for]="'d' + coordinator().id">Import mail that arrived after</label>
                <input [id]="'d' + coordinator().id" type="date" [min]="min" [max]="max" [value]="date()" (input)="date.set($any($event.target).value)" />
                <small>From 1 January 2020 up to {{ show(max) }}.</small>
              </div>
              <ul class="plain">
                <li>Only mail that arrived <b>after</b> this date is fetched.</li>
                <li>Changing it makes the import <b>start again from the new date</b>. Mail that was already imported is not imported twice.</li>
                <li>It <b>never deletes</b> requests that were already imported.</li>
              </ul>
              <div class="rowline">
                <button type="button" class="btn" [disabled]="!!problem()" (click)="review()">Review the change</button>
                <button type="button" class="btn ghost" (click)="open.set(false)">Cancel</button>
                @if (problem() && touched()) { <span class="bad">{{ problem() }}</span> }
              </div>
            } @else {
              <p class="sure">
                Import {{ coordinator().fullName }}'s mail from <b>{{ show(date()) }}</b> instead of <b>{{ show(c.syncFrom) }}</b>?
                {{ date() < c.syncFrom.slice(0, 10) ? 'Older mail will be fetched on the next import.' : 'Mail between the two dates will no longer be fetched.' }}
                Nothing that was already imported is deleted.
              </p>
              <div class="rowline">
                <button type="button" class="btn go" [disabled]="busy()" (click)="save()">{{ busy() ? 'Saving…' : 'Yes, change the date' }}</button>
                <button type="button" class="btn ghost" [disabled]="busy()" (click)="confirming.set(false)">Back</button>
              </div>
            }
          </div>
        }
      } @else {
        <p class="muted">Loading the mailbox…</p>
      }
    </section>
  `,
  styles: `
    .card {
      border: 1.5px solid var(--line);
      border-left: 6px solid var(--teal);
      border-radius: 10px;
      padding: 18px 22px;
      margin-bottom: 16px;
      background: #fff;
    }
    header {
      display: flex;
      justify-content: space-between;
      align-items: start;
      gap: 12px;
      h2 {
        font-size: 26px;
        margin: 0;
      }
    }
    .srv {
      margin: 8px 0 0;
    }
    .since {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 6px 14px;
      margin-top: 14px;
      .lbl {
        color: var(--muted);
      }
      .date {
        font-family: var(--display);
        font-size: 28px;
        color: var(--ink);
        font-weight: 600;
      }
    }
    .panel2 {
      background: var(--band);
      border-radius: 10px;
      padding: 16px 18px 14px;
      margin-top: 14px;
      input[type='date'] {
        width: auto;
        min-width: 220px;
        padding: 11px 13px;
        border: 1.5px solid var(--line);
        border-radius: 8px;
      }
    }
    .plain {
      margin: 0 0 14px;
      padding-left: 20px;
      li {
        margin-bottom: 4px;
      }
    }
    .sure {
      font-size: 16px;
      margin: 0 0 14px;
    }
    .bad {
      color: var(--red);
      font-weight: 600;
    }
  `,
})
export class SyncFrom implements OnInit {
  readonly coordinator = input.required<AuthUser>();
  /** Admin only: the team the coordinator belongs to. */
  readonly managerId = input<string>();

  private readonly api = inject(ManagerApi);
  private readonly toast = inject(ToastService);

  protected readonly min = SYNC_MIN;
  protected readonly max = syncMax();
  protected readonly config = signal<MailboxConfig | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly open = signal(false);
  protected readonly confirming = signal(false);
  protected readonly touched = signal(false);
  protected readonly date = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly problem = computed(() => syncProblem(this.date()));

  ngOnInit(): void {
    this.api.mailbox(this.coordinator().id, this.managerId()).subscribe({
      next: (c) => this.config.set(c),
      error: (e: unknown) => this.loadError.set(apiMessage(e, 'The mailbox could not be loaded.')),
    });
  }

  protected show(v: string): string {
    return formatDate(v.slice(0, 10));
  }

  protected start(c: MailboxConfig): void {
    this.date.set(c.syncFrom.slice(0, 10));
    this.open.set(true);
    this.confirming.set(false);
    this.touched.set(false);
    this.error.set(null);
  }

  protected review(): void {
    this.touched.set(true);
    if (this.problem()) return;
    this.confirming.set(true);
  }

  protected save(): void {
    if (this.busy() || this.problem()) return;
    this.busy.set(true);
    this.error.set(null);
    this.api.changeSyncFrom(this.coordinator().id, this.date(), this.managerId()).subscribe({
      next: (c) => {
        this.config.set(c);
        this.busy.set(false);
        this.open.set(false);
        this.toast.show('The import now starts from ' + this.show(c.syncFrom) + '.');
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.confirming.set(false);
        this.error.set(apiMessage(e));
      },
    });
  }
}

/** Mailbox settings of the coordinators of a team (technical manager and admin). */
@Component({
  selector: 'app-mailbox',
  imports: [SyncFrom],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="head">
      <div>
        <h1>Mailbox</h1>
        <p class="muted">The email each coordinator's requests are imported from, and since when</p>
      </div>
    </div>
    @if (isAdmin()) {
      <div class="field adm">
        <label for="mgr">Team (technical manager id)</label>
        <input id="mgr" type="text" autocomplete="off" [value]="managerId()" (change)="load($any($event.target).value)" />
        <small>Admins choose the team whose coordinators are listed.</small>
      </div>
    }
    @if (error(); as msg) {
      <div class="error" role="alert">{{ msg }}</div>
    } @else if (loading()) {
      <p class="muted empty" role="status">Loading the team…</p>
    } @else if (isAdmin() && !managerId()) {
      <p class="empty">Enter the technical manager id to see the team.</p>
    } @else if (!coordinators().length) {
      <p class="empty">This team has no coordinator yet.</p>
    } @else {
      @for (c of coordinators(); track c.id) {
        <app-sync-from [coordinator]="c" [managerId]="isAdmin() ? managerId() : undefined" />
      }
    }
  `,
  styles: `
    .adm {
      max-width: 460px;
    }
  `,
})
export class Mailbox implements OnInit {
  private readonly api = inject(ManagerApi);
  private readonly auth = inject(AuthService);

  protected readonly isAdmin = computed(() => this.auth.role() === 'admin');
  protected readonly managerId = signal('');
  protected readonly team = signal<AuthUser[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly coordinators = computed(() => this.team().filter((u) => u.role === 'coordinator' && u.isActive));

  ngOnInit(): void {
    if (this.isAdmin()) this.loading.set(false);
    else this.load('');
  }

  protected load(managerId: string): void {
    this.managerId.set(managerId.trim());
    if (this.isAdmin() && !this.managerId()) return void this.team.set([]);
    this.loading.set(true);
    this.error.set(null);
    this.api.team(this.isAdmin() ? this.managerId() : undefined).subscribe({
      next: (t) => {
        this.team.set(t);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(apiMessage(e, 'The team could not be loaded.'));
        this.loading.set(false);
      },
    });
  }
}
