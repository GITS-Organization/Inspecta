import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../environments/environment';
import { AdminApi } from './admin-api.service';
import { AuthResult, AuthUser } from './api.models';
import { authInterceptor } from './auth.interceptor';
import { AuthService, NotAdminError } from './auth.service';
import { adminGuard } from './guards';
import { problemMessage } from './problem';
import { VerifyService } from './verify.service';

const api = environment.apiUrl;

const user = (role: AuthUser['role'], id = 'u1'): AuthUser => ({
  id, email: `${id}@x.test`, fullName: id, role, isActive: true, qualifications: [], isOnLeave: false, leaveUntil: null,
  managerId: null, createdById: null, subscriptionStatus: null, subscriptionEndDate: null,
});
const session = (role: AuthUser['role'], access = 'access-1', refresh = 'refresh-1'): AuthResult => ({
  accessToken: access, refreshToken: refresh, expiresAt: '2030-01-01T00:00:00Z', user: user(role),
});

function setup() {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
  });
  return { http: TestBed.inject(HttpTestingController), auth: TestBed.inject(AuthService) };
}

describe('problemMessage', () => {
  const http = (status: number, error: unknown) => new HttpErrorResponse({ status, error });

  it('shows the detail of a 409 problem body', () => {
    expect(problemMessage(http(409, { title: 'Conflict', detail: 'A user with this email already exists' })))
      .toBe('A user with this email already exists.');
  });
  it('lists validation errors of a 400', () => {
    expect(problemMessage(http(400, { title: 'One or more validation errors occurred.', errors: { Email: ['The Email field is not a valid e-mail address.'], Password: ['Too short.'] } })))
      .toBe('The Email field is not a valid e-mail address. Too short.');
  });
  it('prefers the problems list when present', () => {
    expect(problemMessage(http(400, { title: 'Invalid request', problems: ['The logo is not valid base64'] }))).toBe('The logo is not valid base64.');
  });
  it('falls back to the title, then to a sentence for the status', () => {
    expect(problemMessage(http(409, { title: 'Conflict' }))).toBe('Conflict.');
    expect(problemMessage(http(409, null))).toBe('This conflicts with something that already exists.');
    expect(problemMessage(http(502, '<html>bad gateway</html>'))).toBe('The server had a problem. Try again in a moment.');
  });
  it('explains a network failure', () => {
    expect(problemMessage(http(0, null))).toContain('Cannot reach the server');
  });
});

describe('AuthService', () => {
  it('keeps the session of an admin and stores it', () => {
    const { http, auth } = setup();
    let signedIn: AuthUser | undefined;
    auth.login('a@x.test', 'pw').subscribe((u) => (signedIn = u));
    const req = http.expectOne(`${api}/auth/login`);
    expect(req.request.body).toEqual({ email: 'a@x.test', password: 'pw' });
    req.flush(session('admin'));
    expect(signedIn?.role).toBe('admin');
    expect(auth.isAdmin()).toBe(true);
    expect(JSON.parse(localStorage.getItem('gits.session')!).accessToken).toBe('access-1');
  });

  it('refuses a technical manager, keeps nothing and revokes the tokens', () => {
    const { http, auth } = setup();
    let error: unknown;
    auth.login('m@x.test', 'pw').subscribe({ error: (e) => (error = e) });
    http.expectOne(`${api}/auth/login`).flush(session('technical_manager'));
    expect(error).toBeInstanceOf(NotAdminError);
    expect(auth.isAdmin()).toBe(false);
    expect(localStorage.getItem('gits.session')).toBeNull();
    http.expectOne(`${api}/auth/logout`).flush(null, { status: 204, statusText: 'No Content' });
  });

  it('shares one refresh between parallel calls', () => {
    const { http, auth } = setup();
    auth.login('a@x.test', 'pw').subscribe();
    http.expectOne(`${api}/auth/login`).flush(session('admin'));
    auth.refresh().subscribe();
    auth.refresh().subscribe();
    const refreshes = http.match(`${api}/auth/refresh`);
    expect(refreshes.length).toBe(1);
    expect(refreshes[0].request.body).toEqual({ refreshToken: 'refresh-1' });
    refreshes[0].flush(session('admin', 'access-2', 'refresh-2'));
    expect(auth.accessToken).toBe('access-2');
  });
});

