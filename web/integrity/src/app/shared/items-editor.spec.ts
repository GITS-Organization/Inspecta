import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { SaveRequestItem } from '../api/coordinator.models';
import { ItemsEditor } from './items-editor';

const api = environment.apiBase;
const types = [{ name: 'Web sling', aliases: ['Sling'], standards: [], templateKey: 'gear', templateName: 'Gear', isGear: true }];

describe('ItemsEditor field rules', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  function open(items: SaveRequestItem[]) {
    const f = TestBed.createComponent(ItemsEditor);
    f.componentRef.setInput('items', items);
    f.detectChanges();
    ctrl.expectOne(`${api}/certifications/machine-types`).flush(types);
    f.detectChanges();
    return { f, el: f.nativeElement as HTMLElement };
  }

  it('shows Quantity for a gear only, Serial number for any other machine, and never "Where on site"', () => {
    const { el } = open([{ name: 'Web sling', quantity: 400 }, { name: 'Overhead crane', quantity: 1 }]);
    const cards = Array.from(el.querySelectorAll('.itemcard'));
    expect(cards[0].querySelector('#q0')).not.toBeNull();
    expect(cards[0].querySelector('#s0')).toBeNull();
    expect(cards[1].querySelector('#q1')).toBeNull();
    expect(cards[1].querySelector('#s1')).not.toBeNull();
    expect(el.textContent).not.toContain('Where on site');
  });

  it('turns a machine that is not a gear back to one unit and drops the serial of a gear', () => {
    const { f, el } = open([{ name: 'Web sling', quantity: 400, serialNumber: 'X-1' }]);
    const c = f.componentInstance as unknown as { set(i: number, p: Partial<SaveRequestItem>): void };
    c.set(0, { quantity: 400 });
    expect(f.componentInstance.items()[0].serialNumber).toBeNull();
    c.set(0, { name: 'Overhead crane' });
    f.detectChanges();
    expect(f.componentInstance.items()[0].quantity).toBe(1);
    expect(el.querySelector('#s0')).not.toBeNull();
  });
});
