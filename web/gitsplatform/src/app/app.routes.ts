import { Routes } from '@angular/router';
import { adminGuard, guestGuard } from './core/guards';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./site/site-shell').then((m) => m.SiteShell),
    children: [{ path: '', pathMatch: 'full', loadComponent: () => import('./site/home').then((m) => m.Home) }],
  },
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./admin/login').then((m) => m.Login) },
  {
    path: 'admin',
    canActivate: [adminGuard],
    canActivateChild: [adminGuard],
    loadComponent: () => import('./admin/admin-shell').then((m) => m.AdminShell),
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./admin/companies').then((m) => m.Companies) },
      { path: 'companies/new', loadComponent: () => import('./admin/company-form').then((m) => m.CompanyForm) },
      { path: 'users', loadComponent: () => import('./admin/users').then((m) => m.Users) },
      { path: 'email-backlog', loadComponent: () => import('./admin/email-backlog').then((m) => m.EmailBacklog) },
    ],
  },
  { path: '**', redirectTo: '' },
];
