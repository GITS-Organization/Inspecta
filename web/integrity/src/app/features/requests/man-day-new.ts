import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { Client, Inspector } from '../../api/coordinator.models';
import { AuthService } from '../../core/auth.service';
import { apiMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { formatDate } from '../../shared/format';
import { toLocalInput } from '../queue/queue';

/** Tomorrow at 09:00, in the user's time zone: a sensible start for a day the client has just asked for. */
export function defaultManDayStart(now = new Date()): string {
  const d = new Date(now);
  d.setDate(d.getDate() + 1);
  d.setHours(9, 0, 0, 0);
  return toLocalInput(d.toISOString());
}

/** A client asked for an inspector on a day, with no list of machines: choose the client, the inspector and the day, and the job starts. */
@Component({
  selector: 'app-man-day-new',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="back" routerLink="/requests">Back to requests</a>
    <div class="head">
      <div>
        <h1>New man day</h1>
        <p class="muted">A client asked for an inspector on a day. The job goes straight to the inspector: there is no quotation, and the inspector adds the machines found on site.</p>
      </div>
    </div>
    @if (error(); as msg) {
      <div class="error" role="alert">{{ msg }}</div>
    }
    <form class="narrow" (submit)="$event.preventDefault(); create()" novalidate>
      @if (isAdmin()) {
        <div class="field">
          <label for="mg">Team (technical manager id)</label>
          <input id="mg" type="text" autocomplete="off" [value]="managerId()" (change)="setManager($any($event.target).value)" />
          <small>Admins choose the team the job belongs to. The inspectors below are loaded for it.</small>
        </div>
      }

      <div class="field">
        <label for="cs">Client</label>
        @if (client(); as c) {
          <div class="picked">
            <div><b>{{ c.name }}</b><span class="muted">{{ c.location || 'No location on file' }}</span></div>
            <button type="button" class="btn ghost small" (click)="clearClient()">Change</button>
          </div>
        } @else {
          <input id="cs" type="search" autocomplete="off" placeholder="Search your registered clients" [value]="clientQuery()" (input)="clientQuery.set($any($event.target).value)" aria-controls="clist" />
          <ul id="clist" class="pick" role="listbox" aria-label="Clients">
            @for (c of matches(); track c.id) {
              <li role="option" [attr.aria-selected]="false">
                <button type="button" (click)="pickClient(c)"><b>{{ c.name }}</b><span class="muted">{{ c.location }}</span></button>
              </li>
            } @empty {
              <li class="none muted">{{ loadingClients() ? 'Loading clients…' : clientQuery() ? 'No registered client matches. Register the client first under Clients.' : 'No registered clients yet.' }}</li>
            }
          </ul>
        }
      </div>

      <div class="field">
        <label for="in">Inspector</label>
        <select id="in" (change)="inspectorId.set($any($event.target).value)">
          <option value="">Choose an inspector</option>
          @for (i of inspectors(); track i.id) {
            <option [value]="i.id" [selected]="i.id === inspectorId()">{{ label(i) }}</option>
          }
        </select>
        @if (picked(); as p) {
          @if (p.isOnLeave) {
            <small class="warn">{{ p.fullName }} is on leave{{ p.leaveUntil ? ' until ' + formatDate(p.leaveUntil) : '' }}. The API will refuse the job.</small>
          }
        }
      </div>

      <div class="row2">
        <div class="field">
          <label for="wh">Date and time</label>
          <input id="wh" type="datetime-local" [value]="when()" (input)="when.set($any($event.target).value)" />
        </div>
        <div class="field">
          <label for="lo">Location</label>
          <input id="lo" type="text" maxlength="500" placeholder="The client's location" [value]="location()" (input)="setLocation($any($event.target).value)" />
          <small>Filled from the client. Change it when the inspector goes somewhere else.</small>
        </div>
      </div>

      <div class="field">
        <label for="nt">Note for the inspector (optional)</label>
        <textarea id="nt" maxlength="2000" placeholder="What the client asked for, who to ask for at the gate…" [value]="note()" (input)="note.set($any($event.target).value)"></textarea>
      </div>

      <div class="actions">
        <button type="submit" class="btn go" [disabled]="busy()">{{ busy() ? 'Creating…' : 'Create the man day' }}</button>
        <a class="btn ghost" routerLink="/requests">Cancel</a>
      </div>
    </form>
  `,
  styles: `
    .narrow {
      max-width: 760px;
    }
    .picked,
    .pick button {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 14px;
      width: 100%;
      text-align: left;
    }
    .picked {
      border: 1.5px solid var(--ink);
      border-radius: 8px;
      padding: 8px 8px 8px 14px;
      b {
        display: block;
        color: var(--ink);
      }
    }
    .pick {
      list-style: none;
      margin: 6px 0 0;
      padding: 0;
      max-height: 216px;
      overflow-y: auto;
      border: 1.5px solid var(--line);
      border-radius: 8px;
      button {
        background: none;
        border: 0;
        border-bottom: 1px solid var(--line);
        padding: 10px 14px;
        cursor: pointer;
        b {
          color: var(--ink);
        }
        &:hover,
        &:focus-visible {
          background: var(--band);
        }
      }
      li:last-child button {
        border-bottom: 0;
      }
    }
    .none {
      padding: 12px 14px;
    }
    .warn {
      color: var(--amber) !important;
      font-weight: 600;
    }
  `,
})
export class ManDayNew implements OnInit {
  private readonly api = inject(CoordinatorApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly formatDate = formatDate;

  protected readonly isAdmin = computed(() => this.auth.role() === 'admin');
  protected readonly managerId = signal('');
  protected readonly clients = signal<Client[]>([]);
  protected readonly loadingClients = signal(true);
  protected readonly clientQuery = signal('');
  protected readonly client = signal<Client | null>(null);
  protected readonly inspectors = signal<Inspector[]>([]);
  protected readonly inspectorId = signal('');
  protected readonly when = signal(defaultManDayStart());
  protected readonly location = signal('');
  protected readonly note = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  private locationTouched = false;

  protected readonly matches = computed(() => {
    const q = this.clientQuery().trim().toLowerCase();
    return this.clients().filter((c) => !q || c.name.toLowerCase().includes(q) || (c.location ?? '').toLowerCase().includes(q)).slice(0, 50);
  });
  protected readonly picked = computed(() => this.inspectors().find((i) => i.id === this.inspectorId()) ?? null);

  ngOnInit(): void {
    this.loadLists();
  }

  private loadLists(): void {
    const m = this.isAdmin() ? this.managerId().trim() || undefined : undefined;
    this.error.set(null);
    this.loadingClients.set(true);
    this.api.clients().subscribe({
      next: (c) => {
        this.clients.set(c);
        this.loadingClients.set(false);
      },
      error: (e: unknown) => {
        this.loadingClients.set(false);
        this.error.set(apiMessage(e, 'The clients could not be loaded.'));
      },
    });
    this.api.inspectors(m).subscribe({
      next: (i) => this.inspectors.set(i),
      error: (e: unknown) => this.error.set(apiMessage(e, 'The inspectors could not be loaded.')),
    });
  }

  protected setManager(v: string): void {
    this.managerId.set(v.trim());
    this.inspectorId.set('');
    this.loadLists();
  }

  protected label(i: Inspector): string {
    return `${i.fullName} · ${i.activeJobs} active${i.isOnLeave ? ' · on leave' + (i.leaveUntil ? ' until ' + formatDate(i.leaveUntil) : '') : ''}`;
  }

  protected pickClient(c: Client): void {
    this.client.set(c);
    if (!this.locationTouched || !this.location().trim()) this.location.set(c.location ?? '');
  }
  protected clearClient(): void {
    this.client.set(null);
    this.clientQuery.set('');
  }
  protected setLocation(v: string): void {
    this.locationTouched = true;
    this.location.set(v);
  }

  protected create(): void {
    if (this.busy()) return;
    const c = this.client();
    if (!c) return void this.error.set('Choose the client. Only registered clients can be given a man day.');
    if (!this.inspectorId()) return void this.error.set('Choose an inspector.');
    if (!this.when() || isNaN(new Date(this.when()).getTime())) return void this.error.set('Choose the date and time of the visit.');
    if (this.isAdmin() && !this.managerId()) return void this.error.set('Enter the technical manager id of the team.');
    this.error.set(null);
    this.busy.set(true);
    this.api
      .createManDay({
        clientId: c.id,
        inspectorId: this.inspectorId(),
        scheduledAt: new Date(this.when()).toISOString(),
        location: this.location().trim() || null,
        note: this.note().trim() || null,
        ...(this.isAdmin() ? { managerId: this.managerId() } : {}),
      })
      .subscribe({
        next: (r) => {
          this.toast.show('The man day was created and given to the inspector.');
          void this.router.navigate(['/jobs', r.id]);
        },
        error: (e: unknown) => {
          this.busy.set(false);
          this.error.set(apiMessage(e));
        },
      });
  }
}
