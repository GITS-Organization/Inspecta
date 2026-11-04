import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth.service';
import { JobStatus, MachineList, MachineStatus, MachineSummary } from '../../core/models';
import { Job } from './job';
import { clampSerials, divisionTotal } from './gear-division';

const api = environment.apiBase;
const base = `${api}/quotation-queue/r1`;
const machine = (id: string, status: MachineStatus, over: Partial<MachineSummary> = {}): MachineSummary => ({
  requestItemId: id, name: 'Chain Sling grade 4', quantity: 1000, serialNumber: null, templateKey: 'gear', status, checklistTotal: 10, checklistAnswered: 0,
  reviewNote: null, certificateNumber: null, canEdit: true, canReview: false, isGear: true, ...over,
});
const list = (machines: MachineSummary[], requestStatus: JobStatus = 'in_progress'): MachineList => ({
  requestId: 'r1', requestStatus, clientName: 'Al Noor', location: 'Cairo', scheduledAt: null, progress: { total: machines.length, submitted: 0, approved: 0, delivered: 0 },
  canStartInspection: false, canDeliver: false, machines,
});

describe('gear helpers', () => {
  it('cuts the text at the limit and says it did', () => {
    expect(clampSerials('abc', 5)).toEqual({ text: 'abc', cut: false });
    expect(clampSerials('abcdefgh', 5)).toEqual({ text: 'abcde', cut: true });
    expect(clampSerials('x'.repeat(1000), 1000).cut).toBe(false);
  });
  it('compares the shares with the quantity before the division', () => {
    expect(divisionTotal([machine('a', 'not_started', { quantity: 150, divisionTotal: 1000 }), machine('b', 'not_started', { quantity: 850, divisionTotal: 1000 })])).toBe(1000);
    expect(divisionTotal([machine('a', 'not_started', { quantity: 40 })])).toBe(40);
  });
});

