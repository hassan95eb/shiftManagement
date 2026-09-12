import { clearSession, readSession, writeSession } from './auth-storage';
import type { AuthSession } from '../types/auth';

const session: AuthSession = {
  accessToken: 'token',
  expiresAtUtc: '2035-01-01T00:00:00Z',
  tokenType: 'Bearer',
  userId: 1,
  role: 'Employer',
  employerId: 2,
  expertId: null,
  username: 'employer',
};

describe('auth storage', () => {
  it('round-trips a valid session', () => {
    writeSession(session);
    expect(readSession(Date.parse('2030-01-01T00:00:00Z'))).toEqual(session);
  });

  it('removes an expired session', () => {
    writeSession(session);
    expect(readSession(Date.parse('2040-01-01T00:00:00Z'))).toBeNull();
    expect(sessionStorage.length).toBe(0);
  });

  it('removes malformed session data', () => {
    sessionStorage.setItem('shiftflow.session', '{bad json');
    expect(readSession()).toBeNull();
    clearSession();
  });
});
