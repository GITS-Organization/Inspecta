import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminApi } from '../core/admin-api.service';
import { problemMessage } from '../core/problem';
import { CompanyFormValue, buildCreateRequest, initials, readPngAsBase64, validateCompanyForm } from './company-request';

@Component({
  selector: 'app-company-form',
  imports: [ReactiveFormsModule],
  template: `
    <h1>Add a company</h1>
    <p class="muted">Creates the technical manager's account together with the company's identity.</p>
    <div class="formgrid">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (problems().length > 0) {
          <div class="notice error" role="alert">
            @for (p of problems(); track p) { <div>{{ p }}</div> }
          </div>
        }
        <fieldset>
          <legend>Technical manager</legend>
          <div class="row2">
            <div class="field"><label for="fullName">Full name</label><input id="fullName" type="text" formControlName="fullName" autocomplete="off" placeholder="Full name" /></div>
            <div class="field"><label for="email">Email</label><input id="email" type="email" formControlName="email" autocomplete="off" placeholder="name@company.com" /></div>
          </div>
          <div class="field">
              <label for="password">First password</label>
              <div class="pw">
                <input id="password" [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="new-password" />
                <button type="button" class="btn line small" (click)="generatePassword()">Generate</button>
              </div>
              <small>At least 12 characters. Pass it on to the manager yourself: no invitation email is sent.</small>
            </div>
          <div class="field narrow"><label for="months">Subscription (months)</label><input id="months" type="number" min="1" max="120" formControlName="subscriptionMonths" /></div>
        </fieldset>

        <fieldset>
          <legend>Company</legend>
          <div class="check field">
            <input id="builtin" type="checkbox" formControlName="useBuiltInDetails" />
            <label for="builtin">Keep the built-in company details<br /><small class="muted">Use the first customer's name, address and contacts. Only the images and colors below change.</small></label>
          </div>
          @if (!form.controls.useBuiltInDetails.value) {
            <div class="field"><label for="name">Company name</label><input id="name" type="text" formControlName="name" /></div>
            <div class="field"><label for="address">Address</label><input id="address" type="text" formControlName="address" /></div>
            <details>
              <summary>More details printed on documents</summary>
              <div class="row2">
                <div class="field"><label for="tagline">Tagline</label><input id="tagline" type="text" formControlName="tagline" placeholder="Industrial &amp; Energy Services." /></div>
                <div class="field"><label for="website">Website</label><input id="website" type="text" formControlName="website" /></div>
              </div>
              <div class="row2">
                <div class="field"><label for="certificateTitle">Name on certificates</label><input id="certificateTitle" type="text" formControlName="certificateTitle" /><small>The company name when left empty.</small></div>
                <div class="field"><label for="issuerName">Issuer name on certificates</label><input id="issuerName" type="text" formControlName="issuerName" /></div>
              </div>
              <div class="field"><label for="signerName">Signer on certificates</label><input id="signerName" type="text" formControlName="signerName" /></div>
              <div class="row2">
                <div class="field"><label for="phones">Phone numbers</label><textarea id="phones" rows="2" formControlName="phones"></textarea><small>One per line, up to 5.</small></div>
                <div class="field"><label for="emails">Company emails</label><textarea id="emails" rows="2" formControlName="emails"></textarea><small>One per line, up to 5. The first is printed on quotations.</small></div>
              </div>
            </details>
          }
        </fieldset>

        <fieldset>
          <legend>Look</legend>
          <div class="field">
            <label for="logo">Logo</label>
            <div class="drop"><input id="logo" type="file" accept="image/png" (change)="pick($event, 'logo')" />
              <small>PNG up to 1 MB with a clear background. It appears on certificates, quotations and emails. The built-in logo is used when you add none.</small></div>
            @if (logoError(); as e) { <p class="notice error">{{ e }}</p> }
          </div>
          <div class="field">
            <label for="accr">Accreditation marks</label>
            <div class="drop"><input id="accr" type="file" accept="image/png" (change)="pick($event, 'accreditations')" /><small>PNG up to 1 MB, printed at the foot of certificates. Optional.</small></div>
            @if (accrError(); as e) { <p class="notice error">{{ e }}</p> }
          </div>
          <div class="row2">
            <div class="field"><label for="primary">Main color</label><div class="colors"><input id="primary" type="color" formControlName="primaryColor" /><span>{{ value().primaryColor }}</span></div></div>
            <div class="field"><label for="accent">Accent color</label><div class="colors"><input id="accent" type="color" formControlName="accentColor" /><span>{{ value().accentColor }}</span></div></div>
          </div>
        </fieldset>

        @if (serverError(); as e) { <p class="notice error" role="alert">{{ e }}</p> }
        <button class="btn blue" type="submit" [disabled]="busy()">{{ busy() ? 'Creating…' : 'Create company' }}</button>
      </form>

      <aside class="prev" aria-label="Preview">
        <b>Preview</b>
        <div class="id">
          @if (logoPreview(); as src) { <img class="lg" [src]="src" alt="Logo preview" /> }
          @else { <div class="lg" [style.background]="value().primaryColor" aria-hidden="true">{{ initials() }}</div> }
          <div>
            <div class="pn">{{ previewName() }}</div>
            <div class="muted small">{{ value().useBuiltInDetails ? 'Built-in company details' : value().address || 'Address' }}</div>
          </div>
        </div>
        <div class="bar" [style.background]="value().primaryColor"></div>
        <div class="bar short" [style.background]="value().accentColor"></div>
        <small class="muted">How the company's documents are colored.</small>
      </aside>
    </div>
  `,
  styles: `
    h1 { font-size: 44px; }
    .formgrid { display: grid; grid-template-columns: minmax(0, 1fr) 340px; gap: 40px; margin-top: 28px; align-items: start; }
    fieldset { border: 0; padding: 0; margin: 0 0 14px; min-width: 0; }
    legend { font-family: var(--display); font-weight: 800; font-size: 22px; padding: 0; margin-bottom: 14px; }
    .narrow { max-width: 200px; }
    .pw { display: flex; gap: 8px; input { min-width: 0; } .btn { flex: none; } }
    .drop { border: 2px dashed #b3b1a4; border-radius: 12px; padding: 16px; background: #fff; color: var(--muted); small { display: block; margin-top: 8px; } }
    .colors { display: flex; gap: 12px; align-items: center; }
    .colors input { width: 46px; height: 46px; border: 1.5px solid var(--line); border-radius: 10px; padding: 3px; background: #fff; cursor: pointer; }
    details { margin-bottom: 14px; summary { cursor: pointer; font-weight: 600; margin-bottom: 14px; } }
    .prev { background: #fff; border: 1.5px solid var(--ink); border-radius: 14px; padding: 20px; position: sticky; top: 24px; }
    .id { display: flex; gap: 14px; align-items: center; margin-top: 12px; }
    .lg { width: 64px; height: 64px; border-radius: 14px; object-fit: contain; background: #eee; display: grid; place-items: center; font-family: var(--display); font-weight: 800; font-size: 22px; color: #fff; flex: none; }
    .pn { font-family: var(--display); font-weight: 800; font-size: 22px; overflow-wrap: anywhere; }
    .small { font-size: 14px; }
    .bar { height: 10px; border-radius: 5px; margin: 12px 0 6px; transition: background 0.15s; }
    .bar.short { width: 40%; }
    @media (max-width: 980px) { .formgrid { grid-template-columns: 1fr; gap: 32px; } .prev { position: static; order: -1; } }
  `,
})
export class CompanyForm {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    fullName: '', email: '', password: '', subscriptionMonths: 12,
    useBuiltInDetails: false,
    name: '', address: '', certificateTitle: '', issuerName: '', tagline: '', website: '', signerName: '', phones: '', emails: '',
    primaryColor: '#7a1f2b', accentColor: '#e0a100',
  });

  private readonly images = signal<{ logo: string | null; accreditations: string | null }>({ logo: null, accreditations: null });
  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected readonly value = computed<CompanyFormValue>(() => ({
    ...this.form.getRawValue(),
    ...this.formValue(),
    logoBase64: this.images().logo,
    accreditationsBase64: this.images().accreditations,
  }));
  protected readonly previewName = computed(() => (this.value().useBuiltInDetails ? 'Built-in company' : this.value().name.trim() || 'Company name'));
  protected readonly initials = computed(() => initials(this.value().useBuiltInDetails ? 'Built in' : this.value().name));
  protected readonly logoPreview = computed(() => (this.images().logo ? `data:image/png;base64,${this.images().logo}` : null));

  protected readonly showPassword = signal(false);
  protected readonly busy = signal(false);
  protected readonly problems = signal<string[]>([]);
  protected readonly serverError = signal<string | null>(null);
  protected readonly logoError = signal<string | null>(null);
  protected readonly accrError = signal<string | null>(null);

  protected generatePassword(): void {
    const alphabet = 'abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    const bytes = crypto.getRandomValues(new Uint32Array(16));
    const password = Array.from(bytes, (n) => alphabet[n % alphabet.length]).join('');
    this.form.controls.password.setValue(password);
    this.showPassword.set(true);
  }

  protected async pick(event: Event, which: 'logo' | 'accreditations'): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const setError = which === 'logo' ? this.logoError : this.accrError;
    setError.set(null);
    if (!file) { this.images.update((i) => ({ ...i, [which]: null })); return; }
    try {
      const base64 = await readPngAsBase64(file);
      this.images.update((i) => ({ ...i, [which]: base64 }));
    } catch (error) {
      input.value = '';
      this.images.update((i) => ({ ...i, [which]: null }));
      setError.set(error instanceof Error ? error.message : 'The image could not be used.');
    }
  }

  protected submit(): void {
    this.serverError.set(null);
    const value = this.value();
    const problems = validateCompanyForm(value);
    this.problems.set(problems);
    if (problems.length > 0) return;
    this.busy.set(true);
    this.api.createTechnicalManager(buildCreateRequest(value)).subscribe({
      next: () => void this.router.navigate(['/admin'], {
        queryParams: { created: value.useBuiltInDetails ? value.fullName.trim() : value.name.trim() },
      }),
      error: (error: unknown) => { this.busy.set(false); this.serverError.set(problemMessage(error)); },
    });
  }
}
