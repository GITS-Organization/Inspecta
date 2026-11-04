import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { Client } from '../../api/coordinator.models';
import { ClientDetail } from './client-detail';
import { ClientItems } from './client-items';

const api = environment.apiBase;
const client: Client = {
  id: 'c1', name: 'Al Noor', isCompany: true, hasContract: false, location: 'Cairo', latitude: null, longitude: null, certificatesEmail: null, taxNumber: null, notes: null,
  createdAt: '2026-01-01T00:00:00Z', archivedAt: null, contacts: [{ id: 'k1', name: 'Hana', email: 'h@alnoor.com', phone: null, isPrimary: true }],
};

describe('ClientDetail', () => {
  let ctrl: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  const open = async () => {
    const f = TestBed.createComponent(ClientDetail);
    f.componentRef.setInput('clientId', 'c1');
    f.detectChanges();
    await f.whenStable();
    ctrl.expectOne(`${api}/clients/c1`).flush(client);
    f.detectChanges();
    return f;
  };

  it('saves the whole contact list, keeping the id of the contacts that stay', async () => {
    const f = await open();
    (f.componentInstance as unknown as { save(): void }).save();
    const put = ctrl.expectOne((r) => r.method === 'PUT' && r.url === `${api}/clients/c1`);
    expect(put.request.body.contacts).toEqual([{ id: 'k1', name: 'Hana', email: 'h@alnoor.com', phone: null, isPrimary: true }]);
    put.flush(client);
  });

  it('explains why a client in use cannot be deleted', async () => {
    const f = await open();
    (f.componentInstance as unknown as { remove(): void }).remove();
    ctrl.expectOne((r) => r.method === 'DELETE' && r.url === `${api}/clients/c1`).flush({ title: 'Conflict', code: 'client_in_use' }, { status: 409, statusText: 'Conflict' });
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('[role=alert]')?.textContent).toContain('still has requests or items linked to it');
  });
});

describe('ClientItems', () => {
  it('creates an item for the client and shows the sentence when delete is refused', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const ctrl = TestBed.inject(HttpTestingController);
    const f = TestBed.createComponent(ClientItems);
    f.componentRef.setInput('clientId', 'c1');
    f.detectChanges();
    const item = { id: 'e1', clientId: 'c1', name: 'Crane', itemType: 'equipment', serialNumber: 'C-1' };
    ctrl.expectOne(`${api}/equipment?clientId=c1`).flush([item]);

    type I = { startNew(): void; set(p: object): void; save(): void; remove(e: object): void };
    const c = f.componentInstance as unknown as I;
    c.startNew();
    c.set({ name: 'Hoist', serialNumber: 'H-9' });
    c.save();
    const post = ctrl.expectOne((r) => r.method === 'POST' && r.url === `${api}/equipment`);
    expect(post.request.body).toEqual({ clientId: 'c1', name: 'Hoist', itemType: 'equipment', serialNumber: 'H-9' });
    post.flush({});
    ctrl.expectOne(`${api}/equipment?clientId=c1`).flush([item]);

    c.remove(item);
    ctrl.expectOne((r) => r.method === 'DELETE' && r.url === `${api}/equipment/e1`).flush({ code: 'equipment_in_use' }, { status: 409, statusText: 'Conflict' });
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('[role=alert]')?.textContent).toContain('cannot be deleted');
    ctrl.verify();
  });
});
