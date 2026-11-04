import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { MachineList, MachineSummary } from '../../core/models';
import { Deliver } from './deliver';

const api = environment.apiBase;
const base = `${api}/quotation-queue/r1`;
const today = new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 10);
const machine = (id: string, number: string, date: string | null, over: Partial<MachineSummary> = {}): MachineSummary => ({
  requestItemId: id, name: 'Crane ' + id, quantity: 1, serialNumber: null, templateKey: 'crane', status: 'approved', checklistTotal: 10, checklistAnswered: 10, reviewNote: null,
  certificateNumber: number, certificateDate: date, canEdit: false, canReview: false, pdfUrl: `/api/v1/quotation-queue/r1/machines/${id}/pdf`, ...over,
});
const job = (machines: MachineSummary[], editable = true, status: MachineList['requestStatus'] = 'certificate_approved'): MachineList => ({
  requestId: 'r1', requestStatus: status, clientName: 'Al Noor', location: 'Cairo', scheduledAt: null, progress: { total: machines.length, submitted: 0, approved: 0, delivered: 0 },
  canStartInspection: false, canDeliver: true, machines, canEditCertificateDetails: editable,
});
const preview = {
  emailEnabled: true, to: 'a@b.c', toName: null, subject: 'Certificates', body: 'Hello', attachments: ['a.pdf'], repliesToOriginalEmail: false, notice: null, canSend: true, alreadyDelivered: false,
  certificates: [{ requestItemId: 'a', itemName: 'Crane a', certificateNumber: 'CERT-1', result: 'satisfactory', examinationDate: '2026-10-01', nextExaminationDate: '2027-10-01', fileName: 'a.pdf', sizeBytes: 1, pdfUrl: '/x', verifyUrl: null }],
};

