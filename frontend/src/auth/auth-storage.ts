import type { AuthSession, UserRole } from '../types/auth';

const STORAGE_KEY = 'shiftflow.session';
const roles: UserRole[] = ['Employer', 'Expert'];

function isSession(value: unknown): value is AuthSession {
  if (!value || typeof value !== 'object') return false;
  const session = value as Partial<AuthSession>;
  return typeof session.accessToken === 'string'
    && typeof session.expiresAtUtc === 'string'
    && typeof session.userId === 'number'
    && typeof session.username === 'string'
    && roles.includes(session.role as UserRole);
}

export function readSession(now = Date.now()): AuthSession | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed: unknown = JSON.parse(raw);
    if (!isSession(parsed) || Date.parse(parsed.expiresAtUtc) <= now) {
      clearSession();
      return null;
    }
    return parsed;
  } catch {
    clearSession();
    return null;
  }
}

export function writeSession(session: AuthSession) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

export function clearSession() {
  sessionStorage.removeItem(STORAGE_KEY);
}
