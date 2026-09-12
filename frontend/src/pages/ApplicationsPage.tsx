import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AppShell } from '../components/AppShell';
import { EmptyState, LoadingState, Modal, useFeedback } from '../components/Feedback';
import { InlineError, PageHeader } from '../components/Page';
import { applicationsApi } from '../lib/applications-api';
import { expertsApi, projectsApi } from '../lib/management-api';
import { shiftsApi } from '../lib/scheduling-api';
import { formatPersianDate, formatPersianTime, getErrorMessage } from '../lib/presentation';
import type { ApplicationStatus, ShiftApplication } from '../types/applications';
import type { Shift } from '../types/scheduling';

const statusLabels: Record<ApplicationStatus, string> = { Pending: 'در انتظار', Approved: 'تأییدشده', Rejected: 'ردشده' };
const statusClasses: Record<ApplicationStatus, string> = { Pending: 'pending', Approved: 'active', Rejected: 'rejected' };

function RecommendationModal({ shift, expertNames, onClose }: { shift: Shift | null; expertNames: Map<number, string>; onClose: () => void }) {
  const recommendations = useQuery({ queryKey: ['recommendations', shift?.id], queryFn: () => applicationsApi.recommendations(shift!.id), enabled: Boolean(shift) });
  return <Modal open={Boolean(shift)} title="رتبه‌بندی متقاضیان" description={shift ? `${formatPersianDate(shift.startUtc)}، ساعت ${formatPersianTime(shift.startUtc)}` : ''} onClose={onClose}>
    {recommendations.isPending && <LoadingState label="در حال دریافت پیشنهادها…" />}
    {recommendations.isError && <InlineError message={getErrorMessage(recommendations.error)} onRetry={() => void recommendations.refetch()} />}
    {recommendations.data?.length === 0 && <EmptyState title="پیشنهادی محاسبه نشده" message="اسکریپت پیشنهاددهنده را اجرا کنید تا رتبه‌بندی متقاضیان ساخته شود." />}
    {Boolean(recommendations.data?.length) && <div className="ranking-list">{recommendations.data?.map((item, index) => <article key={item.expertId}><span className="rank-number">{(index + 1).toLocaleString('fa-IR')}</span><div><strong>{expertNames.get(item.expertId) ?? `کارشناس ${item.expertId.toLocaleString('fa-IR')}`}</strong><small>{item.reason}</small></div><b>{item.score.toLocaleString('fa-IR')}٪</b></article>)}</div>}
  </Modal>;
}