describe('Deliver: certificate number and date', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  function open(machines: MachineSummary[], editable = true, status: MachineList['requestStatus'] = 'certificate_approved') {
    const f = TestBed.createComponent(Deliver);
    f.componentRef.setInput('requestId', 'r1');
    f.detectChanges();
    ctrl.expectOne(base + '/machines').flush(job(machines, editable, status));
    ctrl.expectOne(base + '/deliver-to-client/preview').flush(preview);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const btn = (t: string) => [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === t) as HTMLButtonElement | undefined;
    const type = (sel: string, v: string) => {
      const i = el.querySelector<HTMLInputElement>(sel)!;
      i.value = v;
      i.dispatchEvent(new Event('input'));
      f.detectChanges();
    };
    return { f, el, btn, type, text: () => el.textContent ?? '' };
  }

  it('prefills each certificate from the machine and shows nothing as changed', () => {
    const m = open([machine('a', 'CERT-2026-0008', '2026-10-30'), machine('b', 'CERT-2026-0009', '2026-10-30')]);
    expect(m.el.querySelector<HTMLInputElement>('#cna')!.value).toBe('CERT-2026-0008');
    expect(m.el.querySelector<HTMLInputElement>('#cda')!.value).toBe('2026-10-30');
    expect(m.el.querySelector<HTMLInputElement>('#cdb')!.min).toBe('2020-01-01');
    expect(m.el.querySelector<HTMLInputElement>('#cdb')!.max).toBe(today);
    expect(m.text()).toContain('Printed at the top of each certificate. The examination date is not changed.');
    expect(m.el.querySelector('.mark')).toBeNull();
    expect(m.btn('Save certificate details')!.disabled).toBe(true);
  });

  it('marks what changed and saves only the changed certificates, trimmed, in one call', () => {
    const m = open([machine('a', 'CERT-1', '2026-10-30'), machine('b', 'CERT-2', '2026-10-30')]);
    m.type('#cna', '  CERT-2026-0100 ');
    expect(m.el.querySelectorAll('.mark').length).toBe(1);
    expect(m.text()).toContain('1 certificate changed');
    m.type('#cdb', '2026-10-28');
    expect(m.el.querySelectorAll('.mark').length).toBe(2);
    m.btn('Save certificate details')!.click();
    const put = ctrl.expectOne(base + '/certificate-details');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({
      certificates: [
        { requestItemId: 'a', certificateNumber: 'CERT-2026-0100', certificateDate: '2026-10-30' },
        { requestItemId: 'b', certificateNumber: 'CERT-2', certificateDate: '2026-10-28' },
      ],
    });
    put.flush(job([machine('a', 'CERT-2026-0100', '2026-10-30'), machine('b', 'CERT-2', '2026-10-28')]));
    ctrl.expectOne(base + '/deliver-to-client/preview').flush(preview);
    m.f.detectChanges();
    expect(m.el.querySelector('.mark')).toBeNull();
    expect(m.el.querySelector<HTMLInputElement>('#cna')!.value).toBe('CERT-2026-0100');
    expect(m.btn('Save certificate details')!.disabled).toBe(true);
  });

  it('refuses an empty number, a repeated number and a date outside 2020 to today before calling the API', () => {
    const m = open([machine('a', 'CERT-1', '2026-10-30'), machine('b', 'CERT-2', '2026-10-30')]);
    m.type('#cna', '   ');
    expect(m.text()).toContain('Write the certificate number.');
    expect(m.btn('Save certificate details')!.disabled).toBe(true);
    m.type('#cna', 'cert-2');
    expect(m.text()).toContain('Another certificate of this job has the same number.');
    expect(m.btn('Save certificate details')!.disabled).toBe(true);
    m.type('#cna', 'CERT-3');
    m.type('#cdb', '2019-12-31');
    expect(m.text()).toContain('The date cannot be before 2020.');
    m.type('#cdb', '');
    expect(m.text()).toContain('Choose the certificate date.');
    m.type('#cdb', '2999-01-01');
    expect(m.text()).toContain('The date cannot be in the future.');
    expect(m.btn('Save certificate details')!.disabled).toBe(true);
    ctrl.expectNone(base + '/certificate-details');
  });

  it('shows the API sentence when the number is taken, and keeps what was typed', () => {
    const m = open([machine('a', 'CERT-1', '2026-10-30')]);
    m.type('#cna', 'CERT-9');
    m.btn('Save certificate details')!.click();
    ctrl.expectOne(base + '/certificate-details').flush({ code: 'certificate_number_taken', detail: 'The number CERT-9 is already used by another certificate.' }, { status: 409, statusText: 'Conflict' });
    m.f.detectChanges();
    expect(m.text()).toContain('The number CERT-9 is already used by another certificate.');
    expect(m.el.querySelector<HTMLInputElement>('#cna')!.value).toBe('CERT-9');
    expect(m.btn('Save certificate details')!.disabled).toBe(false);
  });

  it('is read-only with a hint once the job was handed over, or when the API says it is not editable', () => {
    const delivered = open([machine('a', 'CERT-1', '2026-10-30')], false, 'sent_to_client');
    expect(delivered.el.querySelector<HTMLInputElement>('#cna')!.readOnly).toBe(true);
    expect(delivered.el.querySelector<HTMLInputElement>('#cda')!.readOnly).toBe(true);
    expect(delivered.text()).toContain('Already handed over');
    expect(delivered.btn('Save certificate details')).toBeUndefined();
    delivered.f.destroy();
    const locked = open([machine('a', 'CERT-1', '2026-10-30')], false);
    expect(locked.el.querySelector<HTMLInputElement>('#cna')!.readOnly).toBe(true);
    expect(locked.btn('Save certificate details')).toBeUndefined();
  });

  it('warns before delivering with unsaved changes, and can save first or deliver anyway', () => {
    const nav = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const m = open([machine('a', 'CERT-1', '2026-10-30')]);
    m.type('#cna', 'CERT-7');
    m.btn('Mark as delivered')!.click();
    m.f.detectChanges();
    ctrl.expectNone(base + '/deliver-to-client/manual');
    expect(m.text()).toContain('changes that are not saved');
    m.btn('Deliver without saving')!.click();
    ctrl.expectOne(base + '/deliver-to-client/manual').flush(job([machine('a', 'CERT-1', '2026-10-30')], false, 'sent_to_client'));
    expect(nav).toHaveBeenCalledWith(['/queue']);
  });

  it('saves first when asked, then delivers', () => {
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const m = open([machine('a', 'CERT-1', '2026-10-30')]);
    m.type('#cna', 'CERT-7');
    m.btn('Mark as delivered')!.click();
    m.f.detectChanges();
    m.btn('Save and deliver')!.click();
    ctrl.expectOne(base + '/certificate-details').flush(job([machine('a', 'CERT-7', '2026-10-30')]));
    ctrl.expectOne(base + '/deliver-to-client/preview').flush(preview);
    ctrl.expectOne(base + '/deliver-to-client/manual').flush(job([machine('a', 'CERT-7', '2026-10-30')], false, 'sent_to_client'));
  });

  it('delivers at once when nothing is unsaved, and Preview asks for the PDF again each time', () => {
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(window, 'open').mockReturnValue(null);
    const m = open([machine('a', 'CERT-1', '2026-10-30')]);
    m.btn('Preview')!.click();
    ctrl.expectOne(base + '/machines/a/pdf').flush(new Blob(['%PDF']));
    m.btn('Preview')!.click();
    ctrl.expectOne(base + '/machines/a/pdf').flush(new Blob(['%PDF']));
    m.btn('Mark as delivered')!.click();
    ctrl.expectOne(base + '/deliver-to-client/manual').flush(job([machine('a', 'CERT-1', '2026-10-30')], false, 'sent_to_client'));
  });
});
