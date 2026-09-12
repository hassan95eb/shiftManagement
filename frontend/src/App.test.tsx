import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import { AuthProvider } from './auth/AuthProvider';
import { AppRoutes } from './App';
import { writeSession } from './auth/auth-storage';
import { FeedbackProvider } from './components/Feedback';
import type { AuthSession } from './types/auth';

function renderApp(path = '/login') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[path]}><AuthProvider><FeedbackProvider><AppRoutes /></FeedbackProvider></AuthProvider></MemoryRouter></QueryClientProvider>);
}

const employerSession: AuthSession = {
  accessToken: 'jwt', expiresAtUtc: '2035-01-01T00:00:00Z', tokenType: 'Bearer', userId: 1,
  role: 'Employer', employerId: 4, expertId: null, username: 'employer',
};

const expertSession: AuthSession = {
  accessToken: 'jwt', expiresAtUtc: '2035-01-01T00:00:00Z', tokenType: 'Bearer', userId: 2,
  role: 'Expert', employerId: null, expertId: 7, username: 'ada',
};

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
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
    writeSession(expertSession);
    renderApp('/employer');
    expect(await screen.findByRole('heading', { name: 'دسترسی به این بخش مجاز نیست' })).toBeInTheDocument();
  });

  it('clears the session and opens the expiry page after an API 401 event', async () => {
    writeSession(employerSession);
    renderApp('/employer');
    await act(async () => window.dispatchEvent(new CustomEvent('shiftflow:unauthorized')));
    expect(await screen.findByRole('heading', { name: 'نشست شما پایان یافته است' })).toBeInTheDocument();
    expect(sessionStorage.getItem('shiftflow.session')).toBeNull();
  });
});

describe('V1 scheduling flow', () => {
  it('creates an employer shift and sends Tehran time as UTC', async () => {
    const user = userEvent.setup();
    const project = { id: 3, employerId: 4, name: 'پشتیبانی', isActive: true, createdAtUtc: '2026-09-01T08:00:00Z' };
    const shift = { id: 9, projectId: 3, startUtc: '2026-11-15T04:30:00.000Z', endUtc: '2026-11-15T12:30:00.000Z', status: 'Open', createdAtUtc: '2026-09-12T08:00:00Z', rowVersion: 'AAAAAA==' };
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/projects') return jsonResponse([project]);
      if (url === '/api/shifts' && init?.method === 'POST') return jsonResponse(shift, 201);
      if (url === '/api/shifts') return jsonResponse(fetchMock.mock.calls.some((call) => call[1]?.method === 'POST') ? [shift] : []);
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    writeSession(employerSession);

    renderApp('/employer/shifts');
    await screen.findByText('شیفتی پیدا نشد');
    await user.click(screen.getByRole('button', { name: 'ایجاد شیفت' }));
    await user.selectOptions(screen.getAllByLabelText('پروژه').at(-1)!, '3');
    fireEvent.change(screen.getByLabelText('شروع شیفت'), { target: { value: '2026-11-15T08:00' } });
    fireEvent.change(screen.getByLabelText('پایان شیفت'), { target: { value: '2026-11-15T16:00' } });
    await user.click(screen.getAllByRole('button', { name: 'ایجاد شیفت' }).at(-1)!);

    expect(await screen.findByText('شیفت جدید ایجاد شد.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/shifts', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ projectId: 3, startUtc: '2026-11-15T04:30:00.000Z', endUtc: '2026-11-15T12:30:00.000Z' }),
    }));
  });

  it('creates an expert availability window through the V1 API', async () => {
    const user = userEvent.setup();
    const window = { id: 4, expertId: 7, startUtc: '2026-11-16T04:30:00.000Z', endUtc: '2026-11-16T08:30:00.000Z', createdAtUtc: '2026-09-12T08:00:00Z' };
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/availability' && init?.method === 'POST') return jsonResponse(window, 201);
      if (url === '/api/availability') return jsonResponse(fetchMock.mock.calls.some((call) => call[1]?.method === 'POST') ? [window] : []);
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    writeSession(expertSession);

    renderApp('/expert/availability');
    await screen.findByText('بازه‌ای ثبت نشده است');
    await user.click(screen.getByRole('button', { name: 'افزودن بازه' }));
    fireEvent.change(screen.getByLabelText('شروع دسترسی'), { target: { value: '2026-11-16T08:00' } });
    fireEvent.change(screen.getByLabelText('پایان دسترسی'), { target: { value: '2026-11-16T12:00' } });
    await user.click(screen.getByRole('button', { name: 'ثبت بازه' }));

    expect(await screen.findByText('بازه دسترسی ثبت و با بازه‌های مجاور ادغام شد.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/availability', expect.objectContaining({ method: 'POST' }));
  });
});

