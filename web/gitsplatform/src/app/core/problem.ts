import { HttpErrorResponse } from '@angular/common/http';

/**
 * Turns whatever the server (or the network) answered into one plain sentence for the screen.
 * The API answers RFC 7807 problem details: `title`, `detail`, optionally `errors` (validation) or `problems` (a list of texts).
 */
export function problemMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return error instanceof Error && error.message ? error.message : 'Something went wrong. Try again.';
  }
  if (error.status === 0) {
    return 'Cannot reach the server. Check your connection and try again.';
  }
  const body: unknown = error.error;
  const parts: string[] = [];
  if (typeof body === 'string' && body.trim() && !body.trim().startsWith('<')) {
    parts.push(body.trim());
  } else if (body && typeof body === 'object') {
    const problem = body as Record<string, unknown>;
    parts.push(...listOf(problem['problems']));
    if (typeof problem['errors'] === 'object' && problem['errors'] !== null) {
      for (const value of Object.values(problem['errors'] as Record<string, unknown>)) parts.push(...listOf(value));
    }
    if (parts.length === 0) {
      const detail = typeof problem['detail'] === 'string' ? problem['detail'] : '';
      const title = typeof problem['title'] === 'string' ? problem['title'] : '';
      if (detail) parts.push(detail);
      else if (title) parts.push(title);
    }
  }
  if (parts.length > 0) return parts.map(sentence).join(' ');
  return statusFallback(error.status);
}

function listOf(value: unknown): string[] {
  if (typeof value === 'string') return [value];
  if (Array.isArray(value)) {
    return value.flatMap((item) =>
      typeof item === 'string' ? [item] : listOf((item as Record<string, unknown> | null)?.['message']),
    );
  }
  return [];
}

function sentence(text: string): string {
  const trimmed = text.trim();
  return /[.!?]$/.test(trimmed) ? trimmed : `${trimmed}.`;
}

function statusFallback(status: number): string {
  switch (status) {
    case 400: return 'The request was not accepted. Check the fields and try again.';
    case 401: return 'Your session has ended. Sign in again.';
    case 403: return 'You do not have access to this.';
    case 404: return 'This was not found.';
    case 409: return 'This conflicts with something that already exists.';
    case 429: return 'Too many attempts. Wait a minute and try again.';
    default: return status >= 500 ? 'The server had a problem. Try again in a moment.' : 'Something went wrong. Try again.';
  }
}
