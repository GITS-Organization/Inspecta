import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

const isAuthCall = (url: string) => /\/auth\/(login|refresh|logout)$/.test(url);

/** Adds the bearer token to API calls; on a 401 refreshes once and retries, signing out when that fails. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  if (!req.url.startsWith(environment.apiBase) || isAuthCall(req.url)) return next(req);

  const withToken = (r: HttpRequest<unknown>, token: string | null) =>
    token ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;

  return next(withToken(req, auth.accessToken())).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse) || err.status !== 401 || !auth.isSignedIn()) return throwError(() => err);
      return auth.refresh().pipe(
        switchMap((s) => next(withToken(req, s.accessToken))),
        catchError((e: unknown) => {
          // The refresh itself was refused (or the retry is still unauthorised): the session is over.
          if (!(e instanceof HttpErrorResponse) || e.status === 401) auth.logout();
          return throwError(() => e);
        }),
      );
    }),
  );
};