describe('V1 application flow', () => {
  it('lets an expert apply to an open shift', async () => {
    const user = userEvent.setup();
    const shift = { id: 9, projectId: 3, startUtc: '2026-11-15T04:30:00Z', endUtc: '2026-11-15T12:30:00Z', status: 'Open', createdAtUtc: '2026-09-12T08:00:00Z', rowVersion: 'AAAAAA==' };
    const application = { id: 21, shiftId: 9, expertId: 7, status: 'Pending', appliedAtUtc: '2026-09-12T08:00:00Z', decidedByUserId: null, decidedAtUtc: null, decisionNote: null };
    let applied = false;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/shifts/open') return jsonResponse([shift]);
      if (url === '/api/applications') return jsonResponse(applied ? [application] : []);
      if (url === '/api/shifts/9/applications' && init?.method === 'POST') { applied = true; return jsonResponse(application, 201); }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    writeSession(expertSession);

    renderApp('/expert/shifts');
    await user.click(await screen.findByRole('button', { name: 'درخواست این شیفت' }));
    await user.click(screen.getByRole('button', { name: 'ثبت درخواست' }));

    expect(await screen.findByText('درخواست شیفت با موفقیت ثبت شد.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/shifts/9/applications', expect.objectContaining({ method: 'POST' }));
  });

  it('lets an employer approve a pending application', async () => {
    const user = userEvent.setup();
    const project = { id: 3, employerId: 4, name: 'پشتیبانی', isActive: true, createdAtUtc: '2026-09-01T08:00:00Z' };
    const expert = { id: 7, userId: 12, fullName: 'آدا رضایی', isActive: true, createdAtUtc: '2026-09-01T08:00:00Z' };
    const shift = { id: 9, projectId: 3, startUtc: '2026-11-15T04:30:00Z', endUtc: '2026-11-15T12:30:00Z', status: 'Open', createdAtUtc: '2026-09-12T08:00:00Z', rowVersion: 'AAAAAA==' };
    const pending = { id: 21, shiftId: 9, expertId: 7, status: 'Pending', appliedAtUtc: '2026-09-12T08:00:00Z', decidedByUserId: null, decidedAtUtc: null, decisionNote: null };
    const approved = { ...pending, status: 'Approved', decidedByUserId: 1, decidedAtUtc: '2026-09-12T09:00:00Z' };
    let decided = false;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.startsWith('/api/applications?')) return jsonResponse(decided ? [] : [pending]);
      if (url === '/api/shifts') return jsonResponse([shift]);
      if (url === '/api/projects') return jsonResponse([project]);
      if (url === '/api/experts') return jsonResponse([expert]);
      if (url === '/api/applications/21/approval' && init?.method === 'POST') { decided = true; return jsonResponse(approved); }
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
    writeSession(employerSession);

    renderApp('/employer/applications');
    await user.click(await screen.findByRole('button', { name: 'تأیید' }));
    await user.click(screen.getByRole('button', { name: 'تأیید نهایی' }));

    expect(await screen.findByText('درخواست تأیید و شیفت بسته شد.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/applications/21/approval', expect.objectContaining({ method: 'POST' }));
  });
});

describe('employer management flow', () => {
  it('lists projects and creates a new project through the V1 API', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([{ id: 3, employerId: 4, name: 'پشتیبانی', isActive: true, createdAtUtc: '2026-09-01T08:00:00Z' }]))
      .mockResolvedValueOnce(jsonResponse({ id: 4, employerId: 4, name: 'فروش', isActive: true, createdAtUtc: '2026-09-12T08:00:00Z' }, 201))
      .mockResolvedValueOnce(jsonResponse([
        { id: 3, employerId: 4, name: 'پشتیبانی', isActive: true, createdAtUtc: '2026-09-01T08:00:00Z' },
        { id: 4, employerId: 4, name: 'فروش', isActive: true, createdAtUtc: '2026-09-12T08:00:00Z' },
      ]));
    vi.stubGlobal('fetch', fetchMock);
    writeSession(employerSession);

    renderApp('/employer/projects');
    expect(await screen.findByText('پشتیبانی')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ایجاد پروژه' }));
    await user.type(screen.getByLabelText('نام پروژه'), 'فروش');
    await user.click(screen.getAllByRole('button', { name: 'ایجاد پروژه' }).at(-1)!);

    expect(await screen.findByText('فروش')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/projects', expect.objectContaining({
      method: 'POST', body: JSON.stringify({ name: 'فروش' }),
    }));
  });

  it('assigns an expert to an active project through the V1 endpoint', async () => {
    const user = userEvent.setup();
    const expert = { id: 8, userId: 12, fullName: 'سارا احمدی', isActive: true, createdAtUtc: '2026-09-01T08:00:00Z' };
    const project = { id: 5, employerId: 4, name: 'مرکز تماس', isActive: true, createdAtUtc: '2026-09-02T08:00:00Z' };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse(expert))
      .mockResolvedValueOnce(jsonResponse([]))
      .mockResolvedValueOnce(jsonResponse([project]))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(jsonResponse([project]))
      .mockResolvedValueOnce(jsonResponse([project]));
    vi.stubGlobal('fetch', fetchMock);
    writeSession(employerSession);

    renderApp('/employer/experts/8');
    expect((await screen.findAllByRole('heading', { name: 'سارا احمدی' })).length).toBeGreaterThan(0);
    await user.selectOptions(screen.getByLabelText('افزودن به پروژه'), '5');
    await user.click(screen.getByRole('button', { name: 'اتصال به پروژه' }));

    expect(await screen.findByText('کارشناس به پروژه متصل شد.')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/experts/8/projects/5', expect.objectContaining({ method: 'POST' }));
  });
});
