import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CoordinatorApi } from '../../api/coordinator.api';
import { ClientHistory, IssuedCertificate } from '../../api/coordinator.models';
import { openBlob, saveBlob } from '../../api/backend.api';
import { problemMessage } from '../../core/problem';
import { RESULT_LABEL, STAGE_LABEL, formatDate, plural, resultChip } from '../../shared/format';

/** What was done for the client: its requests, and the certificates filed under it (PDF and QR code to download). */
@Component({
  selector: 'app-client-archive',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as msg) { <div class="error" role="alert">{{ msg }}</div> }

    <section class="panel first">
      <h2>Certificates</h2>
      @if (loading()) {
        <p class="muted">Loading…</p>
      } @else if (!certs().length) {
        <p class="empty">No certificate was issued for this client yet.</p>
      } @else {
        <table>
          <thead><tr><th>Certificate</th><th>Item</th><th>Examined</th><th>Next due</th><th>Sent</th><th><span class="sr">Files</span></th></tr></thead>
          <tbody>
            @for (c of certs(); track c.id) {
              <tr>
                <td><b>{{ c.certificateNumber }}</b>@if (c.result) { <div><span class="chip" [class]="resultChip(c.result)">{{ resultLabel[c.result] }}</span></div> }</td>
                <td>{{ c.itemName }}</td>
                <td>{{ formatDate(c.examinationDate) }}</td>
                <td>{{ formatDate(c.nextExaminationDate) }}</td>
                <td class="sm">
                  @if (c.delivery; as d) { {{ d.emailSent ? 'Emailed to ' + d.sentTo : 'Handed over' }}<br />{{ formatDate(d.sentAt) }} } @else { Not sent yet }
                </td>
                <td class="act">
                  <button type="button" class="btn ghost small" (click)="pdf(c)">PDF</button>
                  <button type="button" class="btn ghost small" (click)="qr(c)">QR code</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>

    <section class="panel">
      <h2>Requests</h2>
      @if (!history()?.requests?.length && !loading()) {
        <p class="empty">No request yet for this client.</p>
      }
      @for (h of history()?.requests ?? []; track h.request.id) {
        <div class="job">
          <div>
            <b>{{ h.request.subject || 'Request of ' + formatDate(h.request.receivedAt) }}</b>
            <div class="muted">{{ formatDate(h.request.receivedAt) }} · {{ plural(h.request.items.length, 'item') }}@if (h.issuedCertificates?.length) { · {{ plural(h.issuedCertificates!.length, 'certificate') }} }</div>
          </div>
          <span class="chip">{{ stage(h.request.status) }}</span>
          <a class="btn ghost small" [routerLink]="['/requests', h.request.id]">Open</a>
        </div>
      }
    </section>
  `,
  styles: `
    .first {
      margin-top: 18px;
    }
    .sm {
      font-size: 13px;
    }
    .act {
      white-space: nowrap;
      text-align: right;
      .btn + .btn {
        margin-left: 6px;
      }
    }
  `,
})
export class ClientArchive implements OnInit {
  readonly clientId = input.required<string>();
  private readonly api = inject(CoordinatorApi);
  protected readonly formatDate = formatDate;
  protected readonly plural = plural;
  protected readonly resultLabel = RESULT_LABEL;
  protected readonly resultChip = resultChip;
  protected readonly stage = (s: string) => (s === 'request_received' ? 'New request' : (STAGE_LABEL[s as keyof typeof STAGE_LABEL] ?? s));

  protected readonly history = signal<ClientHistory | null>(null);
  protected readonly certs = signal<IssuedCertificate[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.clientCertificates(this.clientId()).subscribe({
      next: (c) => {
        this.certs.set(c);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The certificates could not be loaded.'));
        this.loading.set(false);
      },
    });
    this.api.clientHistory(this.clientId()).subscribe({ next: (h) => this.history.set(h), error: () => undefined });
  }

  protected pdf(c: IssuedCertificate): void {
    this.api.certificatePdf(this.clientId(), c.id).subscribe({ next: (b) => openBlob(b), error: (e: unknown) => this.error.set(problemMessage(e)) });
  }

  protected qr(c: IssuedCertificate): void {
    this.api.certificateQr(this.clientId(), c.id).subscribe({ next: (b) => saveBlob(b, `${c.certificateNumber}-qr.png`), error: (e: unknown) => this.error.set(problemMessage(e)) });
  }
}
