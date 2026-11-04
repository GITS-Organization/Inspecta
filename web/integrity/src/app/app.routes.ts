import { Routes } from '@angular/router';
import { authGuard, guestGuard, homeGuard, roleGuard } from './core/guards';

const managers = roleGuard('admin', 'technical_manager');
const ops = roleGuard('coordinator', 'technical_manager', 'admin');

export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], title: 'Sign in · Integrity', loadComponent: () => import('./features/login/login').then((m) => m.Login) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', canActivate: [homeGuard], children: [] },
      {
        path: 'overview',
        canActivate: [managers],
        title: 'Overview · Integrity',
        loadComponent: () => import('./features/overview/overview').then((m) => m.Overview),
      },
      {
        path: 'jobs',
        canActivate: [roleGuard('inspector')],
        title: 'Jobs · Integrity',
        loadComponent: () => import('./features/jobs/jobs').then((m) => m.Jobs),
      },
      {
        path: 'jobs/:requestId',
        canActivate: [roleGuard('inspector', 'admin', 'technical_manager')],
        title: 'Job · Integrity',
        loadComponent: () => import('./features/jobs/job').then((m) => m.Job),
      },
      {
        path: 'jobs/:requestId/machines/:itemId',
        canActivate: [roleGuard('inspector', 'admin', 'technical_manager')],
        title: 'Machine · Integrity',
        loadComponent: () => import('./features/machine-form/machine-form').then((m) => m.MachineFormPage),
      },
      {
        path: 'requests',
        canActivate: [ops],
        title: 'Requests · Integrity',
        loadComponent: () => import('./features/requests/requests').then((m) => m.Requests),
      },
      {
        path: 'requests/new',
        canActivate: [ops],
        title: 'New request · Integrity',
        loadComponent: () => import('./features/requests/request-new').then((m) => m.RequestNew),
      },
      {
        path: 'requests/:requestId',
        canActivate: [ops],
        title: 'Request · Integrity',
        loadComponent: () => import('./features/requests/request-detail').then((m) => m.RequestDetail),
      },
      {
        path: 'review',
        canActivate: [managers],
        title: 'To review · Integrity',
        loadComponent: () => import('./features/review/review-list').then((m) => m.ReviewList),
      },
      {
        path: 'review/:requestId',
        canActivate: [managers],
        title: 'Review · Integrity',
        loadComponent: () => import('./features/review/review').then((m) => m.Review),
      },
      {
        path: 'create',
        canActivate: [roleGuard('admin', 'technical_manager', 'inspector')],
        title: 'Create certification · Integrity',
        loadComponent: () => import('./features/create/create').then((m) => m.Create),
      },
    ],
  },
  { path: '**', canActivate: [homeGuard], children: [] },
];
