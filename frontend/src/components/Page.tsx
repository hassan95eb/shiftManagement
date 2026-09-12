import type { ReactNode } from 'react';

export function PageHeader({ title, description, action, eyebrow = 'فضای کارفرما' }: { title: string; description: string; action?: ReactNode; eyebrow?: string }) {
  return <header className="page-heading"><div><span>{eyebrow}</span><h1>{title}</h1><p>{description}</p></div>{action}</header>;
}

export function InlineError({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return <div className="inline-alert" role="alert"><strong>خطا در دریافت یا ثبت اطلاعات</strong><span>{message}</span>{onRetry && <button className="secondary-button" type="button" onClick={onRetry}>تلاش دوباره</button>}</div>;
}

export function FieldError({ message }: { message?: string }) {
  return message ? <small className="field-error">{message}</small> : null;
}
