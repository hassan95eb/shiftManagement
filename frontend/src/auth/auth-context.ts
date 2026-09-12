import { createContext, useContext } from 'react';
import type { AuthSession, LoginRequest } from '../types/auth';

export interface AuthContextValue {
  session: AuthSession | null;
  sessionEndPath: string | null;
  login: (credentials: LoginRequest) => Promise<AuthSession>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside AuthProvider.');
  return context;
}
