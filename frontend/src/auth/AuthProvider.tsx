import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError, authApi, setApiAccessToken } from '../lib/api';
import type { AuthSession, LoginRequest } from '../types/auth';
import { clearSession, readSession, writeSession } from './auth-storage';
import { AuthContext } from './auth-context';

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const [session, setSession] = useState<AuthSession | null>(() => {
    const stored = readSession();
    setApiAccessToken(stored?.accessToken ?? null);
    return stored;
  });
  const [sessionEndPath, setSessionEndPath] = useState<string | null>(null);

  useEffect(() => {
    setApiAccessToken(session?.accessToken ?? null);
    if (!session) return;

    const remaining = Date.parse(session.expiresAtUtc) - Date.now();
    if (remaining <= 0) return;
    const timer = window.setTimeout(() => {
      clearSession();
      setApiAccessToken(null);
      setSessionEndPath('/session-expired');
      setSession(null);
      navigate('/session-expired', { replace: true });
    }, Math.min(remaining, 2_147_000_000));
    return () => window.clearTimeout(timer);
  }, [navigate, session]);

  useEffect(() => {
    const handleUnauthorized = () => {
      clearSession();
      setApiAccessToken(null);
      setSessionEndPath('/session-expired');
      setSession(null);
      navigate('/session-expired', { replace: true });
    };
    window.addEventListener('shiftflow:unauthorized', handleUnauthorized);
    return () => window.removeEventListener('shiftflow:unauthorized', handleUnauthorized);
  }, [navigate]);

  const login = useCallback(async (credentials: LoginRequest) => {
    const response = await authApi.login(credentials);
    if (response.role !== 'Employer' && response.role !== 'Expert') {
      throw new ApiError({ status: 403, error: 'UnsupportedRole', message: 'نقش این حساب در فرانت پشتیبانی نمی‌شود.' });
    }
    const nextSession: AuthSession = { ...response, username: credentials.username };
    writeSession(nextSession);
    setSessionEndPath(null);
    setApiAccessToken(nextSession.accessToken);
    setSession(nextSession);
    return nextSession;
  }, []);

  const logout = useCallback(() => {
    clearSession();
    setApiAccessToken(null);
    setSessionEndPath(null);
    setSession(null);
    navigate('/login', { replace: true });
  }, [navigate]);

  const value = useMemo(() => ({ session, sessionEndPath, login, logout }), [login, logout, session, sessionEndPath]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