export function EmployerApplicationsPage() {
  const queryClient = useQueryClient();
  const { showToast } = useFeedback();
  const [status, setStatus] = useState<ApplicationStatus | ''>('Pending');
  const [shiftId, setShiftId] = useState('');
  const [decision, setDecision] = useState<{ application: ShiftApplication; action: 'approve' | 'reject' } | null>(null);
  const [rankingShift, setRankingShift] = useState<Shift | null>(null);
  const applications = useQuery({ queryKey: ['applications', status, shiftId], queryFn: () => applicationsApi.list({ status: status || undefined, shiftId: shiftId ? Number(shiftId) : undefined }) });
  const shifts = useQuery({ queryKey: ['shifts'], queryFn: () => shiftsApi.list() });
  const projects = useQuery({ queryKey: ['projects'], queryFn: projectsApi.list });
  const experts = useQuery({ queryKey: ['experts'], queryFn: expertsApi.list });
  const shiftsById = useMemo(() => new Map(shifts.data?.map((shift) => [shift.id, shift])), [shifts.data]);
  const projectNames = useMemo(() => new Map(projects.data?.map((project) => [project.id, project.name])), [projects.data]);
  const expertNames = useMemo(() => new Map(experts.data?.map((expert) => [expert.id, expert.fullName])), [experts.data]);
  const refresh = () => Promise.all([queryClient.invalidateQueries({ queryKey: ['applications'] }), queryClient.invalidateQueries({ queryKey: ['shifts'] })]);
  const decide = useMutation({
    mutationFn: ({ application, action }: { application: ShiftApplication; action: 'approve' | 'reject' }) => action === 'approve' ? applicationsApi.approve(application.id) : applicationsApi.reject(application.id),
    onSuccess: async (_, variables) => { await refresh(); setDecision(null); showToast(variables.action === 'approve' ? 'درخواست تأیید و شیفت بسته شد.' : 'درخواست رد شد.'); },
  });
  const loading = applications.isPending || shifts.isPending || projects.isPending || experts.isPending;
  const error = applications.error ?? shifts.error ?? projects.error ?? experts.error;

  return <AppShell>
    <PageHeader title="درخواست‌های شیفت" description="بررسی متقاضیان، رتبه‌بندی و ثبت تصمیم نهایی" />
    <section className="toolbar filter-toolbar"><label className="form-field"><span>وضعیت درخواست</span><select value={status} onChange={(event) => setStatus(event.target.value as ApplicationStatus | '')}><option value="">همه وضعیت‌ها</option><option value="Pending">در انتظار</option><option value="Approved">تأییدشده</option><option value="Rejected">ردشده</option></select></label><label className="form-field"><span>شیفت</span><select value={shiftId} onChange={(event) => setShiftId(event.target.value)}><option value="">همه شیفت‌ها</option>{shifts.data?.map((shift) => <option key={shift.id} value={shift.id}>#{shift.id} — {formatPersianDate(shift.startUtc)}</option>)}</select></label><span>{applications.data?.length.toLocaleString('fa-IR') ?? '۰'} درخواست</span></section>
    {loading && <LoadingState label="در حال دریافت درخواست‌ها…" />}
    {error && <InlineError message={getErrorMessage(error)} onRetry={() => { void applications.refetch(); void shifts.refetch(); void projects.refetch(); void experts.refetch(); }} />}
    {!loading && applications.data?.length === 0 && <EmptyState title="درخواستی پیدا نشد" message="فیلتر را تغییر دهید یا منتظر ثبت درخواست جدید باشید." />}
    {Boolean(applications.data?.length) && <section className="data-card"><header><div><h2>فهرست درخواست‌ها</h2><p>تأیید یک درخواست، شیفت را می‌بندد و سایر درخواست‌های در انتظار را رد می‌کند.</p></div></header><div className="table-scroll"><table><thead><tr><th>کارشناس</th><th>شیفت و پروژه</th><th>زمان درخواست</th><th>وضعیت</th><th>رتبه‌بندی</th><th>اقدام</th></tr></thead><tbody>{applications.data?.map((application) => { const shift = shiftsById.get(application.shiftId); return <tr key={application.id}><td><strong>{expertNames.get(application.expertId) ?? `کارشناس ${application.expertId.toLocaleString('fa-IR')}`}</strong><small>شناسه درخواست: {application.id.toLocaleString('fa-IR')}</small></td><td><strong>{shift ? projectNames.get(shift.projectId) ?? `پروژه ${shift.projectId}` : `شیفت ${application.shiftId}`}</strong><small>{shift ? `${formatPersianDate(shift.startUtc)}، ${formatPersianTime(shift.startUtc)}` : `شناسه شیفت: ${application.shiftId}`}</small></td><td>{formatPersianDate(application.appliedAtUtc)}<small>{formatPersianTime(application.appliedAtUtc)}</small></td><td><span className={`status-chip ${statusClasses[application.status]}`}>{statusLabels[application.status]}</span>{application.decisionNote && <small>{application.decisionNote}</small>}</td><td>{shift ? <button className="text-button" type="button" onClick={() => setRankingShift(shift)}>مشاهده امتیازها</button> : '—'}</td><td>{application.status === 'Pending' ? <div className="row-actions"><button className="success-button compact" type="button" onClick={() => setDecision({ application, action: 'approve' })}>تأیید</button><button className="danger-button compact" type="button" onClick={() => setDecision({ application, action: 'reject' })}>رد</button></div> : <span className="muted-copy">تصمیم ثبت شده</span>}</td></tr>; })}</tbody></table></div></section>}
    <RecommendationModal shift={rankingShift} expertNames={expertNames} onClose={() => setRankingShift(null)} />
    <Modal open={Boolean(decision)} title={decision?.action === 'approve' ? 'تأیید درخواست' : 'رد درخواست'} description={decision?.action === 'approve' ? 'این تصمیم باعث بسته‌شدن شیفت می‌شود.' : 'شیفت برای سایر متقاضیان باز می‌ماند.'} confirmLabel={decision?.action === 'approve' ? 'تأیید نهایی' : 'رد درخواست'} pending={decide.isPending} onClose={() => { setDecision(null); decide.reset(); }} onConfirm={() => decision && decide.mutate(decision)}><p className="confirm-copy">درخواست <strong>{decision ? expertNames.get(decision.application.expertId) : ''}</strong> برای شیفت شماره <strong>{decision?.application.shiftId.toLocaleString('fa-IR')}</strong> {decision?.action === 'approve' ? 'تأیید' : 'رد'} شود؟</p>{decide.isError && <InlineError message={getErrorMessage(decide.error)} />}</Modal>
  </AppShell>;
}

export function MyApplicationsPage() {
  const [status, setStatus] = useState<ApplicationStatus | ''>('');
  const applications = useQuery({ queryKey: ['my-applications', status], queryFn: () => applicationsApi.list({ status: status || undefined }) });
  return <AppShell>
    <PageHeader title="درخواست‌های من" description="پیگیری وضعیت درخواست‌های ثبت‌شده برای شیفت‌ها" />
    <section className="toolbar filter-toolbar"><label className="form-field"><span>وضعیت</span><select value={status} onChange={(event) => setStatus(event.target.value as ApplicationStatus | '')}><option value="">همه درخواست‌ها</option><option value="Pending">در انتظار</option><option value="Approved">تأییدشده</option><option value="Rejected">ردشده</option></select></label><span>{applications.data?.length.toLocaleString('fa-IR') ?? '۰'} درخواست</span></section>
    {applications.isPending && <LoadingState label="در حال دریافت درخواست‌های شما…" />}
    {applications.isError && <InlineError message={getErrorMessage(applications.error)} onRetry={() => void applications.refetch()} />}
    {applications.data?.length === 0 && <EmptyState title="درخواستی ثبت نکرده‌اید" message="از صفحه شیفت‌های آزاد، شیفت مناسب را انتخاب کنید." />}
    {Boolean(applications.data?.length) && <section className="application-cards">{applications.data?.map((application) => <article key={application.id}><div><span className={`status-chip ${statusClasses[application.status]}`}>{statusLabels[application.status]}</span><small>درخواست شماره {application.id.toLocaleString('fa-IR')}</small></div><h2>شیفت شماره {application.shiftId.toLocaleString('fa-IR')}</h2><p>ثبت در {formatPersianDate(application.appliedAtUtc)}، ساعت {formatPersianTime(application.appliedAtUtc)}</p>{application.decisionNote && <footer>{application.decisionNote}</footer>}</article>)}</section>}
  </AppShell>;
}
