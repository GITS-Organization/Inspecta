import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { VerifyDemo } from './verify-demo';
import { about, clients, hero, what } from './site-content';

@Component({
  selector: 'app-home',
  imports: [RouterLink, VerifyDemo],
  template: `
    <header id="top" class="wrap hero">
      <div>
        <h1>{{ hero.title }}</h1>
        <p>{{ hero.lead }}</p>
        <div class="cta">
          <a class="btn blue" routerLink="/" [fragment]="hero.primary.fragment">{{ hero.primary.label }}</a>
          <a class="btn line" routerLink="/" [fragment]="hero.secondary.fragment">{{ hero.secondary.label }}</a>
        </div>
      </div>
      <app-verify-demo />
    </header>
    <div class="tape" aria-hidden="true"></div>

    <section id="what" class="sec">
      <div class="wrap">
        <h2>{{ what.title }}</h2>
        <p class="lead">{{ what.lead }}</p>
        <ol class="flow">
          @for (step of what.steps; track step.title) {
            <li><b>{{ step.title }}</b><span>{{ step.text }}</span></li>
          }
        </ol>
        <div class="who">
          @for (pillar of what.pillars; track pillar.title) {
            <div><h3>{{ pillar.title }}</h3><p>{{ pillar.text }}</p></div>
          }
        </div>
      </div>
    </section>

    <section id="clients" class="sec">
      <div class="wrap clients">
        <div><h2>{{ clients.title }}</h2><p class="lead">{{ clients.lead }}</p></div>
        <div class="client-list">
          @for (client of clients.list; track client.name) {
            <article class="client">
              <img [src]="client.logo" [alt]="client.name + ' logo'" />
              <div>
                <h3>{{ client.name }}</h3>
                <p>{{ client.tagline }}</p>
                <a class="btn blue" [href]="client.url" rel="noopener">{{ client.cta }}</a>
              </div>
            </article>
          }
        </div>
      </div>
    </section>

    <section id="about" class="sec">
      <div class="wrap about">
        <h2>{{ about.title }}</h2>
        <div>@for (p of about.paragraphs; track p) { <p>{{ p }}</p> }</div>
      </div>
    </section>
  `,
  styles: `
    .hero { display: grid; grid-template-columns: 1.15fr 0.85fr; gap: 60px; align-items: center; padding: 56px 0 90px; }
    .hero h1 { font-size: clamp(46px, 7vw, 92px); }
    .hero p { font-size: 20px; color: var(--soft); max-width: 34em; margin: 24px 0 32px; }
    .cta { display: flex; gap: 12px; flex-wrap: wrap; }
    .tape { height: 14px; background: repeating-linear-gradient(-45deg, var(--tape) 0 16px, var(--ink) 16px 32px); }
    .sec { padding: 84px 0; border-top: 1.5px solid var(--ink); }
    .tape + .sec { border-top: 0; }
    .sec h2 { font-size: clamp(34px, 4.4vw, 56px); max-width: 16ch; }
    .lead { font-size: 19px; color: var(--soft); max-width: 36em; margin-top: 16px; }
    .flow { display: grid; grid-template-columns: repeat(5, 1fr); margin-top: 46px; counter-reset: s; padding: 0; }
    .flow li { list-style: none; padding: 16px 18px 0 0; border-top: 4px solid var(--ink); counter-increment: s; }
    .flow li::before { content: counter(s); display: block; font-family: var(--display); font-weight: 800; font-size: 34px; color: var(--blue); margin-bottom: 6px; }
    .flow b { display: block; font-size: 18px; margin-bottom: 4px; }
    .flow span { color: var(--soft); font-size: 15px; }
    .who { display: grid; grid-template-columns: repeat(3, 1fr); margin-top: 44px; border-top: 1.5px solid var(--ink); }
    .who > div { padding: 24px 28px 24px 0; }
    .who > div + div { padding-left: 28px; border-left: 1.5px solid var(--line); }
    .who h3 { font-size: 24px; margin-bottom: 8px; }
    .who p { color: var(--soft); }
    .clients { display: grid; grid-template-columns: 1.2fr 1fr; gap: 56px; align-items: center; }
    .client { background: #fff; border: 1.5px solid var(--ink); border-radius: 18px; padding: 30px; display: grid; grid-template-columns: auto 1fr; gap: 22px; align-items: center; }
    .client img { width: 120px; max-width: 100%; }
    .client h3 { font-size: 26px; }
    .client p { color: var(--soft); }
    .client .btn { margin-top: 14px; }
    .about { display: grid; grid-template-columns: 1fr 1fr; gap: 56px; }
    .about p { color: var(--soft); font-size: 18px; margin-bottom: 16px; max-width: 34em; }
    @media (max-width: 980px) {
      .hero, .clients, .about { grid-template-columns: 1fr; }
      .hero { gap: 44px; padding-bottom: 64px; }
      .flow { grid-template-columns: 1fr 1fr; row-gap: 28px; }
      .who { grid-template-columns: 1fr; }
      .who > div + div { padding-left: 0; border-left: 0; border-top: 1.5px solid var(--line); }
      .client { grid-template-columns: 1fr; }
    }
    @media (max-width: 520px) { .flow { grid-template-columns: 1fr; } .sec { padding: 56px 0; } }
  `,
})
export class Home {
  protected readonly hero = hero;
  protected readonly what = what;
  protected readonly clients = clients;
  protected readonly about = about;
}
