import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { authGuard, roleGuard } from './guards';
import { Role } from './models';
import { fakeAuth } from './testing';

const run = (guard: ReturnType<typeof roleGuard>) => TestBed.runInInjectionContext(() => guard({} as never, {} as never));
const setup = () => TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });

describe('guards', () => {
  beforeEach(() => {
    localStorage.clear();
    setup();
  });
  afterEach(() => localStorage.clear());

  const signInAs = (role: Role) => {
    localStorage.setItem('integrity.session', JSON.stringify(fakeAuth(role)));
    TestBed.resetTestingModule(); // the service reads storage when it is created
    setup();
    expect(TestBed.inject(AuthService).role()).toBe(role);
  };
  const path = (r: unknown) => TestBed.inject(Router).serializeUrl(r as UrlTree);

  it('sends guests to the login page', () => {
    expect(path(run(authGuard))).toBe('/login');
    expect(path(run(roleGuard('admin')))).toBe('/login');
  });

  it('lets an allowed role in', () => {
    signInAs('technical_manager');
    expect(run(roleGuard('admin', 'technical_manager'))).toBe(true);
  });

  it('sends a coordinator who opens a manager page to their own start page', () => {
    signInAs('coordinator');
    expect(path(run(roleGuard('admin', 'technical_manager')))).toBe('/jobs');
  });

  it('sends an inspector away from the overview to their jobs', () => {
    signInAs('inspector');
    expect(path(run(roleGuard('admin', 'technical_manager')))).toBe('/jobs');
  });
});
