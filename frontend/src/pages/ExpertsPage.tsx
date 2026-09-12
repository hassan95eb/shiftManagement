import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { AppShell } from '../components/AppShell';
import { EmptyState, LoadingState, Modal, useFeedback } from '../components/Feedback';
import { FieldError, InlineError, PageHeader } from '../components/Page';
import { Icon } from '../components/icons';
import { expertsApi, projectsApi } from '../lib/management-api';
import { formatPersianDate, getErrorMessage } from '../lib/presentation';
import type { Project } from '../types/management';

function CreateExpertModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const queryClient = useQueryClient();
  const { showToast } = useFeedback();
  const [fullName, setFullName] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const create = useMutation({
    mutationFn: expertsApi.create,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['experts'] });
      showToast('حساب کارشناس ایجاد شد.');
      setFullName(''); setUsername(''); setPassword(''); setSubmitted(false); onClose();
    },
  });
  const close = () => { create.reset(); setSubmitted(false); onClose(); };

  return <Modal open={open} title="افزودن کارشناس" description="نام کاربری در همه حساب‌های سامانه باید یکتا باشد." confirmLabel="ایجاد حساب" pending={create.isPending} onClose={close} onConfirm={() => { setSubmitted(true); if (fullName.trim() && username.trim() && password) create.mutate({ fullName: fullName.trim(), username: username.trim(), password }); }}>
    <div className="form-grid">
      <label className="form-field"><span>نام و نام خانوادگی</span><input value={fullName} onChange={(event) => setFullName(event.target.value)} maxLength={128} autoFocus /><FieldError message={submitted && !fullName.trim() ? 'نام کامل الزامی است.' : ''} /></label>
      <label className="form-field"><span>نام کاربری</span><input dir="ltr" value={username} onChange={(event) => setUsername(event.target.value)} maxLength={64} autoComplete="off" /><FieldError message={submitted && !username.trim() ? 'نام کاربری الزامی است.' : ''} /></label>
      <label className="form-field full"><span>رمز عبور اولیه</span><input dir="ltr" type="password" value={password} onChange={(event) => setPassword(event.target.value)} maxLength={128} autoComplete="new-password" /><FieldError message={submitted && !password ? 'رمز عبور اولیه الزامی است.' : ''} /></label>
    </div>
    {create.isError && <InlineError message={getErrorMessage(create.error)} />}
  </Modal>;
}

export function ExpertsPage() {
  const [search, setSearch] = useState('');
  const [createOpen, setCreateOpen] = useState(false);
  const experts = useQuery({ queryKey: ['experts'], queryFn: expertsApi.list });
  const filtered = useMemo(() => {
    const phrase = search.trim().toLocaleLowerCase('fa');
    return phrase ? experts.data?.filter((expert) => expert.fullName.toLocaleLowerCase('fa').includes(phrase)) : experts.data;
  }, [experts.data, search]);

  return <AppShell>
    <PageHeader title="کارشناسان" description="فهرست کارشناسان و مدیریت عضویت آن‌ها در پروژه‌ها" action={<button className="primary-button" type="button" onClick={() => setCreateOpen(true)}><Icon name="users" />افزودن کارشناس</button>} />
    <section className="toolbar"><label className="search-field"><Icon name="users" /><input type="search" placeholder="جست‌وجوی نام کارشناس" value={search} onChange={(event) => setSearch(event.target.value)} /></label><span>{filtered?.length.toLocaleString('fa-IR') ?? '۰'} نتیجه</span></section>
    {experts.isPending && <LoadingState label="در حال دریافت کارشناسان…" />}
    {experts.isError && <InlineError message={getErrorMessage(experts.error)} onRetry={() => void experts.refetch()} />}
    {experts.data?.length === 0 && <EmptyState title="هنوز کارشناسی ثبت نشده" message="یک حساب کارشناس ایجاد کنید و سپس آن را به پروژه‌های خود متصل کنید." />}
    {experts.data && experts.data.length > 0 && filtered?.length === 0 && <EmptyState title="نتیجه‌ای پیدا نشد" message="عبارت جست‌وجو را تغییر دهید." />}
    {Boolean(filtered?.length) && <section className="data-card"><header><div><h2>فهرست کارشناسان</h2><p>نام کاربری به‌دلایل امنیتی توسط API در این فهرست نمایش داده نمی‌شود.</p></div></header><div className="table-scroll"><table><thead><tr><th>کارشناس</th><th>شناسه کاربر</th><th>تاریخ ایجاد</th><th>وضعیت</th><th>اقدام</th></tr></thead><tbody>{filtered?.map((expert) => <tr key={expert.id}><td><span className="person-cell"><span className="avatar">{expert.fullName.slice(0, 2)}</span><span><strong>{expert.fullName}</strong><small>شناسه کارشناس: {expert.id.toLocaleString('fa-IR')}</small></span></span></td><td>{expert.userId.toLocaleString('fa-IR')}</td><td>{formatPersianDate(expert.createdAtUtc)}</td><td><span className={`status-chip ${expert.isActive ? 'active' : 'inactive'}`}>{expert.isActive ? 'فعال' : 'غیرفعال'}</span></td><td><Link className="secondary-button compact" to={`/employer/experts/${expert.id}`}>مشاهده و تخصیص</Link></td></tr>)}</tbody></table></div></section>}
    <CreateExpertModal open={createOpen} onClose={() => setCreateOpen(false)} />
  </AppShell>;
}

