import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { MachineForm } from '../../core/models';
import { MachineFormPage, roomFor } from './machine-form';

const api = environment.apiBase;
const form = (item: string, serial: string): MachineForm => ({
  requestId: 'r1', requestItemId: 'i1', requestStatus: 'in_progress', status: 'draft', canEdit: true, canReview: false, readyToSubmit: false, missing: [],
  templateKey: 'basic', templateName: 'Basic',
  header: { clientName: 'Al Noor', clientAddress: null, clientRepresentative: null, inspectedItem: item, standards: [], lastExaminationDate: null, nextExaminationDue: null, inspectorName: 'Omar' },
  fields: [{ key: 'serialNumber', label: 'Serial numbers', value: serial }],
  checklist: [], answers: {}, texts: {}, dates: {}, reviewNote: null, certificateNumber: null, result: null,
});

describe('roomFor', () => {
  it('is the budget minus the rest, never negative', () => {
    expect(roomFor(1200, 300)).toBe(900);
    expect(roomFor(1200, 1500)).toBe(0);
  });
});

describe('Machine form: one page for a gear', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  function open(item: string, serial: string, budget = 1200) {
    const f = TestBed.createComponent(MachineFormPage);
    f.componentRef.setInput('requestId', 'r1');
    f.componentRef.setInput('itemId', 'i1');
    f.detectChanges();
    ctrl.expectOne(`${api}/quotation-queue/r1/machines/i1`).flush(form(item, serial));
    ctrl.match(`${api}/certifications/machine-types`).forEach((r) => r.flush([{ name: 'Web sling', aliases: [], standards: [], templateKey: 'gear', templateName: 'Gear', isGear: true }]));
    ctrl.expectOne(`${api}/certifications/serial-limits`).flush({ maxCharactersPerCertificate: 1000, maxLengthPerSerialNumber: 60, maxSerialNumbers: 5000, pageBudgetCharacters: budget });
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const type = (id: string, v: string) => {
      const i = el.querySelector<HTMLInputElement | HTMLTextAreaElement>('#' + id)!;
      i.value = v;
      i.dispatchEvent(new Event('input'));
      f.detectChanges();
      return i;
    };
    return { f, el, type, text: () => el.textContent ?? '' };
  }

  it('shows the serial text and the four texts as one counter against the budget', () => {
    const m = open('Web sling', 'x'.repeat(300));
    expect(m.text()).toContain('300 / 1200');
    m.type('t-defectDescription', 'y'.repeat(100));
    m.type('t-note', 'z'.repeat(50));
    expect(m.text()).toContain('450 / 1200');
    m.type('t-conclusion', 'not counted '.repeat(40));
    expect(m.text()).toContain('450 / 1200');
  });

  it('blocks typing past the budget and says why', () => {
    const m = open('Web sling', 'x'.repeat(1000));
    const area = m.type('t-defectDescription', 'a'.repeat(500));
    expect(area.value.length).toBe(200);
    expect(m.text()).toContain('1200 / 1200');
    expect(m.text()).toContain('The text was cut');
    const other = m.type('t-repairsRequired', 'b');
    expect(other.value).toBe('');
    m.type('t-defectDescription', 'a'.repeat(10));
    expect(m.text()).not.toContain('The text was cut');
  });

  it('does not limit or count a machine that is not a gear', () => {
    const m = open('Overhead crane', 'SN-1');
    expect(m.text()).not.toContain('/ 1200');
    const area = m.type('t-defectDescription', 'a'.repeat(3000));
    expect(area.value.length).toBe(3000);
  });
});
