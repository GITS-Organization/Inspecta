import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth.service';
import { Client, Inspector } from '../../api/coordinator.models';
import { ManDayNew, defaultManDayStart } from './man-day-new';

const api = environment.apiBase;
const client = (over: Partial<Client> = {}): Client => ({
  id: 'c1', name: 'Al Noor Steel', isCompany: true, hasContract: false, location: 'Tenth of Ramadan', latitude: null, longitude: null, certificatesEmail: null,
  taxNumber: null, notes: null, createdAt: '2026-01-01T00:00:00Z', archivedAt: null, contacts: [], ...over,
});
const inspector = (over: Partial<Inspector> = {}): Inspector => ({ id: 'i1', fullName: 'Omar', email: 'o@x', qualifications: [], isOnLeave: false, leaveUntil: null, activeJobs: 1, ...over });

type Internals = {
  pickClient(c: Client): void; clientQuery: { set(v: string): void }; matches(): Client[]; inspectorId: { set(v: string): void };
  when: { set(v: string): void }; location: { set(v: string): void }; note: { set(v: string): void }; create(): void; setLocation(v: string): void;
};

describe('ManDayNew', () => {
  let ctrl: HttpTestingController;
  let role: string;
  beforeEach(() => {
    role = 'coordinator';
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { role: () => role } }],
    });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  function open() {
    const f = TestBed.createComponent(ManDayNew);
    f.detectChanges();
    ctrl.expectOne(`${api}/clients`).flush([client(), client({ id: 'c2', name: 'Delta Cement', location: 'Suez' })]);
    ctrl.expectOne(`${api}/inspectors`).flush([inspector(), inspector({ id: 'i2', fullName: 'Hana', isOnLeave: true, leaveUntil: '2026-12-01' })]);
    f.detectChanges();
    return { f, c: f.componentInstance as unknown as Internals, text: () => (f.nativeElement as HTMLElement).textContent ?? '' };
  }

  it('searches the registered clients and shows the on-leave state of an inspector', () => {
    const { f, c, text } = open();
    c.clientQuery.set('delta');
    expect(c.matches().map((x) => x.name)).toEqual(['Delta Cement']);
    expect(text()).toContain('Hana · 1 active · on leave until 1 Dec 2026');
    c.inspectorId.set('i2');
    f.detectChanges();
    expect(text()).toContain('Hana is on leave until 1 Dec 2026');
  });

  it('asks for the missing pieces before it calls the API', () => {
    const { f, c, text } = open();
    c.create();
    f.detectChanges();
    expect(text()).toContain('Choose the client.');
    c.pickClient(client());
    c.create();
    f.detectChanges();
    expect(text()).toContain('Choose an inspector.');
    ctrl.expectNone(`${api}/requests/man-day`);
  });

  it('creates the man day with the client location prefilled, then opens the job', () => {
    const { c } = open();
    const router = TestBed.inject(Router);
    const nav = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    c.pickClient(client());
    c.inspectorId.set('i1');
    c.when.set('2026-11-10T09:00');
    c.note.set('  Ask for the gate 3  ');
    c.create();
    const post = ctrl.expectOne(`${api}/requests/man-day`);
    expect(post.request.body).toEqual({
      clientId: 'c1', inspectorId: 'i1', scheduledAt: new Date('2026-11-10T09:00').toISOString(), location: 'Tenth of Ramadan', note: 'Ask for the gate 3',
    });
    post.flush({ id: 'r9', kind: 'man_day', status: 'assigned', items: [] }, { status: 201, statusText: 'Created' });
    expect(nav).toHaveBeenCalledWith(['/jobs', 'r9']);
  });

  it('keeps a location the coordinator typed when another client is picked', () => {
    const { c } = open();
    c.pickClient(client());
    c.setLocation('Gate 3, Suez road');
    c.pickClient(client({ id: 'c2', location: 'Suez' }));
    const nav = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    c.inspectorId.set('i1');
    c.create();
    expect(ctrl.expectOne(`${api}/requests/man-day`).request.body.location).toBe('Gate 3, Suez road');
    expect(nav).not.toHaveBeenCalled();
  });

  it('shows the API sentence for a refusal and lets the coordinator try again', () => {
    const { f, c, text } = open();
    c.pickClient(client());
    c.inspectorId.set('i2');
    c.create();
    ctrl.expectOne(`${api}/requests/man-day`).flush({ title: 'Bad Request', detail: 'The inspector is on leave on that day.' }, { status: 400, statusText: 'Bad Request' });
    f.detectChanges();
    expect(text()).toContain('The inspector is on leave on that day.');
    c.create();
    ctrl.expectOne(`${api}/requests/man-day`).flush({ title: 'Not Found', detail: 'The client was not found.' }, { status: 404, statusText: 'Not Found' });
    f.detectChanges();
    expect(text()).toContain('The client was not found.');
  });

  it('lets an admin name the team: the id goes to the inspector list and the request', () => {
    role = 'admin';
    const f = TestBed.createComponent(ManDayNew);
    f.detectChanges();
    ctrl.expectOne(`${api}/clients`).flush([client()]);
    ctrl.expectOne(`${api}/inspectors`).flush([]);
    f.detectChanges();
    const input = (f.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#mg')!;
    input.value = 'm1';
    input.dispatchEvent(new Event('change'));
    ctrl.expectOne(`${api}/clients`).flush([client()]);
    ctrl.expectOne(`${api}/inspectors?managerId=m1`).flush([inspector()]);
    const c = f.componentInstance as unknown as Internals;
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    c.pickClient(client());
    c.inspectorId.set('i1');
    c.create();
    expect(ctrl.expectOne(`${api}/requests/man-day`).request.body.managerId).toBe('m1');
  });

  it('suggests tomorrow at nine', () => {
    expect(defaultManDayStart(new Date(2026, 10, 4, 15, 30))).toBe('2026-11-05T09:00');
  });
});
