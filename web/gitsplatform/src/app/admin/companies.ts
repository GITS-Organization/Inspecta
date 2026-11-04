import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminApi } from '../core/admin-api.service';
import { CompanyRow } from '../core/api.models';
import { problemMessage } from '../core/problem';

@Component({
  selector: 'app-companies',
  imports: [RouterLink, DatePipe],
  template: `
    <div class="head">
      <div><h1>Companies</h1><p class="muted">Technical managers and their teams</p></div>
      <a class="btn blue" routerLink="/admin/companies/new">Add a company</a>
    </div>
    @if (created(); as name) {
      <p class="notice ok" role="status">{{ name }} was created. Give the technical manager their email and password so they can sign in.</p>
    }
    @if (error(); as message) { <p class="notice error" role="alert">{{ message }} <button type="button" class="link" (click)="load()">Try again</button></p> }
    @if (loading()) { <p class="muted">Loading companies…</p> }
    @else if (rows().length === 0 && !error()) {
      <p class="empty">No companies yet. <a routerLink="/admin/companies/new">Add the first one.</a></p>
    } @else if (rows().length > 0) {
      <div class="table-scroll">
        <table>
          <thead><tr><th>Company</th><th>Technical manager</th><th>Colors</th><th>Team</th><th>Status</th></tr></thead>
          <tbody>
            @for (row of rows(); track row.manager.id) {
              <tr>
                <td>
                  <b>{{ row.profile?.name ?? 'Built-in details' }}</b>
                  <br /><span class="muted">{{ row.profile ? (row.profile.usesBuiltInDetails ? 'Built-in details, own images and colors' : row.profile.website || row.profile.address) : 'No company profile yet' }}</span>
                </td>
                <td>{{ row.manager.fullName }}<br /><span class="muted">{{ row.manager.email }}</span></td>
                <td>
                  @if (row.profile) {
                    <span class="sw" [style.background]="row.profile.primaryColor"></span><span class="sw" [style.background]="row.profile.accentColor"></span>
                  } @else { <span class="muted">Default</span> }
                </td>
                <td>{{ row.inspectors }} {{ row.inspectors === 1 ? 'inspector' : 'inspectors' }} · {{ row.coordinators }} {{ row.coordinators === 1 ? 'coordinator' : 'coordinators' }}</td>
                <td>
                  @if (!row.manager.isActive) { <span class="chip r">Disabled</span> }
                  @else if (row.manager.subscriptionStatus === 'active') { <span class="chip">Active</span> }
                  @else if (row.manager.subscriptionStatus === 'suspended') { <span class="chip g">Suspended</span> }
                  @else { <span class="chip r">Expired</span> }
                  @if (row.manager.subscriptionEndDate) { <br /><span class="muted small">until {{ row.manager.subscriptionEndDate | date: 'd MMM y' }}</span> }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: `
    .head { display: flex; justify-content: space-between; align-items: end; gap: 16px; flex-wrap: wrap; margin-bottom: 24px; }
    h1 { font-size: 44px; }
    .small { font-size: 13px; }
    .empty { padding: 40px 0; font-size: 18px; a { color: var(--blue); text-decoration: underline; } }
    .link { all: unset; cursor: pointer; text-decoration: underline; font-weight: 600; &:focus-visible { outline: 3px solid var(--blue); } }
  `,
})
export class Companies {
  private readonly api = inject(AdminApi);
  protected readonly rows = signal<CompanyRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly created = signal<string | null>(inject(ActivatedRoute).snapshot.queryParamMap.get('created'));

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.listCompanies().subscribe({
      next: (rows) => { this.rows.set(rows); this.loading.set(false); },
      error: (error: unknown) => { this.error.set(problemMessage(error)); this.loading.set(false); },
    });
  }
}
