import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CertificateVerification } from '../core/api.models';
import { problemMessage } from '../core/problem';
import { VerifyService } from '../core/verify.service';
import { demoCertificate, hero } from './site-content';

type View =
  | { kind: 'idle' }
  | { kind: 'demo' }
  | { kind: 'loading' }
  | { kind: 'result'; result: CertificateVerification }
  | { kind: 'error'; message: string };

/** The hero's signature element: press the code for a static demo, or type a real code to call the public verify endpoint. */
@Component({
  selector: 'app-verify-demo',
  imports: [FormsModule, DatePipe],
  template: `
    <section class="scan" aria-label="Certificate verification">
      <button type="button" class="q" aria-label="Scan the demo certificate" (click)="showDemo()"></button>
      <div class="res" aria-live="polite">
        @switch (view().kind) {
          @case ('idle') { <p class="muted">{{ hero.demoHint }}</p> }
          @case ('demo') {
            <span class="ok">Valid certificate</span>
            <dl>
              <dt>Issued by</dt><dd>{{ demo.issuedBy }}</dd>
              <dt>Client</dt><dd>{{ demo.client }}</dd>
              <dt>Item</dt><dd>{{ demo.item }}</dd>
              <dt>Number</dt><dd>{{ demo.number }}</dd>
              <dt>Expires</dt><dd>{{ demo.expires }}</dd>
            </dl>
          }
          @case ('loading') { <p class="muted">Checking…</p> }
          @case ('error') { <p class="notice error">{{ message() }}</p> }
          @case ('result') {
            @if (result(); as r) {
              @if (r.valid) {
                <span class="ok" [class.warn]="r.status === 'expired'">{{ r.status === 'expired' ? 'Genuine, examination date passed' : 'Valid certificate' }}</span>
                <dl>
                  <dt>Issued by</dt><dd>{{ r.issuedBy ?? '—' }}</dd>
                  <dt>Client</dt><dd>{{ r.clientName ?? '—' }}</dd>
                  <dt>Item</dt><dd>{{ r.itemName ?? '—' }}</dd>
                  <dt>Number</dt><dd>{{ r.certificateNumber ?? '—' }}</dd>
                  <dt>Next exam</dt><dd>{{ r.nextExaminationDate ? (r.nextExaminationDate | date: 'd MMM y') : '—' }}</dd>
                </dl>
              } @else {
                <span class="ok bad">Certificate not found</span>
                <p class="muted small-p">This code does not match a certificate we issued. Check that the whole code was typed.</p>
              }
            }
          }
        }
      </div>
      <form (ngSubmit)="check()" class="code">
        <label for="code">{{ hero.codeLabel }}</label>
        <div class="code-row">
          <input id="code" name="code" type="text" [(ngModel)]="code" autocomplete="off" spellcheck="false" placeholder="Code from the QR" />
          <button class="btn small" type="submit" [disabled]="!code.trim() || view().kind === 'loading'">Check</button>
        </div>
      </form>
    </section>
  `,
  styles: `
    .scan { background: #fff; border: 1.5px solid var(--ink); border-radius: 18px; padding: 26px; box-shadow: 10px 10px 0 var(--ink); max-width: 380px; width: 100%; justify-self: center; }
    .q { display: block; width: 150px; height: 150px; margin: 0 auto 16px; border: 3px solid var(--ink); border-radius: 6px; padding: 0; cursor: pointer; position: relative;
      background: conic-gradient(from 90deg at 5px 5px, #0000 90deg, #13171f 0) 0 0 / 10px 10px, #fff; transition: transform 0.15s;
      &:hover { transform: rotate(-2deg) scale(1.03); }
      &::after { content: 'Scan'; position: absolute; inset: auto 0 -3px 0; transform: translateY(100%); text-align: center; font-weight: 600; font-size: 13px; color: var(--blue); } }
    .res { margin-top: 34px; border-top: 1.5px dashed var(--line); padding-top: 16px; min-height: 132px; }
    .ok { display: inline-block; background: var(--ok-bg); color: var(--ok); font-weight: 700; padding: 3px 12px; border-radius: 99px; font-size: 14px; }
    .ok.warn { background: var(--warn-bg); color: var(--warn); }
    .ok.bad { background: var(--bad-bg); color: var(--bad); }
    dl { display: grid; grid-template-columns: auto 1fr; gap: 3px 14px; margin-top: 10px; font-size: 14px; }
    dt { color: var(--muted); } dd { font-weight: 600; margin: 0; overflow-wrap: anywhere; }
    .small-p { font-size: 14px; margin-top: 8px; }
    .code { margin-top: 18px; border-top: 1.5px dashed var(--line); padding-top: 14px; }
    @media (max-width: 520px) { .scan { width: calc(100% - 10px); justify-self: start; } }
    .code-row { display: flex; gap: 8px; input { min-width: 0; padding: 9px 12px; } }
  `,
})
export class VerifyDemo {
  private readonly verifyApi = inject(VerifyService);
  protected readonly hero = hero;
  protected readonly demo = demoCertificate;
  protected code = '';

  protected readonly view = signal<View>({ kind: 'idle' });
  protected result() { const v = this.view(); return v.kind === 'result' ? v.result : null; }
  protected message() { const v = this.view(); return v.kind === 'error' ? v.message : ''; }

  protected showDemo(): void {
    this.view.set({ kind: 'demo' });
  }

  protected check(): void {
    const code = this.code.trim();
    if (!code) return;
    this.view.set({ kind: 'loading' });
    this.verifyApi.verify(code).subscribe({
      next: (result) => this.view.set({ kind: 'result', result }),
      error: (error: unknown) => this.view.set({ kind: 'error', message: problemMessage(error) }),
    });
  }
}
