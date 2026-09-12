import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AppShell } from '../components/AppShell';
import { EmptyState, LoadingState, Modal, useFeedback } from '../components/Feedback';
import { FieldError, InlineError, PageHeader } from '../components/Page';
import { Icon } from '../components/icons';
import { projectsApi } from '../lib/management-api';
import { shiftsApi } from '../lib/scheduling-api';
import { formatDuration, formatPersianDate, formatPersianTime, getErrorMessage, tehranLocalToUtc, toDateTimeLocalValue } from '../lib/presentation';
import type { Shift, ShiftStatus } from '../types/scheduling';

function ShiftFormModal({ shift, projects, pending, error, onClose, onSave }: {
  shift: Shift | null | undefined;
  projects: { id: number; name: string; isActive: boolean }[];
  pending: boolean;
  error: string;
  onClose: () => void;
  onSave: (projectId: number, startUtc: string, endUtc: string) => void;
}) {
  const [projectId, setProjectId] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [submitted, setSubmitted] = useState(false);
  useEffect(() => {
    setProjectId(shift ? String(shift.projectId) : '');
    setStart(shift ? toDateTimeLocalValue(shift.startUtc) : '');
    setEnd(shift ? toDateTimeLocalValue(shift.endUtc) : '');
    setSubmitted(false);
  }, [shift]);
  const invalidRange = Boolean(start && end && new Date(start) >= new Date(end));

  return <Modal open={shift !== undefined} title={shift ? 'ویرایش زمان شیفت' : 'ایجاد شیفت'} description={shift ? 'فقط شیفت باز و بدون درخواست قابل ویرایش است.' : 'زمان‌ها بر اساس ساعت تهران ثبت می‌شوند.'} confirmLabel={shift ? 'ذخیره تغییرات' : 'ایجاد شیفت'} pending={pending} onClose={onClose} onConfirm={() => {
    setSubmitted(true);
    if (projectId && start && end && !invalidRange) onSave(Number(projectId), tehranLocalToUtc(start), tehranLocalToUtc(end));
  }}>
    <div className="form-grid">
      <label className="form-field full"><span>پروژه</span><select value={projectId} disabled={Boolean(shift)} onChange={(event) => setProjectId(event.target.value)}><option value="">انتخاب پروژه فعال</option>{projects.filter((project) => project.isActive || project.id === shift?.projectId).map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}</select><FieldError message={submitted && !projectId ? 'انتخاب پروژه الزامی است.' : ''} /></label>
      <label className="form-field"><span>شروع شیفت</span><input type="datetime-local" value={start} onChange={(event) => setStart(event.target.value)} /><FieldError message={submitted && !start ? 'زمان شروع الزامی است.' : ''} /></label>
      <label className="form-field"><span>پایان شیفت</span><input type="datetime-local" value={end} onChange={(event) => setEnd(event.target.value)} /><FieldError message={submitted && !end ? 'زمان پایان الزامی است.' : ''} /></label>
    </div>
    {invalidRange && <InlineError message="زمان پایان باید بعد از زمان شروع باشد." />}
    {error && <InlineError message={error} />}
  </Modal>;
}

