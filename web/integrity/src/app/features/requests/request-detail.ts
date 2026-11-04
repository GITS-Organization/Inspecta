import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { Client, Equipment, MachineSuggestion, RequestDetails, RequestEmail, SaveRequestItem } from '../../api/coordinator.models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { STAGE_LABEL, formatDate, plural } from '../../shared/format';
import { AssignDialog } from '../../shared/assign-dialog';
import { KindBadge } from '../../shared/kind-badge';
import { ItemsEditor } from '../../shared/items-editor';
import { ManDayOption } from '../../shared/man-day-option';

const toDateInput = (iso: string | null | undefined) => (iso ? iso.slice(0, 10) : '');

/** One request: edit what the client asked for, add suggested machines, read the original email, open or turn down the quotation. */
@Component({
  selector: 'app-request-detail',
  imports: [RouterLink, ItemsEditor, KindBadge, AssignDialog, ManDayOption],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './request-detail.html',
  styleUrl: './request-detail.scss',
})
export class RequestDetail implements OnInit {
  readonly requestId = input.required<string>();

  private readonly api = inject(CoordinatorApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly formatDate = formatDate;
  protected readonly plural = plural;
  protected readonly stage = (s: string) => (s === 'request_received' ? 'New request' : (STAGE_LABEL[s as keyof typeof STAGE_LABEL] ?? s));

  protected readonly request = signal<RequestDetails | null>(null);
  protected readonly clients = signal<Client[]>([]);
  protected readonly equipment = signal<Equipment[]>([]);
  protected readonly suggestions = signal<MachineSuggestion[]>([]);
  protected readonly email = signal<RequestEmail | null>(null);
  protected readonly loading = signal(true);
  protected readonly assigning = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly clientId = signal<string | null>(null);
  protected readonly clientName = signal('');
  protected readonly location = signal('');
  protected readonly preferred = signal('');
  protected readonly access = signal('');
  protected readonly items = signal<SaveRequestItem[]>([]);
  /** Days of the man day line; 0 when it is off. */
  protected readonly manDay = signal(0);
  protected readonly rejecting = signal(false);
  protected readonly reason = signal('');
  protected readonly reasonMissing = signal(false);

  protected readonly editable = computed(() => !!this.request()?.canEditDetails && this.request()?.triage === 'request');
  protected readonly isMaybe = computed(() => this.request()?.triage === 'maybe');
  protected readonly canReject = computed(() => {
    const s = this.request()?.status;
    return !!s && ['request_received', 'quote_draft', 'quote_sent', 'counter_offer_sent', 'client_countered'].includes(s) && !this.isMaybe();
  });
  protected readonly complete = computed(() => !!this.clientName().trim() && !!this.location().trim() && this.items().some((i) => i.name.trim()));

  ngOnInit(): void {
    this.api.clients().subscribe({ next: (c) => this.clients.set(c), error: () => undefined });
    this.load();
  }

  private load(): void {
    this.api.request(this.requestId()).subscribe({
      next: (r) => {
        this.apply(r);
        this.loading.set(false);
        if (r.clientId) this.loadClientSides(r);
        if (r.source === 'email') this.api.requestEmail(r.id).subscribe({ next: (e) => this.email.set(e), error: () => undefined });
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The request could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  private loadClientSides(r: RequestDetails): void {
    this.api.equipment(r.clientId!).subscribe({ next: (e) => this.equipment.set(e), error: () => undefined });
    if (r.triage === 'request') this.api.suggestions(r.id).subscribe({ next: (s) => this.suggestions.set(s), error: () => undefined });
  }

  private apply(r: RequestDetails): void {
    this.request.set(r);
    this.clientId.set(r.clientId);
    this.clientName.set(r.clientName ?? '');
    this.location.set(r.location ?? '');
    this.preferred.set(toDateInput(r.preferredDate));
    this.access.set(r.accessNotes ?? '');
    this.manDay.set(r.manDayDays ?? 0);
    this.items.set(
      r.items.map((i) => ({
        id: i.id,
        name: i.name,
        quantity: i.quantity,
        equipmentId: i.equipmentId,
        type: i.type,
        capacity: i.capacity,
        serialNumber: i.serialNumber,
        issueDescription: i.issueDescription,
      })),
    );
  }

  protected pickClient(id: string): void {
    const c = this.clients().find((x) => x.id === id);
    this.clientId.set(c?.id ?? null);
    if (c) {
      this.clientName.set(c.name);
      if (!this.location().trim() && c.location) this.location.set(c.location);
      this.api.equipment(c.id).subscribe({ next: (e) => this.equipment.set(e), error: () => undefined });
    } else {
      this.equipment.set([]);
    }
  }

  protected addSuggestion(s: MachineSuggestion): void {
    this.items.update((l) => [...l, { name: s.name, quantity: s.quantity, equipmentId: s.equipmentId }]);
    this.suggestions.update((l) => l.filter((x) => x !== s));
  }

  protected save(prepare: boolean | false): void {
    const r = this.request();
    if (!r || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.api
      .saveRequest(r.id, {
        clientId: this.clientId(),
        clientName: this.clientName().trim(),
        location: this.location().trim(),
        preferredDate: this.preferred() ? this.preferred() + 'T00:00:00Z' : null,
        accessNotes: this.access().trim() || null,
        items: this.items().filter((i) => i.name.trim()),
        manDayDays: this.manDay(),
        expectedUpdatedAt: r.updatedAt,
        prepareQuotation: prepare ? true : false,
      })
      .subscribe({
        next: (updated) => {
          this.busy.set(false);
          this.apply(updated);
          if (updated.clientId) this.loadClientSides(updated);
          if (prepare && updated.quotation) {
            this.toast.show('Saved. The quotation is ready for prices.');
            void this.router.navigate(['/quotations', updated.id, updated.quotation.id]);
          } else this.toast.show('Saved.');
        },
        error: (e: unknown) => {
          this.busy.set(false);
          this.error.set(problemMessage(e));
        },
      });
  }

  protected assigned(r: RequestDetails): void {
    this.assigning.set(false);
    this.toast.show('The job was given to the inspector. No quotation is needed.');
    void this.router.navigate(['/jobs', r.id]);
  }

  protected autofill(): void {
    this.run(this.api.autofill(this.requestId()), 'The email was read again.');
  }

  protected confirm(): void {
    this.run(this.api.confirmMaybe(this.requestId()), 'It is now a request.');
  }

  protected deleteMaybe(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.api.deleteMaybe(this.requestId()).subscribe({
      next: () => {
        this.toast.show('The email was deleted.');
        void this.router.navigate(['/requests']);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }

  protected reject(): void {
    if (!this.reason().trim()) {
      this.reasonMissing.set(true);
      return;
    }
    this.run(this.api.rejectRequest(this.requestId(), this.reason().trim()), 'The request was turned down.', () => this.rejecting.set(false));
  }

  private run(call: ReturnType<CoordinatorApi['request']>, message: string, after?: () => void): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: (r) => {
        this.busy.set(false);
        this.apply(r);
        if (r.clientId) this.loadClientSides(r);
        this.toast.show(message);
        after?.();
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }
}
