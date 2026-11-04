import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, homeFor } from './auth.service';
import { Role } from './models';

/** Signed-in users only; everyone else goes to the login page. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isSignedIn() ? true : inject(Router).createUrlTree(['/login']);
};

/** Allows the listed roles; others are sent to the start page of their own role (no 403 page). */
export const roleGuard =
  (...allowed: Role[]): CanActivateFn =>
  () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const role = auth.role();
    if (!role) return router.createUrlTree(['/login']);
    return allowed.includes(role) ? true : router.createUrlTree([homeFor(role)]);
  };

/** The login page for guests; signed-in users skip it. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isSignedIn() ? inject(Router).createUrlTree([homeFor(auth.role())]) : true;
};

/** `/` goes to the right start page. */
export const homeGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return inject(Router).createUrlTree([auth.isSignedIn() ? homeFor(auth.role()) : '/login']);
};
