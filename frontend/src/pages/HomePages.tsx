import { AppShell } from '../components/AppShell';
import { Icon } from '../components/icons';
import { useAuth } from '../auth/auth-context';
import { Link } from 'react-router-dom';

function TaskOneNotice({ role }: { role: 'کارفرما' | 'کارشناس' }) {
  return (
    <section className="welcome-card">
      <span className="welcome-card__icon"><Icon name="shield" /></span>
      <div><span className="eyebrow">زیرساخت آماده است</span><h2>فضای {role} با موفقیت فعال شد</h2><p>ورود، نشست کاربر، دسترسی مبتنی بر نقش و ارتباط با API اکنون واقعی است. امکانات عملیاتی در تسک‌های بعدی به همین فضا اضافه می‌شوند.</p></div>
    </section>
  );
}

export function EmployerHomePage() {
  const { session } = useAuth();
  return <AppShell><header className="page-heading"><div><span>فضای کارفرما</span><h1>داشبورد مدیریت</h1><p>خوش آمدید، {session?.username}</p></div><span className="status-badge">نشست فعال</span></header><div className="dashboard-links"><Link to="/employer/projects"><Icon name="folder" /><span><strong>پروژه‌ها</strong><small>ایجاد و مدیریت پروژه‌ها</small></span></Link><Link to="/employer/experts"><Icon name="users" /><span><strong>کارشناسان</strong><small>ایجاد حساب و تخصیص پروژه</small></span></Link></div></AppShell>;
}

export function ExpertHomePage() {
  const { session } = useAuth();
  return <AppShell><header className="page-heading"><div><span>فضای کارشناس</span><h1>داشبورد من</h1><p>خوش آمدید، {session?.username}</p></div><span className="status-badge">نشست فعال</span></header><TaskOneNotice role="کارشناس" /></AppShell>;
}
