import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { MailboxConfig } from '../../api/manager.api';
import { AuthService } from '../../core/auth.service';
import { AuthUser } from '../../core/models';
import { Mailbox, SYNC_MIN, syncMax, syncProblem } from './mailbox';

const api = environment.apiBase;
const user = (id: string, role: AuthUser['role'], over: Partial<AuthUser> = {}): AuthUser => ({ id, email: id + '@x.com', fullName: 'User ' + id, role, isActive: true, managerId: 'm1', ...over });
const config = (over: Partial<MailboxConfig> = {}): MailboxConfig => ({
  configured: true, imapServer: 'imap.x.com', imapPort: 993, emailUsername: 'requests@x.com', useSsl: true, smtpServer: null, smtpPort: null, smtpUseSsl: null,
  syncFrom: '2026-11-01T00:00:00Z', updatedAt: null, ...over,
});

describe('date rules', () => {
  const now = new Date(2026, 10, 4);
  it('accepts 2020 up to 30 days ahead and says what is wrong otherwise', () => {
    expect(syncMax(now)).toBe('2026-12-04');
    expect(syncProblem('2026-11-01', now)).toBe('');
    expect(syncProblem(SYNC_MIN, now)).toBe('');
    expect(syncProblem('2026-12-04', now)).toBe('');
    expect(syncProblem('2019-12-31', now)).toContain('before 1 January 2020');
    expect(syncProblem('2026-12-05', now)).toContain('30 days ahead');
    expect(syncProblem('', now)).toBe('Choose a date.');
  });
});

describe('Mailbox', () => {
  let ctrl: HttpTestingController;
  let role = 'technical_manager';
  beforeEach(() => {
    role = 'technical_manager';
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { role: () => role } }] });
    ctrl = TestBed.inject(HttpTestingController);
  });
  afterEach(() => ctrl.verify());

  function open() {
    const f = TestBed.createComponent(Mailbox);
    f.detectChanges();
    ctrl.expectOne(`${api}/manager/team`).flush([user('c1', 'coordinator'), user('i1', 'inspector'), user('c2', 'coordinator', { isActive: false })]);
    f.detectChanges();
    ctrl.expectOne(`${api}/manager/team/c1/email-config`).flush(config());
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    const btn = (t: string) => [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === t) as HTMLButtonElement | undefined;
    return { f, el, btn, text: () => el.textContent ?? '' };
  }

  it('lists the active coordinators with the date mail is imported from, and never a password', () => {
    const { text, el } = open();
    expect(text()).toContain('User c1');
    expect(text()).not.toContain('User i1');
    expect(text()).not.toContain('User c2');
    expect(text()).toContain('1 Nov 2026');
    expect(text()).toContain('requests@x.com · imap.x.com:993 (SSL)');
    expect(el.querySelector('input[type=password]')).toBeNull();
  });

  it('explains the change, asks for confirmation, then saves the date as midnight UTC', () => {
    const { f, el, btn, text } = open();
    btn('Change the date')!.click();
    f.detectChanges();
    expect(text()).toContain('start again from the new date');
    expect(text()).toContain('never deletes');
    const input = el.querySelector<HTMLInputElement>('input[type=date]')!;
    expect(input.min).toBe('2020-01-01');
    expect(input.max).toBe(syncMax());
    input.value = '2026-10-15';
    input.dispatchEvent(new Event('input'));
    f.detectChanges();
    btn('Review the change')!.click();
    f.detectChanges();
    ctrl.expectNone((r) => r.method === 'PUT');
    expect(text()).toContain('from 15 Oct 2026 instead of 1 Nov 2026');
    expect(text()).toContain('Older mail will be fetched');
    btn('Yes, change the date')!.click();
    const put = ctrl.expectOne(`${api}/manager/team/c1/email-config/sync-from`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ syncFrom: '2026-10-15T00:00:00Z' });
    put.flush(config({ syncFrom: '2026-10-15T00:00:00Z' }));
    f.detectChanges();
    expect(text()).toContain('15 Oct 2026');
    expect(el.querySelector('input[type=date]')).toBeNull();
  });

  it('does not let an out-of-range date through, and shows the API refusal', () => {
    const { f, el, btn, text } = open();
    btn('Change the date')!.click();
    f.detectChanges();
    const input = el.querySelector<HTMLInputElement>('input[type=date]')!;
    input.value = '2019-05-01';
    input.dispatchEvent(new Event('input'));
    f.detectChanges();
    expect(btn('Review the change')!.disabled).toBe(true);

    input.value = '2026-09-01';
    input.dispatchEvent(new Event('input'));
    f.detectChanges();
    btn('Review the change')!.click();
    f.detectChanges();
    btn('Yes, change the date')!.click();
    ctrl.expectOne(`${api}/manager/team/c1/email-config/sync-from`).flush({ title: 'Bad Request', detail: 'The date is too far ahead.' }, { status: 400, statusText: 'Bad Request' });
    f.detectChanges();
    expect(text()).toContain('The date is too far ahead.');
    expect(btn('Yes, change the date')).toBeUndefined();
  });

  it('asks an admin for the team and passes managerId on every call', () => {
    role = 'admin';
    const f = TestBed.createComponent(Mailbox);
    f.detectChanges();
    ctrl.expectNone(`${api}/manager/team`);
    const el = f.nativeElement as HTMLElement;
    const i = el.querySelector<HTMLInputElement>('#mgr')!;
    i.value = 'm9';
    i.dispatchEvent(new Event('change'));
    ctrl.expectOne(`${api}/manager/team?managerId=m9`).flush([user('c1', 'coordinator')]);
    f.detectChanges();
    ctrl.expectOne(`${api}/manager/team/c1/email-config?managerId=m9`).flush(config({ configured: false, imapServer: null, imapPort: null, emailUsername: null }));
    f.detectChanges();
    expect(el.textContent).toContain('No mailbox set yet');
  });
});
