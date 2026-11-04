import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import { EmailAttachmentUpload, SendEmail } from '../api/coordinator.models';

export interface ComposerPreview {
  emailEnabled: boolean;
  to: string | null;
  cc?: string[] | null;
  subject: string;
  body: string;
  from?: string | null;
  notice?: string | null;
  canSend: boolean;
  blockers?: string[] | null;
  attachmentNames?: string[];
}

export const MAX_FILES = 5;
export const MAX_BYTES = 15 * 1024 * 1024;

const readBase64 = (f: File): Promise<string> =>
  new Promise((resolve, reject) => {
    const r = new FileReader();
    r.onload = () => resolve(String(r.result).split(',')[1] ?? '');
    r.onerror = () => reject(r.error);
    r.readAsDataURL(f);
  });

/** The editable email of a quotation or of the certificates: To, Cc, Subject, text and extra files. Nothing is sent until a button is pressed. */
@Component({
  selector: 'app-email-composer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (preview(); as p) {
      @if (!p.emailEnabled) {
        <div class="callout info"><b>Email sending is switched off.</b> Sending only records the step; nothing is emailed to the client.</div>
      }
      @if (p.notice) {
        <div class="callout info" role="note">{{ p.notice }}</div>
      }
      @if (p.blockers?.length) {
        <div class="callout" role="alert">
          <b>It cannot be sent yet:</b>
          <ul>@for (b of p.blockers; track b) { <li>{{ b }}</li> }</ul>
        </div>
      }
      <div class="row2">
        <div class="field">
          <label [for]="id + 'to'">To</label>
          <input [id]="id + 'to'" type="text" [value]="to()" (input)="to.set($any($event.target).value)" />
          @if (p.from) { <small>Sent from {{ p.from }}</small> }
        </div>
        <div class="field">
          <label [for]="id + 'cc'">Cc</label>
          <input [id]="id + 'cc'" type="text" [value]="cc()" (input)="cc.set($any($event.target).value)" placeholder="Separate addresses with commas" />
        </div>
      </div>
      <div class="field">
        <label [for]="id + 'su'">Subject</label>
        <input [id]="id + 'su'" type="text" [value]="subject()" (input)="subject.set($any($event.target).value)" />
      </div>
      <div class="field">
        <label [for]="id + 'bo'">Message</label>
        <textarea class="tall" [id]="id + 'bo'" [value]="body()" (input)="body.set($any($event.target).value)"></textarea>
      </div>
      <div class="field">
        <label [for]="id + 'fi'">Extra files</label>
        @if (p.attachmentNames?.length) {
          <p class="muted sm">Attached automatically: {{ p.attachmentNames!.join(', ') }}</p>
        }
        <input [id]="id + 'fi'" type="file" multiple (change)="pick($any($event.target))" />
        <small>Up to {{ maxFiles }} files, 15 MB together. They go after the {{ attachLabel() }}.</small>
        @for (f of files(); track f.fileName) {
          <div class="file"><span>{{ f.fileName }}</span> <button type="button" class="btn ghost small" (click)="drop(f)">Remove</button></div>
        }
        @if (fileError()) { <small class="bad" role="alert">{{ fileError() }}</small> }
      </div>
      <div class="rowline">
        <button type="button" class="btn go" [disabled]="busy() || !p.canSend" (click)="send.emit(value())">{{ sendLabel() }}</button>
        @if (showTest()) {
          <button type="button" class="btn ghost" [disabled]="busy()" (click)="test.emit(value())">Send me a test copy</button>
        }
        <ng-content />
      </div>
    } @else {
      <p class="muted">Preparing the email…</p>
    }
  `,
  styles: `
    .tall {
      min-height: 220px;
    }
    .sm {
      font-size: 13px;
    }
    .file {
      display: flex;
      gap: 10px;
      align-items: center;
      margin-top: 6px;
    }
    .bad {
      color: var(--red) !important;
    }
  `,
})
export class EmailComposer {
  private static seq = 0;
  protected readonly id = 'ec' + EmailComposer.seq++ + '-';
  protected readonly maxFiles = MAX_FILES;

  readonly preview = input<ComposerPreview | null>(null);
  readonly sendLabel = input('Send');
  readonly attachLabel = input('quotation PDF');
  readonly showTest = input(false);
  readonly busy = input(false);
  readonly send = output<SendEmail>();
  readonly test = output<SendEmail>();

  protected readonly to = signal('');
  protected readonly cc = signal('');
  protected readonly subject = signal('');
  protected readonly body = signal('');
  protected readonly files = signal<EmailAttachmentUpload[]>([]);
  protected readonly fileError = signal<string | null>(null);
  private readonly sizes = new Map<string, number>();

  protected readonly value = computed<SendEmail>(() => ({
    sentTo: this.to().trim() || null,
    cc: this.cc().split(/[,;\s]+/).map((x) => x.trim()).filter(Boolean),
    subject: this.subject().trim() || null,
    message: this.body() || null,
    attachments: this.files().length ? this.files() : null,
  }));

  constructor() {
    effect(() => {
      const p = this.preview();
      if (!p) return;
      this.to.set(p.to ?? '');
      this.cc.set((p.cc ?? []).join(', '));
      this.subject.set(p.subject);
      this.body.set(p.body);
    });
  }

  protected async pick(input: HTMLInputElement): Promise<void> {
    this.fileError.set(null);
    const added = Array.from(input.files ?? []);
    input.value = '';
    if (this.files().length + added.length > MAX_FILES) {
      this.fileError.set(`You can add at most ${MAX_FILES} files.`);
      return;
    }
    const total = added.reduce((a, f) => a + f.size, 0) + [...this.sizes.values()].reduce((a, n) => a + n, 0);
    if (total > MAX_BYTES) {
      this.fileError.set('The files are larger than 15 MB together. Remove some or send them another way.');
      return;
    }
    for (const f of added) {
      const content = await readBase64(f);
      this.sizes.set(f.name, f.size);
      this.files.update((l) => [...l.filter((x) => x.fileName !== f.name), { fileName: f.name, contentBase64: content }]);
    }
  }

  protected drop(f: EmailAttachmentUpload): void {
    this.sizes.delete(f.fileName);
    this.files.update((l) => l.filter((x) => x !== f));
  }
}
