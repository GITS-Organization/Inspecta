import { AuthResult, Role } from './models';

/** A JWT-shaped string carrying the `role` claim (unsigned: the UI only reads it). */
export function fakeJwt(role: string): string {
  const enc = (o: object) => btoa(JSON.stringify(o)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${enc({ alg: 'none' })}.${enc({ sub: 'u1', role })}.sig`;
}

export function fakeAuth(role: Role, token = 'access-1', refresh = 'refresh-1'): AuthResult {
  return {
    accessToken: fakeJwt(role) + '.' + token,
    refreshToken: refresh,
    expiresAt: '2030-01-01T00:00:00Z',
    user: { id: 'u1', email: 'a@b.c', fullName: 'Test User', role, isActive: true, managerId: null },
  };
}
