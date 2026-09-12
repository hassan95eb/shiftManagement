import { ApiError } from './api';

export function formatPersianDate(value: string) {
  return new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
    timeZone: 'Asia/Tehran',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  }).format(new Date(value));
}

export function getErrorMessage(error: unknown, fallback = 'انجام عملیات با خطا روبه‌رو شد.') {
  if (!(error instanceof ApiError)) return fallback;
  if (error.status === 0) return error.message;
  if (error.details) {
    const details = Object.values(error.details).flat();
    if (details.length) return details.join(' ');
  }
  if (error.status === 409) return error.message;
  if (error.status === 404) return 'مورد درخواستی پیدا نشد یا به آن دسترسی ندارید.';
  return error.message || fallback;
}
