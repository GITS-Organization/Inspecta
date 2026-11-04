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
  checklist: [], answers: { within6Months: true, within12Months: false, firstExamination: false, installedCorrectly: true, examinationScheme: true, exceptionalCircumstances: true, existingDanger: false, futureDanger: false }, texts: {}, dates: {}, reviewNote: null, certificateNumber: null, result: null,
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

  describe('linked answers', () => {
    const press = (el: HTMLElement, key: string, label: 'Yes' | 'No') =>
      ([...el.querySelectorAll(`[aria-labelledby="a-${key}"] button`)].find((b) => b.textContent?.trim() === label) as HTMLButtonElement).click();
    const pressed = (el: HTMLElement, key: string) => [...el.querySelectorAll(`[aria-labelledby="a-${key}"] button`)].find((b) => b.getAttribute('aria-pressed') === 'true')?.textContent?.trim();

    it('starts from the answers the API sends: none is unanswered', () => {
      const m = open('Overhead crane', 'SN-1');
      expect(pressed(m.el, 'within6Months')).toBe('Yes');
      expect(pressed(m.el, 'within12Months')).toBe('No');
      expect(pressed(m.el, 'installedCorrectly')).toBe('Yes');
      expect(pressed(m.el, 'firstExamination')).toBe('No');
      expect(pressed(m.el, 'futureDanger')).toBe('No');
      expect(m.el.querySelectorAll('.pair').length).toBe(2);
    });

    it('sets the other answer of a pair to the opposite at once, in both directions', () => {
      const m = open('Overhead crane', 'SN-1');
      press(m.el, 'within12Months', 'Yes');
      m.f.detectChanges();
      expect(pressed(m.el, 'within6Months')).toBe('No');
      press(m.el, 'within6Months', 'No');
      m.f.detectChanges();
      expect(pressed(m.el, 'within12Months')).toBe('Yes');
      press(m.el, 'firstExamination', 'Yes');
      m.f.detectChanges();
      expect(pressed(m.el, 'installedCorrectly')).toBe('No');
      press(m.el, 'installedCorrectly', 'Yes');
      m.f.detectChanges();
      expect(pressed(m.el, 'firstExamination')).toBe('No');
    });

    it('never sends a pair with the same value, and leaves the other questions alone', () => {
      const m = open('Overhead crane', 'SN-1');
      press(m.el, 'within12Months', 'Yes');
      press(m.el, 'existingDanger', 'Yes');
      m.f.detectChanges();
      const a = m.f.componentInstance.buildRequest().answers!;
      expect(a['within6Months']).toBe(false);
      expect(a['within12Months']).toBe(true);
      expect(a['installedCorrectly']).toBe(!a['firstExamination']);
      expect(a['existingDanger']).toBe(true);
      expect(a['examinationScheme']).toBe(true);
    });
  });
});
