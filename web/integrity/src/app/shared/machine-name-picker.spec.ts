import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { MachineNamePicker } from './machine-name-picker';

const api = environment.apiBase;
const types = [
  { name: 'Web Sling', aliases: ['Sling'], standards: ['BS EN 1492-1', 'LOLER 1998'], templateKey: 'gear', templateName: 'Gear', isGear: true, kind: 'gear' },
  { name: 'Container', aliases: [], standards: [], templateKey: 'basic', templateName: 'Basic', isGear: false, kind: 'non_lifting' },
  { name: 'Overhead Crane', aliases: [], standards: ['BS 7121'], templateKey: 'crane', templateName: 'Crane', isGear: false, kind: 'equipment' },
];

describe('MachineNamePicker', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  function open(value = '') {
    const f = TestBed.createComponent(MachineNamePicker);
    f.componentRef.setInput('value', value);
    f.detectChanges();
    ctrl.expectOne(`${api}/certifications/machine-types`).flush(types);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    return { f, el, text: () => el.textContent ?? '' };
  }

  it('is a select, not a text field, and searches the catalogue', () => {
    const { f, el } = open();
    expect(el.querySelector('input[type=text]')).toBeNull();
    el.querySelector<HTMLButtonElement>('button.sel')!.click();
    f.detectChanges();
    expect(el.querySelectorAll('[role=option]').length).toBe(3);
    const q = el.querySelector<HTMLInputElement>('input[type=search]')!;
    q.value = 'crane';
    q.dispatchEvent(new Event('input'));
    f.detectChanges();
    expect([...el.querySelectorAll('[role=option]')].map((o) => o.textContent?.trim().split(/\s{2,}/)[0])).toEqual(['Overhead CraneEquipment']);
  });

  it('fixes the kind and the standards under the chosen name, and says when a name has none', () => {
    const { f, el, text } = open();
    const picked: string[] = [];
    f.componentInstance.picked.subscribe((t) => picked.push(t.name));
    for (const [name, expected] of [['Web Sling', 'Gear'], ['Container', 'Non-lifting']] as const) {
      el.querySelector<HTMLButtonElement>('button.sel')!.click();
      f.detectChanges();
      ([...el.querySelectorAll('[role=option] button')].find((b) => b.textContent?.includes(name)) as HTMLButtonElement).click();
      f.detectChanges();
      expect(f.componentInstance.value()).toBe(name);
      expect(el.querySelector('dd')?.textContent?.trim()).toBe(expected);
    }
    expect(picked).toEqual(['Web Sling', 'Container']);
    expect(text()).toContain('No standard defined yet');
    expect(text()).not.toContain('Not in the list');
  });

  it('lists the standards of a name one per line', () => {
    const { el } = open('web sling');
    expect([...el.querySelectorAll('.std')].map((s) => s.textContent)).toEqual(['BS EN 1492-1', 'LOLER 1998']);
  });

  it('keeps an old name that is not in the catalogue and asks for a new one', () => {
    const { el, text } = open('Magic lifter');
    expect(el.querySelector('button.sel')!.textContent).toContain('Magic lifter');
    expect(text()).toContain('Not in the list: choose a name.');
    expect(el.querySelector('dl')).toBeNull();
  });
});
