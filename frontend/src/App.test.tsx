import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import { AuthProvider } from './auth/AuthProvider';
import { AppRoutes } from './App';
import { writeSession } from './auth/auth-storage';
import type { AuthSession } from './types/auth';

function renderApp(path = '/login') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[path]}><AuthProvider><AppRoutes /></AuthProvider></MemoryRouter></QueryClientProvider>);
}

describe('authentication flow', () => {
  it('logs in against the API and opens the employer workspace', async () => {
    const user = userEvent.setup();
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      accessToken: 'jwt', expiresAtUtc: '2035-01-01T00:00:00Z', tokenType: 'Bearer', userId: 1,
      role: 'Employer', employerId: 4, expertId: null,
    }), { status: 200, headers: { 'Content-Type': 'application/json' } })));
    renderApp();
    await user.type(screen.getByLabelText('نام کاربری'), 'employer');
    await user.type(screen.getByLabelText('رمز عبور'), 'Demo!Pass1');
    await user.click(screen.getByRole('button', { name: 'ورود به سامانه' }));
    expect(await screen.findByRole('heading', { name: 'داشبورد مدیریت' })).toBeInTheDocument();
    expect(JSON.parse(sessionStorage.getItem('shiftflow.session') ?? '{}').accessToken).toBe('jwt');
  });

  it('shows the server credential error without expiring the session', async () => {
    const user = userEvent.setup();
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      status: 401, error: 'InvalidCredentials', message: 'نام کاربری یا رمز عبور نادرست است.',
    }), { status: 401, headers: { 'Content-Type': 'application/json' } })));
    renderApp();
    await user.type(screen.getByLabelText('نام کاربری'), 'bad');
    await user.type(screen.getByLabelText('رمز عبور'), 'bad');
    await user.click(screen.getByRole('button', { name: 'ورود به سامانه' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('نام کاربری یا رمز عبور نادرست است.');
  });

  it('redirects an anonymous visitor away from a protected route', async () => {
    renderApp('/employer');
    expect(await screen.findByRole('heading', { name: 'خوش آمدید' })).toBeInTheDocument();
  });

  it('renders forbidden for a valid user with the wrong role', async () => {
    const expertSession: AuthSession = {
      accessToken: 'jwt', expiresAtUtc: '2035-01-01T00:00:00Z', tokenType: 'Bearer', userId: 2,
      role: 'Expert', employerId: null, expertId: 7, username: 'ada',
    };
    writeSession(expertSession);
    renderApp('/employer');
    expect(await screen.findByRole('heading', { name: 'دسترسی به این بخش مجاز نیست' })).toBeInTheDocument();
  });

  it('clears the session and opens the expiry page after an API 401 event', async () => {
    const employerSession: AuthSession = {
      accessToken: 'jwt', expiresAtUtc: '2035-01-01T00:00:00Z', tokenType: 'Bearer', userId: 1,
      role: 'Employer', employerId: 4, expertId: null, username: 'employer',
    };
    writeSession(employerSession);
    renderApp('/employer');
    await act(async () => window.dispatchEvent(new CustomEvent('shiftflow:unauthorized')));
    expect(await screen.findByRole('heading', { name: 'نشست شما پایان یافته است' })).toBeInTheDocument();
    expect(sessionStorage.getItem('shiftflow.session')).toBeNull();
  });
});
