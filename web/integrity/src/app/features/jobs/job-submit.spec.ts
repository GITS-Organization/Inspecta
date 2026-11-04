import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { fileNameOf } from '../../api/backend.api';
import { AuthService } from '../../core/auth.service';
import { JobStatus, MachineList, MachineStatus, MachineSummary } from '../../core/models';
import { Job } from './job';

const api = environment.apiBase;
const base = `${api}/quotation-queue/r1`;
const machine = (id: string, name: string, status: MachineStatus, over: Partial<MachineSummary> = {}): MachineSummary => ({
  requestItemId: id, name, quantity: 1, serialNumber: null, templateKey: 'crane', status, checklistTotal: 10, checklistAnswered: 0, reviewNote: null, certificateNumber: null,
  canEdit: true, canReview: false, isGear: false, ...over,
});
const list = (machines: MachineSummary[], requestStatus: JobStatus = 'in_progress'): MachineList => ({
  requestId: 'r1', requestStatus, clientName: 'Al Noor', location: 'Cairo', scheduledAt: null, progress: { total: machines.length, submitted: 0, approved: 0, delivered: 0 },
  canStartInspection: false, canDeliver: false, machines,
});

describe('fileNameOf', () => {
  it('reads the name of a Content-Disposition header', () => {
    expect(fileNameOf('attachment; filename="previews-2026.zip"')).toBe('previews-2026.zip');
    expect(fileNameOf("attachment; filename*=UTF-8''pre%20view.zip")).toBe('pre view.zip');
    expect(fileNameOf(null)).toBeNull();
    expect(fileNameOf('inline')).toBeNull();
  });
});

