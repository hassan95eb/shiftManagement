import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/auth-context';
import { Brand } from '../components/Brand';
import { Icon } from '../components/icons';
import { ApiError } from '../lib/api';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!username.trim() || !password) {
      setError('نام کاربری و رمز عبور را وارد کنید.');
      return;
    }
    setSubmitting(true);
    setError('');
    try {
      const session = await login({ username: username.trim(), password });
      navigate(session.role === 'Employer' ? '/employer' : '/expert', { replace: true });
    } catch (caught) {
      if (caught instanceof ApiError && caught.status >= 500) {
        navigate('/server-error', { replace: true });
        return;
      }
      setError(caught instanceof ApiError ? caught.message : 'ورود انجام نشد. دوباره تلاش کنید.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="login-page">
      <section className="login-brand-panel">
        <Brand />
        <div><span className="eyebrow">سامانه داخلی مدیریت شیفت</span><h1>شیفت‌ها و درخواست‌ها، در یک نمای روشن</h1><p>فضای یکپارچه‌ی کارفرما و کارشناس برای برنامه‌ریزی و انتخاب شیفت.</p></div>
        <ul><li><Icon name="calendar" />مدیریت دقیق شیفت‌ها</li><li><Icon name="shield" />دسترسی متناسب با نقش واقعی حساب</li><li><Icon name="clock" />نمایش زمان تهران و تاریخ شمسی</li></ul>
      </section>
      <section className="login-form-panel">
        <form className="login-card" onSubmit={handleSubmit} noValidate>
          <span className="eyebrow">ورود به فضای کاری</span>
          <h2>خوش آمدید</h2>
          <p>نقش حساب پس از ورود، به‌صورت خودکار از سرور تشخیص داده می‌شود.</p>
          <label><span>نام کاربری</span><input autoComplete="username" value={username} onChange={(e) => setUsername(e.target.value)} autoFocus /></label>
          <label><span>رمز عبور</span><span className="password-field"><input type={showPassword ? 'text' : 'password'} autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} /><button type="button" onClick={() => setShowPassword((value) => !value)}>{showPassword ? 'پنهان' : 'نمایش'}</button></span></label>
          {error && <div className="form-error" role="alert"><Icon name="warning" />{error}</div>}
          <button className="primary-button" type="submit" disabled={submitting}>{submitting ? 'در حال ورود…' : 'ورود به سامانه'}</button>
          {import.meta.env.DEV && <div className="demo-accounts"><span>حساب‌های توسعه</span><button type="button" onClick={() => { setUsername('employer'); setPassword('Demo!Pass1'); }}>کارفرما</button><button type="button" onClick={() => { setUsername('ada'); setPassword('Demo!Pass1'); }}>کارشناس</button></div>}
        </form>
      </section>
    </main>
  );
}
