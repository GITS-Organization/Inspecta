import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { Client, Contact, SaveClient } from '../../api/coordinator.models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { ClientArchive } from './client-archive';
import { ClientEmails } from './client-emails';
import { ClientItems } from './client-items';

type Tab = 'details' | 'items' | 'emails' | 'history';

/** One client (or a new one): details and contacts, items, the senders that count as this client, and the archive. */
@Component({
  selector: 'app-client-detail',
  imports: [RouterLink, ClientItems, ClientEmails, ClientArchive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './client-detail.html',
  styleUrl: './client-detail.scss',
})
export class ClientDetail {
  /** Absent on `/clients/new`. */
  readonly clientId = input<string>();

  private readonly api = inject(CoordinatorApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly client = signal<Client | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly tab = signal<Tab>('details');
  protected readonly confirmDelete = signal(false);
  protected readonly tabs: { id: Tab; label: string }[] = [
    { id: 'details', label: 'Details' },
    { id: 'items', label: 'Items' },
    { id: 'emails', label: 'Senders' },
    { id: 'history', label: 'History and certificates' },
  ];

  protected readonly name = signal('');
  protected readonly isCompany = signal(true);
  protected readonly hasContract = signal(false);
  protected readonly location = signal('');
  protected readonly certEmail = signal('');
  protected readonly tax = signal('');
  protected readonly notes = signal('');
  protected readonly contacts = signal<Contact[]>([]);

  constructor() {
    effect(() => {
      const id = this.clientId();
      untracked(() => (id ? this.load(id) : this.reset()));
    });
  }

  private reset(): void {
    this.client.set(null);
    this.tab.set('details');
    this.apply(null);
  }

  private load(id: string): void {
    this.loading.set(true);
    this.api.client(id).subscribe({
      next: (c) => {
        this.client.set(c);
        this.apply(c);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The client could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  private apply(c: Client | null): void {
    this.name.set(c?.name ?? '');
    this.isCompany.set(c?.isCompany ?? true);
    this.hasContract.set(c?.hasContract ?? false);
    this.location.set(c?.location ?? '');
    this.certEmail.set(c?.certificatesEmail ?? '');
    this.tax.set(c?.taxNumber ?? '');
    this.notes.set(c?.notes ?? '');
    this.contacts.set((c?.contacts ?? []).map((x) => ({ ...x })));
  }

  protected addContact(): void {
    this.contacts.update((l) => [...l, { name: '', email: null, phone: null, isPrimary: l.length === 0 }]);
  }
  protected removeContact(i: number): void {
    this.contacts.update((l) => l.filter((_, x) => x !== i));
  }
  protected setContact(i: number, patch: Partial<Contact>): void {
    this.contacts.update((l) => l.map((c, x) => (x === i ? { ...c, ...patch } : patch.isPrimary ? { ...c, isPrimary: false } : c)));
  }

  protected save(): void {
    if (this.busy()) return;
    if (!this.name().trim()) {
      this.error.set('Give the client a name.');
      return;
    }
    const body: SaveClient = {
      name: this.name().trim(),
      isCompany: this.isCompany(),
      hasContract: this.hasContract(),
      location: this.location().trim() || null,
      certificatesEmail: this.certEmail().trim() || null,
      taxNumber: this.tax().trim() || null,
      notes: this.notes().trim() || null,
      contacts: this.contacts().filter((c) => c.name.trim()).map((c) => ({ ...c, name: c.name.trim(), email: c.email?.trim() || null, phone: c.phone?.trim() || null })),
    };
    this.busy.set(true);
    this.error.set(null);
    const id = this.clientId();
    (id ? this.api.updateClient(id, body) : this.api.createClient(body)).subscribe({
      next: (c) => {
        this.busy.set(false);
        this.toast.show(id ? 'Saved.' : 'Client added.');
        if (id) {
          this.client.set(c);
          this.apply(c);
        } else void this.router.navigate(['/clients', c.id]);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }

  protected remove(): void {
    const id = this.clientId();
    if (!id || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.api.deleteClient(id).subscribe({
      next: () => {
        this.toast.show('The client was deleted.');
        void this.router.navigate(['/clients']);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.confirmDelete.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }
}
