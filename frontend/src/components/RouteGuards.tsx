import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../auth/auth-context';
import type { UserRole } from '../types/auth';
import { ForbiddenPage } from '../pages/StatusPages';

export function PublicOnlyRoute({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  if (session) return <Navigate to={session.role === 'Employer' ? '/employer' : '/expert'} replace />;
  return children;
}

export function ProtectedRoute({ role, children }: { role: UserRole; children: ReactNode }) {
  const { session, sessionEndPath } = useAuth();
  if (!session) return <Navigate to={sessionEndPath ?? '/login'} replace />;
  if (session.role !== role) return <ForbiddenPage />;
  return children;
}