describe('authInterceptor', () => {
  function signedIn() {
    const ctx = setup();
    ctx.auth.login('a@x.test', 'pw').subscribe();
    ctx.http.expectOne(`${api}/auth/login`).flush(session('admin'));
    return ctx;
  }

  it('adds the bearer token to API calls only', () => {
    const { http } = signedIn();
    const client = TestBed.inject(HttpClient);
    client.get(`${api}/users`).subscribe();
    expect(http.expectOne(`${api}/users`).request.headers.get('Authorization')).toBe('Bearer access-1');
    client.get('https://other.test/x').subscribe();
    expect(http.expectOne('https://other.test/x').request.headers.has('Authorization')).toBe(false);
  });

  it('on a 401 refreshes once and retries with the new token', () => {
    const { http } = signedIn();
    let body: unknown;
    TestBed.inject(HttpClient).get(`${api}/users`).subscribe((b) => (body = b));
    http.expectOne(`${api}/users`).flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne(`${api}/auth/refresh`).flush(session('admin', 'access-2', 'refresh-2'));
    const retry = http.expectOne(`${api}/users`);
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush([{ ok: true }]);
    expect(body).toEqual([{ ok: true }]);
  });

  it('signs out and goes to the login page when the refresh fails', () => {
    const { http, auth } = signedIn();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    let status = 0;
    TestBed.inject(HttpClient).get(`${api}/users`).subscribe({ error: (e: HttpErrorResponse) => (status = e.status) });
    http.expectOne(`${api}/users`).flush({}, { status: 401, statusText: 'Unauthorized' });
    http.expectOne(`${api}/auth/refresh`).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(status).toBe(401);
    expect(auth.isAdmin()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('does not retry other errors (a 409 reaches the caller)', () => {
    const { http } = signedIn();
    let message = '';
    TestBed.inject(HttpClient).post(`${api}/technical-managers`, {}).subscribe({ error: (e) => (message = problemMessage(e)) });
    http.expectOne(`${api}/technical-managers`).flush({ title: 'Conflict', detail: 'This email is already used' }, { status: 409, statusText: 'Conflict' });
    expect(message).toBe('This email is already used.');
    http.expectNone(`${api}/auth/refresh`);
  });
});

describe('adminGuard', () => {
  it('lets an admin in and sends everyone else to the login page', () => {
    const { http, auth } = setup();
    const run = () => TestBed.runInInjectionContext(() => adminGuard({} as never, {} as never));
    expect(String(run())).toBe('/login');
    auth.login('a@x.test', 'pw').subscribe();
    http.expectOne(`${api}/auth/login`).flush(session('admin'));
    expect(run()).toBe(true);
  });
});

describe('VerifyService', () => {
  it('returns the certificate for a valid code', () => {
    const { http } = setup();
    let valid: boolean | undefined;
    TestBed.inject(VerifyService).verify(' ab/c ').subscribe((r) => (valid = r.valid));
    http.expectOne(`${api}/verify/ab%2Fc`).flush({ valid: true, status: 'valid', certificateNumber: 'C-1' });
    expect(valid).toBe(true);
  });
  it('treats a 404 as "not found", not as a failure', () => {
    const { http } = setup();
    let result: { valid: boolean; status: string } | undefined;
    TestBed.inject(VerifyService).verify('nope').subscribe((r) => (result = r));
    http.expectOne(`${api}/verify/nope`).flush({ valid: false, status: 'not_found' }, { status: 404, statusText: 'Not Found' });
    expect(result).toMatchObject({ valid: false, status: 'not_found' });
  });
  it('still fails on a server error', () => {
    const { http } = setup();
    let status = 0;
    TestBed.inject(VerifyService).verify('x').subscribe({ error: (e: HttpErrorResponse) => (status = e.status) });
    http.expectOne(`${api}/verify/x`).flush('', { status: 429, statusText: 'Too Many Requests' });
    expect(status).toBe(429);
  });
});

describe('AdminApi', () => {
  it('composes companies from managers, their team and their profile (404 means no profile)', () => {
    const { http, auth } = setup();
    auth.login('a@x.test', 'pw').subscribe();
    http.expectOne(`${api}/auth/login`).flush(session('admin'));

    let rows: { name: string | undefined; inspectors: number; coordinators: number }[] = [];
    TestBed.inject(AdminApi).listCompanies().subscribe((r) => (rows = r.map((x) => ({ name: x.profile?.name, inspectors: x.inspectors, coordinators: x.coordinators }))));
    const m1 = { ...user('technical_manager', 'm1') };
    const m2 = { ...user('technical_manager', 'm2') };
    http.expectOne(`${api}/users`).flush([
      m1, m2, { ...user('inspector', 'i1'), managerId: 'm1' }, { ...user('inspector', 'i2'), managerId: 'm1' }, { ...user('coordinator', 'c1'), managerId: 'm1' }, user('admin', 'a1'),
    ]);
    http.expectOne(`${api}/admin/managers/m1/company-profile`).flush({ name: 'Integrity' });
    http.expectOne(`${api}/admin/managers/m2/company-profile`).flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    expect(rows).toEqual([
      { name: 'Integrity', inspectors: 2, coordinators: 1 },
      { name: undefined, inspectors: 0, coordinators: 0 },
    ]);
  });

  it('sends the role filter and the company body to the right routes', () => {
    const { http, auth } = setup();
    auth.login('a@x.test', 'pw').subscribe();
    http.expectOne(`${api}/auth/login`).flush(session('admin'));
    const admin = TestBed.inject(AdminApi);
    admin.listUsers({ role: 'technical_manager', isActive: true }).subscribe();
    expect(http.expectOne((r) => r.url === `${api}/users`).request.params.toString()).toBe('role=technical_manager&isActive=true');
    admin.createTechnicalManager({ email: 'e@x.test', fullName: 'E', password: 'p'.repeat(12), subscriptionMonths: 6 }).subscribe();
    expect(http.expectOne(`${api}/technical-managers`).request.method).toBe('POST');
    admin.cleanupEmailBacklog({ createdBefore: '2026-01-01T00:00:00.000Z', dryRun: true }).subscribe();
    expect(http.expectOne(`${api}/admin/email-backlog/cleanup`).request.body.dryRun).toBe(true);
  });
});
