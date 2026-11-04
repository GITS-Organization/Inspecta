import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { problemMessage } from '../core/problem';
import { brand } from '../site/site-content';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth">
      <div class="l">
        <a routerLink="/" class="mark"><i></i>{{ brand.name }}</a>
        <div>
          <h1>Admin sign in.</h1>
          <p>For the GITS team. Create and manage the inspection companies on the platform.</p>
        </div>
        <span class="aside">Not an admin? Companies sign in at their own address.</span>
      </div>
      <div class="r">
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <h2>Welcome back</h2>
          @if (error(); as message) { <p class="notice error" role="alert">{{ message }}</p> }
          <div class="field">
            <label for="email">Email</label>
            <input id="email" type="email" formControlName="email" autocomplete="username" placeholder="you@gitsplatform.com" />
          </div>
          <div class="field">
            <label for="password">Password</label>
            <input id="password" type="password" formControlName="password" autocomplete="current-password" />
          </div>
          <button class="btn wide" type="submit" [disabled]="busy()">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>
        </form>
      </div>
    </div>
  `,
  styles: `
    .auth { min-height: 100vh; display: grid; grid-template-columns: 1fr 1fr; }
    .l { background: var(--ink); color: #fff; padding: 48px clamp(24px, 6vw, 80px); display: flex; flex-direction: column; justify-content: space-between; gap: 40px; }
    .l .mark { color: #fff; } .l .mark i { background: #fff; }
    .l h1 { font-size: clamp(40px, 5vw, 64px); max-width: 10ch; }
    .l p { color: #b7bfd0; max-width: 30em; margin-top: 14px; }
    .aside { color: #8791a8; font-size: 14px; }
    .r { display: flex; align-items: center; justify-content: center; padding: 40px 24px; }
    form { width: min(380px, 100%); }
    h2 { font-size: 34px; margin-bottom: 22px; }
    .wide { width: 100%; }
    @media (max-width: 980px) { .auth { grid-template-columns: 1fr; min-height: 0; } .l { padding-block: 32px; gap: 24px; } }
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly brand = brand;

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (this.form.invalid) {
      this.error.set('Enter your email and password.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email.trim(), password).subscribe({
      next: () => void this.router.navigate(['/admin']),
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(error instanceof Error && !('status' in error) ? error.message : this.loginMessage(error));
      },
    });
  }

  private loginMessage(error: unknown): string {
    return (error as { status?: number }).status === 401 ? 'Wrong email or password.' : problemMessage(error);
  }
}