export function EmployerShiftsPage() {
  const queryClient = useQueryClient();
  const { showToast } = useFeedback();
  const [formShift, setFormShift] = useState<Shift | null | undefined>(undefined);
  const [projectFilter, setProjectFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState<ShiftStatus | ''>('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const projects = useQuery({ queryKey: ['projects'], queryFn: projectsApi.list });
  const shifts = useQuery({
    queryKey: ['shifts', projectFilter, statusFilter, fromDate, toDate],
    queryFn: () => shiftsApi.list({
      projectId: projectFilter ? Number(projectFilter) : undefined,
      status: statusFilter || undefined,
      fromUtc: fromDate ? tehranLocalToUtc(`${fromDate}T00:00`) : undefined,
      toUtc: toDate ? tehranLocalToUtc(`${toDate}T23:59`) : undefined,
    }),
  });
  const projectNames = useMemo(() => new Map(projects.data?.map((project) => [project.id, project.name])), [projects.data]);
  const filtered = shifts.data;
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['shifts'] });
  const create = useMutation({ mutationFn: shiftsApi.create, onSuccess: async () => { await refresh(); setFormShift(undefined); showToast('شیفت جدید ایجاد شد.'); } });
  const update = useMutation({ mutationFn: ({ shift, startUtc, endUtc }: { shift: Shift; startUtc: string; endUtc: string }) => shiftsApi.update(shift.id, { startUtc, endUtc, rowVersion: shift.rowVersion }), onSuccess: async () => { await refresh(); setFormShift(undefined); showToast('زمان شیفت به‌روزرسانی شد.'); } });
  const mutationError = create.error ?? update.error;

  return <AppShell>
    <PageHeader title="شیفت‌ها" description="ایجاد شیفت و مدیریت زمان‌بندی پروژه‌ها" action={<button className="primary-button" type="button" onClick={() => setFormShift(null)}><Icon name="calendar" />ایجاد شیفت</button>} />
    <section className="toolbar filter-toolbar"><label className="form-field"><span>پروژه</span><select value={projectFilter} onChange={(event) => setProjectFilter(event.target.value)}><option value="">همه پروژه‌ها</option>{projects.data?.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}</select></label><label className="form-field"><span>وضعیت</span><select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as ShiftStatus | '')}><option value="">همه وضعیت‌ها</option><option value="Open">باز</option><option value="Closed">بسته</option></select></label><label className="form-field"><span>از تاریخ</span><input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} /></label><label className="form-field"><span>تا تاریخ</span><input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} /></label><span>{filtered?.length.toLocaleString('fa-IR') ?? '۰'} شیفت</span></section>
    {(projects.isPending || shifts.isPending) && <LoadingState label="در حال دریافت شیفت‌ها…" />}
    {(projects.isError || shifts.isError) && <InlineError message={getErrorMessage(projects.error ?? shifts.error)} onRetry={() => { void projects.refetch(); void shifts.refetch(); }} />}
    {shifts.data && filtered?.length === 0 && <EmptyState title="شیفتی پیدا نشد" message="فیلترها را تغییر دهید یا یک شیفت جدید ایجاد کنید." />}
    {Boolean(filtered?.length) && <section className="data-card"><header><div><h2>فهرست شیفت‌ها</h2><p>شیفت بسته یا دارای درخواست قابل ویرایش نیست.</p></div></header><div className="table-scroll"><table><thead><tr><th>پروژه</th><th>تاریخ</th><th>ساعت</th><th>مدت</th><th>وضعیت</th><th>اقدام</th></tr></thead><tbody>{filtered?.map((shift) => <tr key={shift.id}><td><strong>{projectNames.get(shift.projectId) ?? `پروژه ${shift.projectId.toLocaleString('fa-IR')}`}</strong><small>شناسه شیفت: {shift.id.toLocaleString('fa-IR')}</small></td><td>{formatPersianDate(shift.startUtc)}</td><td><span dir="ltr">{formatPersianTime(shift.startUtc)} – {formatPersianTime(shift.endUtc)}</span></td><td>{formatDuration(shift.startUtc, shift.endUtc)}</td><td><span className={`status-chip ${shift.status === 'Open' ? 'active' : 'inactive'}`}>{shift.status === 'Open' ? 'باز' : 'بسته'}</span></td><td>{shift.status === 'Open' ? <button className="secondary-button compact" type="button" onClick={() => { create.reset(); update.reset(); setFormShift(shift); }}>ویرایش زمان</button> : <span className="muted-copy">قفل‌شده</span>}</td></tr>)}</tbody></table></div></section>}
    <ShiftFormModal shift={formShift} projects={projects.data ?? []} pending={create.isPending || update.isPending} error={mutationError ? getErrorMessage(mutationError) : ''} onClose={() => { setFormShift(undefined); create.reset(); update.reset(); }} onSave={(projectId, startUtc, endUtc) => formShift ? update.mutate({ shift: formShift, startUtc, endUtc }) : create.mutate({ projectId, startUtc, endUtc })} />
  </AppShell>;
}

export function OpenShiftsPage() {
  const shifts = useQuery({ queryKey: ['open-shifts'], queryFn: () => shiftsApi.open() });
  return <AppShell>
    <PageHeader title="شیفت‌های آزاد" description="شیفت‌های باز پروژه‌هایی که در آن‌ها عضو هستید" />
    {shifts.isPending && <LoadingState label="در حال دریافت شیفت‌های آزاد…" />}
    {shifts.isError && <InlineError message={getErrorMessage(shifts.error)} onRetry={() => void shifts.refetch()} />}
    {shifts.data?.length === 0 && <EmptyState title="شیفت آزادی وجود ندارد" message="در حال حاضر شیفت بازی در پروژه‌های شما ثبت نشده است." />}
    {Boolean(shifts.data?.length) && <section className="shift-grid">{shifts.data?.map((shift) => <article className="shift-card" key={shift.id}><header><span className="status-chip active">باز</span><small>پروژه {shift.projectId.toLocaleString('fa-IR')}</small></header><h2>{formatPersianDate(shift.startUtc)}</h2><div className="shift-card__time"><Icon name="clock" /><strong dir="ltr">{formatPersianTime(shift.startUtc)} – {formatPersianTime(shift.endUtc)}</strong></div><footer><span>{formatDuration(shift.startUtc, shift.endUtc)}</span><small>ثبت درخواست در تسک بعدی فعال می‌شود</small></footer></article>)}</section>}
  </AppShell>;
}