describe('Job: dividing a gear into certificates', () => {
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
    ctrl.match(`${api}/certifications/machine-types`).forEach((r) => r.flush([]));
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const btn = (t: string) => [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === t) as HTMLButtonElement | undefined;
    const type = (sel: string, v: string) => {
      const i = el.querySelector<HTMLInputElement | HTMLTextAreaElement>(sel)!;
      i.value = v;
      i.dispatchEvent(new Event('input'));
      f.detectChanges();
    };
    return { f, el, btn, type, text: () => el.textContent ?? '' };
  }
  const openDialog = (m: ReturnType<typeof open>) => {
    m.btn('Divide into certificates')!.click();
    m.f.detectChanges();
    ctrl.expectOne(`${api}/certifications/serial-limits`).flush({ maxCharactersPerCertificate: 1000, maxLengthPerSerialNumber: 60, maxSerialNumbers: 5000 });
    m.f.detectChanges();
  };

  it('offers the division only for a gear whose certificate was not started, while the job is under way', () => {
    expect(open([machine('a', 'not_started')]).btn('Divide into certificates')).toBeTruthy();
    for (const m of [machine('a', 'draft'), machine('a', 'not_started', { isGear: false, name: 'Crane', quantity: 1 })]) {
      const x = open([m]);
      expect(x.btn('Divide into certificates')).toBeUndefined();
      x.f.destroy();
    }
    expect(open([machine('a', 'not_started')], 'certificate_submitted').btn('Divide into certificates')).toBeUndefined();
  });

  it('opens one card per certificate, each with Qty and Serial numbers, and no maximum on the count', () => {
    const m = open([machine('a', 'not_started')]);
    openDialog(m);
    expect(m.el.querySelectorAll('fieldset.card').length).toBe(1);
    expect((m.el.querySelector('#gq0') as HTMLInputElement).value).toBe('1000');
    m.type('#gd-n', '7');
    expect(m.el.querySelectorAll('fieldset.card').length).toBe(7);
    expect(m.el.querySelector<HTMLInputElement>('#gd-n')!.max).toBe('');
    m.type('#gd-n', '120');
    expect(m.el.querySelectorAll('fieldset.card').length).toBe(120);
    m.type('#gd-n', '2');
    expect(m.el.querySelectorAll('fieldset.card').length).toBe(2);
  });

  it('counts the characters live and blocks what goes past the limit, telling the inspector', () => {
    const m = open([machine('a', 'not_started')]);
    openDialog(m);
    m.type('#gs0', 'CS-0001-CS-0150');
    expect(m.text()).toContain('15 / 1000');
    const area = m.el.querySelector<HTMLTextAreaElement>('#gs0')!;
    m.type('#gs0', 'x'.repeat(1400));
    expect(area.value.length).toBe(1000);
    expect(m.text()).toContain('1000 / 1000');
    expect(m.text()).toContain('The text was cut at 1000 characters');
    m.type('#gs0', 'short');
    expect(m.text()).not.toContain('The text was cut');
  });

  it('warns, without blocking, when the quantities do not add up to the total', () => {
    const m = open([machine('a', 'not_started')]);
    openDialog(m);
    expect(m.text()).toContain('1000 of 1000 added up.');
    expect(m.el.querySelector('.sum.diff')).toBeNull();
    m.type('#gq0', '950');
    expect(m.el.querySelector('.sum.diff')).not.toBeNull();
    expect(m.text()).toContain('950 of 1000 added up.');
    expect(m.btn('Save certificates')!.disabled).toBe(false);
  });

  it('saves the shares, trimmed, and shows the group together with the answer', () => {
    const m = open([machine('a', 'not_started')]);
    openDialog(m);
    m.type('#gd-n', '2');
    m.type('#gq0', '150');
    m.type('#gs0', '  CS-0001-CS-0150 ');
    m.type('#gq1', '850');
    m.type('#gs1', 'CS-0151-CS-1000');
    m.btn('Save certificates')!.click();
    const put = ctrl.expectOne(base + '/machines/a/certificates');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ certificates: [{ quantity: 150, serialNumbers: 'CS-0001-CS-0150' }, { quantity: 850, serialNumbers: 'CS-0151-CS-1000' }] });
    put.flush(list([
      machine('a', 'not_started', { quantity: 150, serialNumber: 'CS-0001-CS-0150', divisionGroupId: 'g1', divisionTotal: 1000 }),
      machine('b', 'not_started', { quantity: 850, serialNumber: 'CS-0151-CS-1000', divisionGroupId: 'g1', divisionTotal: 1000 }),
    ]));
    m.f.detectChanges();
    expect(m.el.querySelector('app-gear-division')).toBeNull();
    expect(m.text()).toContain('Chain Sling grade 4, 2 certificates, 1000 in all');
    expect(m.el.querySelectorAll('.mrow.share').length).toBe(2);
    expect(m.btn('Change the division')).toBeTruthy();
  });

  it('does not save a certificate without serial numbers, and shows the API refusal', () => {
    const m = open([machine('a', 'not_started')]);
    openDialog(m);
    m.btn('Save certificates')!.click();
    m.f.detectChanges();
    ctrl.expectNone(base + '/machines/a/certificates');
    expect(m.text()).toContain('Write the serial numbers of certificate 1.');
    m.type('#gs0', 'CS-1');
    m.btn('Save certificates')!.click();
    ctrl.expectOne(base + '/machines/a/certificates').flush({ code: 'machine_already_started', detail: 'A certificate of this group was already started.' }, { status: 409, statusText: 'Conflict' });
    m.f.detectChanges();
    expect(m.text()).toContain('A certificate of this group was already started.');
    expect(m.el.querySelector('app-gear-division')).not.toBeNull();
  });

  it('reopens a saved group with its shares, and locks it once a certificate was started', () => {
    const group = [
      machine('a', 'not_started', { quantity: 150, serialNumber: 'CS-1', divisionGroupId: 'g1', divisionTotal: 1000 }),
      machine('b', 'not_started', { quantity: 850, serialNumber: 'CS-2', divisionGroupId: 'g1', divisionTotal: 1000 }),
    ];
    const m = open(group);
    m.btn('Change the division')!.click();
    m.f.detectChanges();
    ctrl.expectOne(`${api}/certifications/serial-limits`).flush({ maxCharactersPerCertificate: 1000, maxLengthPerSerialNumber: 60, maxSerialNumbers: 5000 });
    expect(m.el.querySelectorAll('fieldset.card').length).toBe(2);
    expect((m.el.querySelector('#gs1') as HTMLTextAreaElement).value).toBe('CS-2');
    m.f.destroy();

    const locked = open([group[0], { ...group[1], status: 'draft' }]);
    expect(locked.btn('Change the division')).toBeUndefined();
    expect(locked.text()).toContain('the division is locked');
  });

  it('gives a coordinator the same division', () => {
    role = 'coordinator';
    expect(open([machine('a', 'not_started')], 'assigned').btn('Divide into certificates')).toBeTruthy();
  });
});
