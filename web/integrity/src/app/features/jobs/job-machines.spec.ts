import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth.service';
import { JobStatus, MachineList, MachineStatus, MachineSummary } from '../../core/models';
import { Job } from './job';

const api = environment.apiBase;
const base = `${api}/quotation-queue/r1`;
const machine = (id: string, status: MachineStatus, over: Partial<MachineSummary> = {}): MachineSummary => ({
  requestItemId: id, name: 'Crane ' + id, quantity: 1, serialNumber: 'SN-' + id, templateKey: 'crane', status, checklistTotal: 10, checklistAnswered: 0,
  reviewNote: null, certificateNumber: null, canEdit: true, canReview: false, ...over,
});
const list = (machines: MachineSummary[], requestStatus: JobStatus = 'in_progress'): MachineList => ({
  requestId: 'r1', requestStatus, clientName: 'Al Noor', location: 'Cairo', scheduledAt: null, progress: { total: machines.length, submitted: 0, approved: 0, delivered: 0 },
  canStartInspection: false, canDeliver: false, machines,
});

const types = [{ name: 'Sling', aliases: ['Web sling'], standards: [], templateKey: 'gear', templateName: 'Gear', isGear: true }];

type Internals = { adding: { set(v: boolean): void }; editing: { set(v: MachineSummary | null): void }; remove(m: MachineSummary): void };

