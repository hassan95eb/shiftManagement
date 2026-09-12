import { ApiError } from './api';

export function formatPersianDate(value: string) {
  return new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
    timeZone: 'Asia/Tehran',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  }).format(new Date(value));
}

export function formatPersianTime(value: string) {
  return new Intl.DateTimeFormat('fa-IR', {
    timeZone: 'Asia/Tehran',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value));
}

export function formatDuration(start: string, end: string) {
  const minutes = Math.max(0, Math.round((new Date(end).getTime() - new Date(start).getTime()) / 60_000));
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest ? `${hours.toLocaleString('fa-IR')} ساعت و ${rest.toLocaleString('fa-IR')} دقیقه` : `${hours.toLocaleString('fa-IR')} ساعت`;
}

export function toDateTimeLocalValue(value: string) {
  const date = new Date(value);
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Tehran', year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? '';
  return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}`;
}

export function tehranLocalToUtc(value: string) {
  if (!value) return '';
  const [date, time] = value.split('T');
  const [year, month, day] = date.split('-').map(Number);
  const [hour, minute] = time.split(':').map(Number);
  // Iran has used a fixed UTC+03:30 offset since September 2022.
  return new Date(Date.UTC(year, month - 1, day, hour, minute) - 210 * 60_000).toISOString();
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