export function ExpertDetailPage() {
  const rawId = useParams().id;
  const expertId = Number(rawId);
  const queryClient = useQueryClient();
  const { showToast } = useFeedback();
  const [selectedProject, setSelectedProject] = useState('');
  const [removeProject, setRemoveProject] = useState<Project | null>(null);
  const [operationError, setOperationError] = useState('');

  const expert = useQuery({ queryKey: ['experts', expertId], queryFn: () => expertsApi.get(expertId), enabled: Number.isInteger(expertId) && expertId > 0 });
  const assigned = useQuery({ queryKey: ['experts', expertId, 'projects'], queryFn: () => expertsApi.projects(expertId), enabled: Number.isInteger(expertId) && expertId > 0 });
  const projects = useQuery({ queryKey: ['projects'], queryFn: projectsApi.list });
  const assignedIds = new Set(assigned.data?.map((project) => project.id));
  const availableProjects = projects.data?.filter((project) => project.isActive && !assignedIds.has(project.id)) ?? [];

  const refreshAssignments = () => Promise.all([
    queryClient.invalidateQueries({ queryKey: ['experts', expertId, 'projects'] }),
    queryClient.invalidateQueries({ queryKey: ['projects'] }),
  ]);
  const assign = useMutation({
    mutationFn: (projectId: number) => expertsApi.assign(expertId, projectId),
    onSuccess: async () => { await refreshAssignments(); setSelectedProject(''); showToast('کارشناس به پروژه متصل شد.'); },
    onError: (error) => setOperationError(getErrorMessage(error)),
  });
  const unassign = useMutation({
    mutationFn: (projectId: number) => expertsApi.unassign(expertId, projectId),
    onSuccess: async () => { await refreshAssignments(); setRemoveProject(null); showToast('اتصال کارشناس از پروژه حذف شد.'); },
  });

  if (!Number.isInteger(expertId) || expertId <= 0) return <AppShell><InlineError message="شناسه کارشناس معتبر نیست." /></AppShell>;

  return <AppShell>
    <PageHeader title={expert.data?.fullName ?? 'جزئیات کارشناس'} description="مشاهده مشخصات و مدیریت پروژه‌های کارشناس" action={<Link className="secondary-button" to="/employer/experts">بازگشت به فهرست</Link>} />
    {(expert.isPending || assigned.isPending || projects.isPending) && <LoadingState />}
    {(expert.isError || assigned.isError || projects.isError) && <InlineError message={getErrorMessage(expert.error ?? assigned.error ?? projects.error)} onRetry={() => { void expert.refetch(); void assigned.refetch(); void projects.refetch(); }} />}
    {operationError && <InlineError message={operationError} />}
    {expert.data && assigned.data && projects.data && <>
      <section className="profile-card"><span className="profile-avatar">{expert.data.fullName.slice(0, 2)}</span><div><span className="eyebrow">پرونده کارشناس</span><h2>{expert.data.fullName}</h2><p>ایجاد حساب: {formatPersianDate(expert.data.createdAtUtc)}</p></div><dl><div><dt>شناسه کارشناس</dt><dd>{expert.data.id.toLocaleString('fa-IR')}</dd></div><div><dt>وضعیت</dt><dd><span className={`status-chip ${expert.data.isActive ? 'active' : 'inactive'}`}>{expert.data.isActive ? 'فعال' : 'غیرفعال'}</span></dd></div></dl></section>
      <section className="data-card assignment-card"><header><div><h2>پروژه‌های کارشناس</h2><p>فقط پروژه‌های متعلق به حساب کارفرمای فعلی نمایش داده می‌شوند.</p></div></header>
        <div className="assign-control"><label className="form-field"><span>افزودن به پروژه</span><select value={selectedProject} onChange={(event) => setSelectedProject(event.target.value)}><option value="">انتخاب پروژه فعال</option>{availableProjects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}</select></label><button className="primary-button" type="button" disabled={!selectedProject || assign.isPending} onClick={() => { setOperationError(''); assign.mutate(Number(selectedProject)); }}>{assign.isPending ? 'در حال اتصال…' : 'اتصال به پروژه'}</button></div>
        {assigned.data.length === 0 ? <EmptyState title="پروژه‌ای متصل نیست" message="از فهرست بالا یکی از پروژه‌های فعال را انتخاب کنید." /> : <div className="assignment-list">{assigned.data.map((project) => <article key={project.id}><span><strong>{project.name}</strong><small>{project.isActive ? 'پروژه فعال' : 'پروژه غیرفعال'}</small></span><button className="danger-button compact" type="button" onClick={() => { setOperationError(''); setRemoveProject(project); }}>حذف اتصال</button></article>)}</div>}
      </section>
    </>}
    <Modal open={Boolean(removeProject)} title="حذف اتصال پروژه" description="درخواست‌های در انتظار این کارشناس در پروژه رد خواهند شد." confirmLabel="حذف اتصال" pending={unassign.isPending} onClose={() => { setRemoveProject(null); unassign.reset(); }} onConfirm={() => removeProject && unassign.mutate(removeProject.id)}>
      <p className="confirm-copy">اتصال <strong>{expert.data?.fullName}</strong> از پروژه‌ی <strong>{removeProject?.name}</strong> حذف شود؟ اگر کارشناس شیفت تأییدشده داشته باشد، سرور این عملیات را متوقف می‌کند.</p>
      {unassign.isError && <InlineError message={getErrorMessage(unassign.error)} />}
    </Modal>
  </AppShell>;
}
