import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { brand, contact, nav } from './site-content';

@Component({
  selector: 'app-site-shell',
  imports: [RouterLink, RouterOutlet],
  template: `
    <a class="skip" href="#main">Skip to content</a>
    <div class="wrap top">
      <a routerLink="/" class="mark" [attr.aria-label]="brand.markLabel"><i></i>{{ brand.name }}</a>
      <nav aria-label="Main">
        @for (item of nav; track item.fragment) {
          <a routerLink="/" [fragment]="item.fragment" class="hide-small">{{ item.label }}</a>
        }
        <a class="btn line small" routerLink="/login">Admin login</a>
      </nav>
    </div>
    <main id="main"><router-outlet /></main>
    <footer id="contact">
      <div class="wrap">
        <h2>{{ contact.title }}</h2>
        <div>
          <b>Write to us</b>
          <a [href]="'mailto:' + contact.email">{{ contact.email }}</a>
          <a [href]="'tel:' + contact.phone.replaceAll(' ', '')">{{ contact.phone }}</a>
        </div>
        <div>
          <b>Platform</b>
          <a routerLink="/login">Admin login</a>
          <a routerLink="/" fragment="top">Verify a certificate</a>
        </div>
        <small>{{ contact.note }}</small>
      </div>
    </footer>
  `,
  styles: `
    .skip { position: absolute; left: -9999px; background: var(--ink); color: #fff; padding: 8px 14px; z-index: 10; border-radius: 0 0 8px 0;
      &:focus { left: 0; top: 0; } }
    .top { display: flex; justify-content: space-between; align-items: center; padding: 22px 0; gap: 16px; }
    nav { display: flex; gap: 30px; font-weight: 500; align-items: center; }
    nav a:not(.btn):hover { text-decoration: underline; text-underline-offset: 5px; }
    footer { background: var(--ink); color: #fff; padding: 64px 0 40px; }
    footer .wrap { display: grid; grid-template-columns: 1.4fr 1fr 1fr; gap: 40px; }
    footer h2 { font-size: 42px; max-width: 12ch; }
    footer small { display: block; margin-top: 40px; color: #9aa3b5; grid-column: 1 / -1; }
    footer a { display: block; color: #cfd5e3; margin-top: 8px; &:hover { color: #fff; } }
    @media (max-width: 980px) { .hide-small { display: none; } footer .wrap { grid-template-columns: 1fr; } }
  `,
})
export class SiteShell {
  protected readonly brand = brand;
  protected readonly nav = nav;
  protected readonly contact = contact;
}