describe('Job: submit all, preview, add a certificate', () => {
  let ctrl: HttpTestingController;
  let role = 'inspector';
  beforeEach(() => {
    role = 'inspector';
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { role: () => role } }] });
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
    return { f, el, btn, text: () => el.textContent ?? '' };
  }

  it('shows "Submit certificates" only to the inspector while the job is in progress or returned', () => {
    expect(open([machine('a', 'Crane', 'draft')]).btn('Submit certificates')).toBeTruthy();
    expect(open([machine('a', 'Crane', 'returned')], 'certificate_returned').btn('Submit certificates')).toBeTruthy();
    for (const s of ['assigned', 'certificate_submitted', 'certificate_approved'] as JobStatus[]) expect(open([machine('a', 'Crane', 'draft')], s).btn('Submit certificates')).toBeUndefined();
    role = 'technical_manager';
    expect(open([machine('a', 'Crane', 'draft')]).btn('Submit certificates')).toBeUndefined();
    role = 'inspector';
    expect(open([], 'in_progress').btn('Submit certificates')).toBeUndefined();
  });

  it('asks for confirmation, then sends the whole job with one call and shows the new status', () => {
    const m = open([machine('a', 'Crane', 'draft'), machine('b', 'Forklift', 'draft')]);
    m.btn('Submit certificates')!.click();
    m.f.detectChanges();
    ctrl.expectNone(base + '/submit-all');
    expect(m.text()).toContain('2 certificates will go to your technical manager.');
    [...m.el.querySelectorAll('[role=alertdialog] button')].find((b) => b.textContent?.trim() === 'Submit certificates')!.dispatchEvent(new Event('click'));
    const post = ctrl.expectOne(base + '/submit-all');
    expect(post.request.method).toBe('POST');
    post.flush(list([machine('a', 'Crane', 'submitted'), machine('b', 'Forklift', 'submitted')], 'certificate_submitted'));
    m.f.detectChanges();
    expect(m.btn('Submit certificates')).toBeUndefined();
    expect(m.el.querySelector('[role=alertdialog]')).toBeNull();
  });

  it('lists the problems of a refused submission, says nothing was sent, and links each to its machine', () => {
    const m = open([machine('a', 'Chain Sling', 'draft', { serialNumber: 'CS-1' }), machine('b', 'Forklift Truck', 'draft')]);
    m.btn('Submit certificates')!.click();
    m.f.detectChanges();
    [...m.el.querySelectorAll('[role=alertdialog] button')].find((b) => b.textContent?.trim() === 'Submit certificates')!.dispatchEvent(new Event('click'));
    ctrl.expectOne(base + '/submit-all').flush(
      { code: 'certificate_incomplete', detail: 'Nothing was submitted.', problems: ['Chain Sling (CS-1...): the certificate is not filled in', 'Forklift Truck: the serial number is missing'] },
      { status: 409, statusText: 'Conflict' },
    );
    m.f.detectChanges();
    const items = [...m.el.querySelectorAll('.problems li')];
    expect(m.text()).toContain('Nothing was submitted.');
    expect(items.map((i) => i.querySelector('.lnk')?.textContent)).toEqual(['Chain Sling (CS-1...): the certificate is not filled in', 'Forklift Truck: the serial number is missing']);
    expect(items[1].querySelector('a.open')?.getAttribute('href')).toBe('/jobs/r1/machines/b');
    (items[1].querySelector('.lnk') as HTMLElement).click();
    m.f.detectChanges();
    expect(m.el.querySelector('#m-b')?.classList.contains('flash')).toBe(true);
  });

  it('asks the API at once when a certificate is not saved, so it explains what is missing', () => {
    const m = open([machine('a', 'Crane', 'not_started'), machine('b', 'Forklift', 'draft')]);
    expect(m.text()).toContain('1 certificate not saved yet.');
    expect(m.btn('Submit certificates')!.disabled).toBe(false);
    m.btn('Submit certificates')!.click();
    ctrl.expectOne(base + '/submit-all').flush({ code: 'certificate_incomplete', problems: ['Crane: the certificate is not filled in'] }, { status: 409, statusText: 'Conflict' });
    m.f.detectChanges();
    expect(m.text()).toContain('Crane: the certificate is not filled in');
  });

  it('shows the API sentence for the other 409 refusals', () => {
    const m = open([machine('a', 'Crane', 'draft')]);
    m.btn('Submit certificates')!.click();
    m.f.detectChanges();
    [...m.el.querySelectorAll('[role=alertdialog] button')].find((b) => b.textContent?.trim() === 'Submit certificates')!.dispatchEvent(new Event('click'));
    ctrl.expectOne(base + '/submit-all').flush({ code: 'inspection_not_started', detail: 'Start the inspection first.' }, { status: 409, statusText: 'Conflict' });
    m.f.detectChanges();
    expect(m.text()).toContain('Start the inspection first.');
    expect(m.el.querySelector('.problems')).toBeNull();
  });

  it('previews a saved certificate: fetched with HttpClient as a blob, shown in a viewer marked DRAFT, and released on close', () => {
    const created: string[] = [];
    const revoked: string[] = [];
    vi.spyOn(URL, 'createObjectURL').mockImplementation(() => {
      created.push('blob:x');
      return 'blob:x';
    });
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation((u) => void revoked.push(u));
    const m = open([machine('a', 'Crane', 'draft', { pdfUrl: '/api/v1/quotation-queue/r1/machines/a/pdf' }), machine('b', 'Forklift', 'not_started')]);
    const previews = [...m.el.querySelectorAll('button')].filter((b) => b.textContent?.trim() === 'Preview');
    expect(previews.map((b) => b.disabled)).toEqual([false, true]);
    expect(previews[1].getAttribute('title')).toBe('Fill the certificate first');
    previews[0].click();
    const get = ctrl.expectOne(base + '/machines/a/pdf');
    expect(get.request.responseType).toBe('blob');
    get.flush(new Blob(['%PDF'], { type: 'application/pdf' }));
    m.f.detectChanges();
    expect(created.length).toBe(1);
    expect(m.el.querySelector('iframe')).not.toBeNull();
    expect(m.text()).toContain('marked DRAFT');
    m.btn('Close')!.click();
    m.f.detectChanges();
    expect(revoked).toEqual(['blob:x']);
    expect(m.el.querySelector('iframe')).toBeNull();
  });

  it('shows the API message when a preview is refused (the error body arrives as a blob)', async () => {
    const m = open([machine('a', 'Crane', 'draft', { pdfUrl: '/api/v1/quotation-queue/r1/machines/a/pdf' })]);
    m.btn('Preview')!.click();
    ctrl.expectOne(base + '/machines/a/pdf').flush(new Blob([JSON.stringify({ detail: 'The certificate is not ready.' })], { type: 'application/json' }), { status: 409, statusText: 'Conflict' });
    await new Promise((r) => setTimeout(r, 20));
    m.f.detectChanges();
    expect(m.text()).toContain('The certificate is not ready.');
  });

  it('downloads every preview in one ZIP, named by the API', async () => {
    const saved: string[] = [];
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:z');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      saved.push(this.download);
    });
    const m = open([machine('a', 'Crane', 'draft')]);
    m.btn('Preview all (ZIP)')!.click();
    ctrl.expectOne(base + '/certificates/preview.zip').flush(new Blob(['PK']), { headers: { 'Content-Disposition': 'attachment; filename="al-noor-previews.zip"' } });
    expect(saved).toEqual(['al-noor-previews.zip']);

    m.btn('Preview all (ZIP)')!.click();
    ctrl.expectOne(base + '/certificates/preview.zip').flush(new Blob(['PK']));
    expect(saved[1]).toBe('certificates-preview.zip');

    m.btn('Preview all (ZIP)')!.click();
    ctrl.expectOne(base + '/certificates/preview.zip').flush(new Blob([JSON.stringify({ detail: 'No certificate was saved yet.' })]), { status: 404, statusText: 'Not Found' });
    await new Promise((r) => setTimeout(r, 20));
    m.f.detectChanges();
    expect(m.text()).toContain('No certificate was saved yet.');
  });

  it('always offers "Add a certificate" on an open job, and lights the new row after adding', () => {
    vi.useFakeTimers();
    try {
      for (const s of ['assigned', 'task_accepted', 'in_progress', 'certificate_returned'] as JobStatus[]) expect(open([machine('a', 'Crane', 'draft')], s).btn('Add a certificate')).toBeTruthy();
      const m = open([machine('a', 'Crane', 'draft')]);
      m.btn('Add a certificate')!.click();
      m.f.detectChanges();
      expect(m.text()).toContain('Add a certificate');
      ctrl.expectOne(base + '/available-machines').flush([{ id: 'e1', clientId: 'c1', name: 'Hoist', serialNumber: null, model: null, capacity: null, category: null, manufacturer: null }]);
      m.f.detectChanges();
      ([...m.el.querySelectorAll('.reg button')].find((b) => b.textContent?.trim() === 'Add') as HTMLButtonElement).click();
      ctrl.expectOne(base + '/machines').flush(list([machine('a', 'Crane', 'draft'), machine('n', 'Hoist', 'not_started')]), { status: 201, statusText: 'Created' });
      m.f.detectChanges();
      expect(m.el.querySelector('#m-n')?.classList.contains('flash')).toBe(true);
      expect(m.el.querySelector('#m-a')?.classList.contains('flash')).toBe(false);
      vi.advanceTimersByTime(3000);
      m.f.detectChanges();
      expect(m.el.querySelector('#m-n')?.classList.contains('flash')).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });
});
