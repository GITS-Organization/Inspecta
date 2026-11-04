import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { CERTIFICATION_API, HttpCertificationApi } from '../../api/certification.api';
import { DirectCertification, DirectOutcome, DirectPlan } from './direct-certification';

const api = environment.apiBase;
const job = (ids: string[]) => ({ requestId: 'r1', machines: ids.map((id) => ({ requestItemId: id, name: 'Chain sling' })) });

const gearPlan: DirectPlan = {
  clientId: 'c1',
  machine: { name: 'Chain sling', type: 'Chain sling', isGear: true },
  serials: ['A1', 'A2', 'A3'],
  certificates: 2,
  examinedOn: '2026-11-04',
  nextExaminationOn: '2027-11-04',
  result: 'satisfactory',
  submit: true,
};

describe('DirectCertification', () => {
  let ctrl: HttpTestingController;
  let flow: DirectCertification;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: CERTIFICATION_API, useExisting: HttpCertificationApi }],
    });
    ctrl = TestBed.inject(HttpTestingController);
    flow = TestBed.inject(DirectCertification);
  });
  afterEach(() => ctrl.verify());

  it('creates the job, splits the serials, then saves and submits every certificate', () => {
    let out: DirectOutcome | undefined;
    flow.run(gearPlan).subscribe((o) => (out = o));

    const create = ctrl.expectOne(`${api}/certifications`);
    expect(create.request.body).toEqual({ clientId: 'c1', machines: [{ name: 'Chain sling', quantity: 3, type: 'Chain sling' }] });
    create.flush(job(['i1']));

    const split = ctrl.expectOne(`${api}/quotation-queue/r1/machines/i1/split`);
    expect(split.request.body).toEqual({ serialNumbers: ['A1', 'A2', 'A3'], certificates: 2 });
    split.flush(job(['i1', 'i2']));

    for (const id of ['i1', 'i2']) {
      const put = ctrl.expectOne((r) => r.method === 'PUT' && r.url === `${api}/quotation-queue/r1/machines/${id}`);
      expect(put.request.body).toEqual({ dates: { examination: '2026-11-04', nextExamination: '2027-11-04' }, result: 'satisfactory' });
      put.flush({});
      ctrl.expectOne(`${api}/quotation-queue/r1/machines/${id}/submit`).flush({});
    }
    expect(out?.submitted).toBe(2);
    expect(out?.pending).toEqual([]);
  });

  it('registers an existing standard machine by id, sends no split, and keeps a certificate pending when submit is refused', () => {
    let out: DirectOutcome | undefined;
    flow
      .run({ ...gearPlan, machine: { name: 'Air compressor', equipmentId: 'e1', isGear: false }, serials: [], certificates: 1 })
      .subscribe((o) => (out = o));

    const create = ctrl.expectOne(`${api}/certifications`);
    expect(create.request.body.machines[0]).toEqual({ name: 'Air compressor', quantity: 1, equipmentId: 'e1' });
    create.flush(job(['i1']));

    ctrl.expectOne((r) => r.method === 'PUT').flush({});
    ctrl.expectOne(`${api}/quotation-queue/r1/machines/i1/submit`).flush({ title: 'The checklist is not complete.' }, { status: 409, statusText: 'Conflict' });

    expect(out?.submitted).toBe(0);
    expect(out?.pending).toEqual([{ requestItemId: 'i1', name: 'Chain sling', reason: 'The checklist is not complete.' }]);
  });

  it('only saves, and does not submit, for a draft', () => {
    let out: DirectOutcome | undefined;
    flow.run({ ...gearPlan, serials: [], machine: { name: 'Air compressor', isGear: false }, submit: false }).subscribe((o) => (out = o));
    ctrl.expectOne(`${api}/certifications`).flush(job(['i1']));
    ctrl.expectOne((r) => r.method === 'PUT').flush({});
    expect(out?.submitted).toBe(0);
    expect(out?.pending).toEqual([]);
  });
});
