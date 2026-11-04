import { HttpErrorResponse } from '@angular/common/http';
import { problemMessage } from './problem';

const err = (status: number, error: unknown = null) => new HttpErrorResponse({ status, error });

describe('problemMessage', () => {
  it('turns a known code into a plain sentence', () => {
    expect(problemMessage(err(409, { title: 'Conflict', detail: 'x', code: 'client_in_use' }))).toBe(
      'This client still has requests or items linked to it, so it cannot be deleted.',
    );
    expect(problemMessage(err(409, { code: 'no_recipient' }))).toContain('email address');
  });

  it('uses the detail of the answer when the code is unknown, and ignores generic titles', () => {
    expect(problemMessage(err(400, { title: 'Invalid request', detail: 'Quantity must be at least 1.', code: 'bad_request' }))).toBe('Quantity must be at least 1.');
    expect(problemMessage(err(409, { title: 'Conflict' }))).toContain('Reload the page');
  });

  it('shows the first validation error', () => {
    expect(problemMessage(err(400, { errors: { Name: ['Name is required.'] } }))).toBe('Name is required.');
  });

  it('explains 403, 404 and a lost connection', () => {
    expect(problemMessage(err(403))).toBe('Your role cannot do this.');
    expect(problemMessage(err(404))).toContain('could not be found');
    expect(problemMessage(err(0))).toContain('Cannot reach the server');
  });
});
