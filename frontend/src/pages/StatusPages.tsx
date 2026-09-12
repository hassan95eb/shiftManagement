import { Link } from 'react-router-dom';
import { Brand } from '../components/Brand';
import { Icon } from '../components/icons';

interface StatusProps { code: string; title: string; message: string; action: string; href: string }

function StatusPage({ code, title, message, action, href }: StatusProps) {
  return <main className="status-page"><Brand /><section><span className="status-code">{code}</span><span className="status-illustration"><Icon name="warning" /></span><h1>{title}</h1><p>{message}</p><Link className="primary-button" to={href}>{action}</Link></section></main>;
}

export function NotFoundPage() { return <StatusPage code="۴۰۴" title="این صفحه پیدا نشد" message="ممکن است آدرس تغییر کرده باشد یا این بخش هنوز پیاده‌سازی نشده باشد." action="بازگشت به ورود" href="/login" />; }
export function ForbiddenPage() { return <StatusPage code="۴۰۳" title="دسترسی به این بخش مجاز نیست" message="این صفحه برای نقش حساب شما در دسترس نیست." action="بازگشت به فضای کاری" href="/" />; }
export function ServerErrorPage() { return <StatusPage code="۵۰۰" title="ارتباط با سرور دچار مشکل شد" message="اطلاعاتی تغییر نکرده است. وضعیت API را بررسی و دوباره تلاش کنید." action="تلاش دوباره" href="/login" />; }
export function SessionExpiredPage() { return <StatusPage code="۴۰۱" title="نشست شما پایان یافته است" message="برای حفظ امنیت حساب، دوباره وارد سامانه شوید." action="ورود دوباره" href="/login" />; }
