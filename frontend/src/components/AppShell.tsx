import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { useAuth } from '../auth/auth-context';
import { Brand } from './Brand';
import { Icon } from './icons';

const roleLabels = { Employer: 'کارفرما', Expert: 'کارشناس' } as const;

function useTehranClock() {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 1_000);
    return () => window.clearInterval(timer);
  }, []);
  return useMemo(() => ({
    time: new Intl.DateTimeFormat('fa-IR', { timeZone: 'Asia/Tehran', hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(now),
    date: new Intl.DateTimeFormat('fa-IR-u-ca-persian', { timeZone: 'Asia/Tehran', day: 'numeric', month: 'long', year: 'numeric' }).format(now),
  }), [now]);
}

export function AppShell({ children }: { children: ReactNode }) {
  const { session, logout } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);
  const clock = useTehranClock();
  if (!session) return null;

  const isEmployer = session.role === 'Employer';
  const items = isEmployer
    ? [
        { label: 'داشبورد', icon: 'grid' as const, href: '/employer' },
        { label: 'پروژه‌ها', icon: 'folder' as const, href: '/employer/projects' },
        { label: 'کارشناسان', icon: 'users' as const, href: '/employer/experts' },
        { label: 'شیفت‌ها', icon: 'calendar' as const },
      ]
    : [
        { label: 'داشبورد', icon: 'grid' as const, href: '/expert' },
        { label: 'شیفت‌های آزاد', icon: 'calendar' as const },
        { label: 'درخواست‌های من', icon: 'folder' as const },
        { label: 'اعلام دسترسی', icon: 'clock' as const },
      ];

  return (
    <div className="app-layout">
      <header className="topbar">
        <button className="icon-button mobile-menu" type="button" onClick={() => setMenuOpen(true)} aria-label="بازکردن منو"><Icon name="menu" /></button>
        <Brand />
        <div className="topbar__clock"><Icon name="clock" /><span><small>ساعت تهران</small><strong>{clock.time}</strong></span><time>{clock.date}</time></div>
        <div className="user-summary"><span className="avatar">{session.username.slice(0, 2)}</span><span><strong>{session.username}</strong><small>{roleLabels[session.role]}</small></span></div>
      </header>

      <button className={`sidebar-backdrop ${menuOpen ? 'is-visible' : ''}`} onClick={() => setMenuOpen(false)} aria-label="بستن منو" />
      <aside className={`sidebar ${menuOpen ? 'is-open' : ''}`}>
        <button className="sidebar__close" type="button" onClick={() => setMenuOpen(false)} aria-label="بستن منو"><Icon name="close" /></button>
        <div className="workspace"><Icon name={isEmployer ? 'folder' : 'users'} /><span><small>فضای کاری</small><strong>{roleLabels[session.role]}</strong></span></div>
        <nav aria-label="ناوبری اصلی">
          {items.map((item) => item.href ? (
            <NavLink key={item.label} to={item.href} end={item.href === '/employer' || item.href === '/expert'} className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`} onClick={() => setMenuOpen(false)}>
              <Icon name={item.icon} /><span>{item.label}</span>
            </NavLink>
          ) : (
            <span className="nav-item disabled" key={item.label} aria-disabled="true" title="در تسک بعدی فعال می‌شود">
              <Icon name={item.icon} /><span>{item.label}</span><small>به‌زودی</small>
            </span>
          ))}
        </nav>
        <div className="sidebar__footer"><span><i />سامانه در دسترس است</span><button type="button" onClick={logout}><Icon name="logout" />خروج از حساب</button></div>
      </aside>

      <main className="main-content" id="main-content">{children}</main>
    </div>
  );
}
