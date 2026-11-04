import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../environments/environment';
import { authInterceptor } from './auth.interceptor';
import { AuthService, homeFor, roleFromToken } from './auth.service';
import { fakeAuth, fakeJwt } from './testing';

const api = environment.apiBase;

describe('roleFromToken', () => {
  it('reads the role claim', () => {
    expect(roleFromToken(fakeJwt('technical_manager'))).toBe('technical_manager');
  });
  it('is null for garbage, a missing token and unknown roles', () => {
    expect(roleFromToken('nope')).toBeNull();
    expect(roleFromToken(null)).toBeNull();
    expect(roleFromToken(fakeJwt('superuser'))).toBeNull();
  });
  it('sends each role to its start page', () => {
    expect(homeFor('admin')).toBe('/overview');
    expect(homeFor('technical_manager')).toBe('/overview');
    expect(homeFor('inspector')).toBe('/jobs');
    expect(homeFor('coordinator')).toBe('/jobs');
    expect(homeFor(null)).toBe('/login');
  });
});

describe('auth interceptor', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'login', children: [] }]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });
  afterEach(() => {
    ctrl.verify();
    localStorage.clear();
  });

  const signIn = () => {
    auth.login('a@b.c', 'pw').subscribe();
    ctrl.expectOne(`${api}/auth/login`).flush(fakeAuth('inspector', 'one', 'r1'));
  };

  it('stores the session after login and exposes the role claim', () => {
    signIn();
    expect(auth.isSignedIn()).toBe(true);
    expect(auth.role()).toBe('inspector');
  });

  it('adds the bearer token to API calls', () => {
    signIn();
    http.get(`${api}/dashboard/overview`).subscribe();
    const req = ctrl.expectOne(`${api}/dashboard/overview`);
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${auth.accessToken()}`);
    req.flush({});
  });

  it('refreshes once for two simultaneous 401s and retries both with the new token', () => {
    signIn();
    const results: unknown[] = [];
    http.get(`${api}/a`).subscribe((r) => results.push(r));
    http.get(`${api}/b`).subscribe((r) => results.push(r));
    ctrl.expectOne(`${api}/a`).flush(null, { status: 401, statusText: 'Unauthorized' });
    ctrl.expectOne(`${api}/b`).flush(null, { status: 401, statusText: 'Unauthorized' });

    const refresh = ctrl.expectOne(`${api}/auth/refresh`); // exactly one
    expect(refresh.request.body).toEqual({ refreshToken: 'r1' });
    refresh.flush(fakeAuth('inspector', 'two', 'r2'));

    const retryA = ctrl.expectOne(`${api}/a`);
    const retryB = ctrl.expectOne(`${api}/b`);
    expect(retryA.request.headers.get('Authorization')).toContain('two');
    expect(retryB.request.headers.get('Authorization')).toContain('two');
    retryA.flush('A');
    retryB.flush('B');
    expect(results).toEqual(['A', 'B']);
  });

  it('signs out when the refresh token is refused', () => {
    signIn();
    let failed = false;
    http.get(`${api}/a`).subscribe({ error: () => (failed = true) });
    ctrl.expectOne(`${api}/a`).flush(null, { status: 401, statusText: 'Unauthorized' });
    ctrl.expectOne(`${api}/auth/refresh`).flush(null, { status: 401, statusText: 'Unauthorized' });
    ctrl.match(`${api}/auth/logout`).forEach((r) => r.flush(null));
    expect(failed).toBe(true);
    expect(auth.isSignedIn()).toBe(false);
  });

  it('does not touch calls to other hosts', () => {
    signIn();
    http.get('https://example.com/x').subscribe();
    const req = ctrl.expectOne('https://example.com/x');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });
});
