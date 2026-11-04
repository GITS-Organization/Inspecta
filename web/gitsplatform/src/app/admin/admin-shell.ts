import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { brand } from '../site/site-content';

@Component({
  selector: 'app-admin-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="adm">
      <aside>
        <a routerLink="/" class="mark"><i></i>{{ brand.name }}</a>
        <nav aria-label="Admin">
          <a routerLink="/admin" routerLinkActive="on" [routerLinkActiveOptions]="{ exact: true }" ariaCurrentWhenActive="page">Companies</a>
          <a routerLink="/admin/companies/new" routerLinkActive="on" ariaCurrentWhenActive="page">Add a company</a>
          <a routerLink="/admin/users" routerLinkActive="on" ariaCurrentWhenActive="page">Users</a>
          <a routerLink="/admin/email-backlog" routerLinkActive="on" ariaCurrentWhenActive="page">Email backlog</a>
        </nav>
        <div class="who">
          <span class="muted">{{ auth.user()?.fullName }}</span>
          <button type="button" class="out" (click)="auth.logout()">Sign out</button>
        </div>
      </aside>
      <main><router-outlet /></main>
    </div>
  `,
  styles: `
    .adm { display: grid; grid-template-columns: 230px 1fr; min-height: 100vh; }
    aside { background: #fff; border-right: 1.5px solid var(--ink); padding: 22px 16px; position: sticky; top: 0; height: 100vh; display: flex; flex-direction: column; gap: 22px; }
    nav { display: flex; flex-direction: column; gap: 2px; }
    nav a { display: block; padding: 10px 12px; border-radius: 8px; font-weight: 600; color: var(--soft); }
    nav a:hover { background: var(--paper); }
    nav a.on { background: var(--ink); color: #fff; }
    .who { margin-top: auto; display: flex; flex-direction: column; gap: 4px; font-size: 14px; }
    .out { all: unset; cursor: pointer; font-weight: 500; color: var(--blue); &:hover { text-decoration: underline; } &:focus-visible { outline: 3px solid var(--blue); outline-offset: 3px; border-radius: 4px; } }
    main { padding: 36px clamp(20px, 4vw, 56px) 80px; min-width: 0; }
    @media (max-width: 980px) {
      .adm { grid-template-columns: 1fr; }
      aside { position: static; height: auto; flex-direction: row; flex-wrap: wrap; align-items: center; border-right: 0; border-bottom: 1.5px solid var(--ink); gap: 12px 20px; }
      nav { flex-direction: row; flex-wrap: wrap; }
      .who { margin: 0 0 0 auto; }
    }
  `,
})
export class AdminShell {
  protected readonly auth = inject(AuthService);
  protected readonly brand = brand;
}
