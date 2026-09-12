import type { LoginRequest, LoginResponse } from '../types/auth';

export interface ApiErrorBody {
  status: number;
  error: string;
  message: string;
  details?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly details?: Record<string, string[]>;

  constructor(body: ApiErrorBody) {
    super(body.message);
    this.name = 'ApiError';
    this.status = body.status;
    this.code = body.error;
    this.details = body.details;
  }
}

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
let accessToken: string | null = null;

export function setApiAccessToken(token: string | null) {
  accessToken = token;
}

function isApiErrorBody(value: unknown): value is ApiErrorBody {
  if (!value || typeof value !== 'object') return false;
  const body = value as Partial<ApiErrorBody>;
  return typeof body.status === 'number' && typeof body.error === 'string' && typeof body.message === 'string';
}

export async function apiRequest<T>(
  path: string,
  options: RequestInit = {},
  config: { suppressUnauthorizedEvent?: boolean } = {},
): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('Accept', 'application/json');
  if (options.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`);

  let response: Response;
  try {
    response = await fetch(`${baseUrl}${path}`, { ...options, headers });
  } catch {
    throw new ApiError({
      status: 0,
      error: 'NetworkError',
      message: 'ارتباط با سرور برقرار نشد. اتصال شبکه و وضعیت API را بررسی کنید.',
    });
  }

  if (response.status === 204) return undefined as T;

  const body = await response.json().catch(() => null) as unknown;
  if (!response.ok) {
    const error = isApiErrorBody(body)
      ? new ApiError(body)
      : new ApiError({ status: response.status, error: 'HttpError', message: 'پاسخ نامعتبر از سرور دریافت شد.' });
    if (response.status === 401 && !config.suppressUnauthorizedEvent) {
      window.dispatchEvent(new CustomEvent('shiftflow:unauthorized'));
    }
    throw error;
  }

  return body as T;
}

export const authApi = {
  login: (request: LoginRequest) => apiRequest<LoginResponse>(
    '/api/auth/login',
    { method: 'POST', body: JSON.stringify(request) },
    { suppressUnauthorizedEvent: true },
  ),
};
