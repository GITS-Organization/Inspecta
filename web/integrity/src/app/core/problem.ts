import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** A sentence a person can read from any failed call. */
export function problemMessage(err: unknown, fallback = 'Something went wrong. Try again.'): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return 'Cannot reach the server. Check your connection and try again.';
    const body = err.error as ProblemDetails | string | null;
    if (body && typeof body === 'object') {
      if (body.errors) {
        const first = Object.values(body.errors).flat()[0];
        if (first) return first;
      }
      if (body.detail) return body.detail;
      if (body.title) return body.title;
    }
    if (err.status === 401) return 'Your session ended. Sign in again.';
    if (err.status === 403) return 'You do not have access to this.';
    if (err.status === 404) return 'This could not be found.';
  }
  return fallback;
}