describe('Job: machines added on site', () => {
  let ctrl: HttpTestingController;
  let role = 'inspector';
  beforeEach(() => {
    role = 'inspector';
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { role: () => role } }],
    });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  function open(machines: MachineSummary[], status: JobStatus = 'in_progress') {
    const f = TestBed.createComponent(Job);
    f.componentRef.setInput('requestId', 'r1');
    f.detectChanges();
    ctrl.expectOne(base + '/machines').flush(list(machines, status));
    ctrl.match(`${api}/certifications/machine-types`).forEach((r) => r.flush(types));
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    return { f, el, c: f.componentInstance as unknown as Internals, text: () => el.textContent ?? '', btn: (t: string) => [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === t) };
  }

  it('offers "Add the first machine" on a man day that has none', () => {
    const { text, btn } = open([], 'assigned');
    expect(text()).toContain('No machines in this job yet.');
    expect(btn('Add the first machine')).toBeTruthy();
  });

  it('shows the machine actions only while the job is under way', () => {
    for (const status of ['certificate_submitted', 'certificate_approved', 'sent_to_client'] as JobStatus[]) {
      const { btn, f } = open([machine('a', 'not_started')], status);
      expect(btn('Add a machine')).toBeUndefined();
      expect(btn('Edit')).toBeUndefined();
      expect(btn('Remove')).toBeUndefined();
      f.destroy();
    }
  });

  it('lets only an untouched machine be removed and only an unsubmitted one be edited', () => {
    const { el } = open([machine('a', 'not_started'), machine('b', 'draft'), machine('c', 'submitted')]);
    const rows = [...el.querySelectorAll('.mrow')].map((r) => [...r.querySelectorAll('button')].map((b) => b.textContent?.trim()));
    expect(rows[0]).toEqual(['Edit', 'Remove']);
    expect(rows[1]).toEqual(['Edit']);
    expect(rows[2]).toEqual([]);
  });

  it('adds a registered machine from the dialog and refreshes the list with the answer', () => {
    const { f, btn, text } = open([machine('a', 'not_started')]);
    btn('Add a machine')!.click();
    f.detectChanges();
    ctrl.expectOne(base + '/available-machines').flush([
      { id: 'e1', clientId: 'c1', name: 'Forklift 3t', serialNumber: 'FL-77', model: 'H30', capacity: '3 t', category: null, manufacturer: null, locationInsideClientLocation: null },
      { id: 'e2', clientId: 'c1', name: 'Hoist', serialNumber: null, model: null, capacity: null, category: null, manufacturer: null, locationInsideClientLocation: 'Shed 2' },
    ]);
    f.detectChanges();
    expect(text()).toContain('Forklift 3t');
    const q = (f.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#md-q')!;
    q.value = 'hoist';
    q.dispatchEvent(new Event('input'));
    f.detectChanges();
    expect(text()).not.toContain('Forklift 3t');
    const add = [...(f.nativeElement as HTMLElement).querySelectorAll('.reg button')].find((b) => b.textContent?.trim() === 'Add') as HTMLButtonElement;
    add.click();
    const post = ctrl.expectOne(base + '/machines');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ equipmentId: 'e2' });
    post.flush(list([machine('a', 'not_started'), machine('n', 'not_started', { name: 'Hoist' })]), { status: 201, statusText: 'Created' });
    f.detectChanges();
    expect(text()).toContain('Hoist');
    expect((f.nativeElement as HTMLElement).querySelector('app-machine-dialog')).toBeNull();
  });

  it('adds a new machine by its details and warns that a gear is not registered', () => {
    const { f, btn, text } = open([]);
    ctrl.verify();
    btn('Add the first machine')!.click();
    f.detectChanges();
    ctrl.expectOne(base + '/available-machines').flush([]);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const set = (id: string, v: string) => {
      const i = el.querySelector<HTMLInputElement>('#' + id)!;
      i.value = v;
      i.dispatchEvent(new Event('input'));
    };
    set('md-n', '  Chain sling 2t ');
    set('md-t', 'web sling');
    f.detectChanges();
    expect(text()).toContain('web sling is a gear.');
    expect(el.querySelector('#md-s')).toBeNull();
    expect(el.querySelector('#md-l')).toBeNull();
    set('md-q2', '12');
    f.detectChanges();
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    const post = ctrl.expectOne(base + '/machines');
    expect(post.request.body).toEqual({
      name: 'Chain sling 2t', quantity: 12, type: 'web sling', capacity: null, serialNumber: null, manufacturer: null, model: null, issueDescription: null,
    });
    post.flush(list([machine('n', 'not_started', { name: 'Chain sling 2t', quantity: 12 })]), { status: 201, statusText: 'Created' });
    f.detectChanges();
    expect(text()).toContain('Chain sling 2t');
  });

  it('shows the API sentence when a machine cannot be removed', () => {
    const { f, btn, text } = open([machine('a', 'not_started'), machine('b', 'not_started')]);
    btn('Remove')!.click();
    f.detectChanges();
    btn('Remove it')!.click();
    const del = ctrl.expectOne(base + '/machines/a');
    expect(del.request.method).toBe('DELETE');
    del.flush({ code: 'machine_already_started', detail: "This machine's certificate was already started, so it cannot be removed." }, { status: 409, statusText: 'Conflict' });
    f.detectChanges();
    expect(text()).toContain("This machine's certificate was already started, so it cannot be removed.");
    expect(btn('Remove it')).toBeUndefined();
  });

  it('edits the details with PUT and shows certificate_locked as the API words it', () => {
    const { f, btn, text } = open([machine('a', 'draft', { type: 'Crane', capacity: '5 t' })]);
    btn('Edit')!.click();
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    expect(el.querySelector<HTMLInputElement>('#md-n')!.value).toBe('Crane a');
    const n = el.querySelector<HTMLInputElement>('#md-n')!;
    n.value = 'Crane A1';
    n.dispatchEvent(new Event('input'));
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    const put = ctrl.expectOne(base + '/machines/a/details');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toMatchObject({ name: 'Crane A1', quantity: 1, type: 'Crane', capacity: '5 t', serialNumber: 'SN-a' });
    put.flush({ code: 'certificate_locked', detail: 'A submitted certificate cannot be changed; wait for the review or for it to be returned.' }, { status: 409, statusText: 'Conflict' });
    f.detectChanges();
    expect(text()).toContain('A submitted certificate cannot be changed');
    expect(el.querySelector('app-machine-dialog')).not.toBeNull();
  });

  it('gives a coordinator the same actions and a way back to the queue', () => {
    role = 'coordinator';
    const { el, btn } = open([machine('a', 'not_started')], 'assigned');
    expect(btn('Add a machine')).toBeTruthy();
    expect(el.querySelector('a.back')?.getAttribute('href')).toBe('/queue');
  });
});
