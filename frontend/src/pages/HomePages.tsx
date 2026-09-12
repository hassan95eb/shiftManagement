import { AppShell } from '../components/AppShell';
import { Icon } from '../components/icons';
import { useAuth } from '../auth/auth-context';
import { Link } from 'react-router-dom';

export function EmployerHomePage() {
  const { session } = useAuth();
  return <AppShell><header className="page-heading"><div><span>فضای کارفرما</span><h1>داشبورد مدیریت</h1><p>خوش آمدید، {session?.username}</p></div><span className="status-badge">نشست فعال</span></header><div className="dashboard-links"><Link to="/employer/projects"><Icon name="folder" /><span><strong>پروژه‌ها</strong><small>ایجاد و مدیریت پروژه‌ها</small></span></Link><Link to="/employer/experts"><Icon name="users" /><span><strong>کارشناسان</strong><small>ایجاد حساب و تخصیص پروژه</small></span></Link><Link to="/employer/shifts"><Icon name="calendar" /><span><strong>شیفت‌ها</strong><small>ایجاد و مدیریت زمان‌بندی</small></span></Link><Link to="/employer/applications"><Icon name="shield" /><span><strong>درخواست‌ها</strong><small>بررسی، رتبه‌بندی و تصمیم‌گیری</small></span></Link></div></AppShell>;
}

export function ExpertHomePage() {
  const { session } = useAuth();
  return <AppShell><header className="page-heading"><div><span>فضای کارشناس</span><h1>داشبورد من</h1><p>خوش آمدید، {session?.username}</p></div><span className="status-badge">نشست فعال</span></header><div className="dashboard-links"><Link to="/expert/shifts"><Icon name="calendar" /><span><strong>شیفت‌های آزاد</strong><small>مشاهده و ثبت درخواست شیفت</small></span></Link><Link to="/expert/applications"><Icon name="folder" /><span><strong>درخواست‌های من</strong><small>پیگیری وضعیت درخواست‌ها</small></span></Link><Link to="/expert/availability"><Icon name="clock" /><span><strong>اعلام دسترسی</strong><small>ثبت و مدیریت زمان‌های آزاد</small></span></Link></div></AppShell>;
}
