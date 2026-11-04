import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './models';

/** Plain sentences for the `code` the API puts on its 400/403/409/502 answers. */
const CODE_TEXT: Record<string, string> = {
  client_required: 'This sender does not belong to a client yet. Add the address to a client first.',
  request_incomplete: 'Fill in the client, the location and at least one item first.',
  no_recipient: 'There is no email address to send to. Type one in the To field.',
  email_disabled: 'Email sending is switched off, so a test email cannot be sent.',
  email_delivery_failed: 'The email could not be sent. Nothing was changed, so you can try again.',
  client_in_use: 'This client still has requests or items linked to it, so it cannot be deleted.',
  equipment_in_use: 'This item is used by requests or certificates, so it cannot be deleted.',
  user_in_use: 'This person still has records linked to them, so they cannot be deleted.',
  machine_already_started: 'This machine already has a saved certificate, so it cannot be split.',
  not_divisible: 'This machine cannot be split into several certificates.',
  inspection_not_started: 'Start the inspection before splitting the machine.',
};

/** A sentence a person can read from any failed call. */
export function problemMessage(err: unknown, fallback = 'Something went wrong. Try again.'): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return 'Cannot reach the server. Check your connection and try again.';
    const body = err.error as (ProblemDetails & { code?: string }) | string | null;
    if (body && typeof body === 'object') {
      if (body.code && CODE_TEXT[body.code]) return CODE_TEXT[body.code];
      if (body.errors) {
        const first = Object.values(body.errors).flat()[0];
        if (first) return first;
      }
      if (body.detail) return body.detail;
      if (body.title && !/^(conflict|bad request|forbidden|not found|unauthorized)$/i.test(body.title)) return body.title;
    }
    if (err.status === 401) return 'Your session ended. Sign in again.';
    if (err.status === 403) return 'Your role cannot do this.';
    if (err.status === 404) return 'This could not be found. It may have been deleted.';
    if (err.status === 409) return 'This conflicts with the current state. Reload the page and try again.';
    if (err.status === 400) return 'Some of the details are not valid. Check the form and try again.';
  }
  return fallback;
}

/** Like `problemMessage`, but the API's own sentence (its `detail`) wins over the generic text for its `code`. */
export function apiMessage(err: unknown, fallback?: string): string {
  if (err instanceof HttpErrorResponse && err.error && typeof err.error === 'object') {
    const d = (err.error as ProblemDetails).detail;
    if (d) return d;
  }
  return problemMessage(err, fallback);
}

/**
 * Like `apiMessage`, for calls whose answer is a file (`responseType: 'blob'`): an error body then arrives as a Blob, so the API's
 * sentence has to be read out of it first.
 */
export async function apiMessageAsync(err: unknown, fallback?: string): Promise<string> {
  if (err instanceof HttpErrorResponse && err.error instanceof Blob) {
    try {
      const body = JSON.parse(await err.error.text()) as ProblemDetails;
      if (body.detail) return body.detail;
    } catch {
      /* not JSON */
    }
    return apiMessage(new HttpErrorResponse({ status: err.status, statusText: err.statusText, url: err.url ?? undefined }), fallback);
  }
  return apiMessage(err, fallback);
}
