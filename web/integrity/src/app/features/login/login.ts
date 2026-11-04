import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService, homeFor } from '../../core/auth.service';
import { BrandingService } from '../../core/branding.service';
import { problemMessage } from '../../core/problem';
import { HttpErrorResponse } from '@angular/common/http';
import { CertificatePaper } from '../../shared/certificate-paper';

@Component({
  selector: 'app-login',
  imports: [FormsModule, CertificatePaper],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly branding = inject(BrandingService);

  protected email = '';
  protected password = '';
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (this.busy()) return;
    this.error.set(null);
    this.busy.set(true);
    this.auth.login(this.email.trim(), this.password).subscribe({
      next: () => void this.router.navigateByUrl(homeFor(this.auth.role())),
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(
          e instanceof HttpErrorResponse && e.status === 401
            ? 'That email and password do not match. Check them and try again.'
            : problemMessage(e),
        );
      },
    });
  }
}
