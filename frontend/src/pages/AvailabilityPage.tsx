import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AppShell } from '../components/AppShell';
import { EmptyState, LoadingState, Modal, useFeedback } from '../components/Feedback';
import { FieldError, InlineError, PageHeader } from '../components/Page';
import { PersianDateTimeField } from '../components/PersianDateTime';
import { Icon } from '../components/icons';
import { availabilityApi } from '../lib/scheduling-api';
import { formatDuration, formatPersianDate, formatPersianTime, getErrorMessage, tehranLocalToUtc, toDateTimeLocalValue } from '../lib/presentation';
import type { Availability } from '../types/scheduling';

function AvailabilityForm({ window, pending, error, onClose, onSave }: { window: Availability | null | undefined; pending: boolean; error: string; onClose: () => void; onSave: (startUtc: string, endUtc: string) => void }) {
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [submitted, setSubmitted] = useState(false);
  useEffect(() => { setStart(window ? toDateTimeLocalValue(window.startUtc) : ''); setEnd(window ? toDateTimeLocalValue(window.endUtc) : ''); setSubmitted(false); }, [window]);
  const invalidRange = Boolean(start && end && new Date(start) >= new Date(end));
  return <Modal open={window !== undefined} title={window ? 'ویرایش بازه دسترسی' : 'افزودن بازه دسترسی'} description="بازه‌های هم‌پوشان یا متصل، خودکار با هم ادغام می‌شوند." confirmLabel={window ? 'ذخیره تغییرات' : 'ثبت بازه'} pending={pending} onClose={onClose} onConfirm={() => { setSubmitted(true); if (start && end && !invalidRange) onSave(tehranLocalToUtc(start), tehranLocalToUtc(end)); }}>
    <div className="form-grid"><label className="form-field full"><span>شروع دسترسی</span><PersianDateTimeField value={start} onChange={setStart} /><FieldError message={submitted && !start ? 'زمان شروع الزامی است.' : ''} /></label><label className="form-field full"><span>پایان دسترسی</span><PersianDateTimeField value={end} onChange={setEnd} /><FieldError message={submitted && !end ? 'زمان پایان الزامی است.' : ''} /></label></div>
    {invalidRange && <InlineError message="زمان پایان باید بعد از زمان شروع باشد." />}{error && <InlineError message={error} />}
  </Modal>;
}

export function AvailabilityPage() {
  const queryClient = useQueryClient();
  const { showToast } = useFeedback();
  const [formWindow, setFormWindow] = useState<Availability | null | undefined>(undefined);
  const [deleteWindow, setDeleteWindow] = useState<Availability | null>(null);
  const windows = useQuery({ queryKey: ['availability'], queryFn: availabilityApi.list });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['availability'] });
  const create = useMutation({ mutationFn: availabilityApi.create, onSuccess: async () => { await refresh(); setFormWindow(undefined); showToast('بازه دسترسی ثبت و با بازه‌های مجاور ادغام شد.'); } });
  const update = useMutation({ mutationFn: ({ id, startUtc, endUtc }: { id: number; startUtc: string; endUtc: string }) => availabilityApi.update(id, { startUtc, endUtc }), onSuccess: async () => { await refresh(); setFormWindow(undefined); showToast('بازه دسترسی به‌روزرسانی شد.'); } });
  const remove = useMutation({ mutationFn: availabilityApi.remove, onSuccess: async () => { await refresh(); setDeleteWindow(null); showToast('بازه دسترسی حذف شد.'); } });
  const mutationError = create.error ?? update.error;

  return <AppShell>
    <PageHeader title="اعلام دسترسی" description="زمان‌هایی را ثبت کنید که امکان پذیرش شیفت دارید" action={<button className="primary-button" type="button" onClick={() => setFormWindow(null)}><Icon name="clock" />افزودن بازه</button>} />
    <div className="info-banner"><Icon name="shield" /><span><strong>ادغام خودکار بازه‌ها</strong><small>اگر دو بازه به هم برسند یا هم‌پوشانی داشته باشند، سرور آن‌ها را به یک بازه تبدیل می‌کند.</small></span></div>
    {windows.isPending && <LoadingState label="در حال دریافت بازه‌های دسترسی…" />}
    {windows.isError && <InlineError message={getErrorMessage(windows.error)} onRetry={() => void windows.refetch()} />}
    {windows.data?.length === 0 && <EmptyState title="بازه‌ای ثبت نشده است" message="برای مشاهده و درخواست شیفت‌های مناسب، زمان‌های آزاد خود را ثبت کنید." />}
    {Boolean(windows.data?.length) && <section className="availability-list">{windows.data?.map((window) => <article key={window.id}><div className="date-tile"><strong>{new Intl.DateTimeFormat('fa-IR-u-ca-persian', { timeZone: 'Asia/Tehran', day: 'numeric' }).format(new Date(window.startUtc))}</strong><small>{new Intl.DateTimeFormat('fa-IR-u-ca-persian', { timeZone: 'Asia/Tehran', month: 'short' }).format(new Date(window.startUtc))}</small></div><div><h2>{formatPersianDate(window.startUtc)}</h2><p dir="ltr">{formatPersianTime(window.startUtc)} – {formatPersianTime(window.endUtc)}</p></div><span className="duration-chip">{formatDuration(window.startUtc, window.endUtc)}</span><div className="row-actions"><button className="secondary-button compact" type="button" onClick={() => { create.reset(); update.reset(); setFormWindow(window); }}>ویرایش</button><button className="danger-button compact" type="button" onClick={() => setDeleteWindow(window)}>حذف</button></div></article>)}</section>}
    <AvailabilityForm window={formWindow} pending={create.isPending || update.isPending} error={mutationError ? getErrorMessage(mutationError) : ''} onClose={() => { setFormWindow(undefined); create.reset(); update.reset(); }} onSave={(startUtc, endUtc) => formWindow ? update.mutate({ id: formWindow.id, startUtc, endUtc }) : create.mutate({ startUtc, endUtc })} />
    <Modal open={Boolean(deleteWindow)} title="حذف بازه دسترسی" description="اگر این بازه تنها پوشش یک شیفت تأییدشده باشد، حذف متوقف می‌شود." confirmLabel="حذف بازه" pending={remove.isPending} onClose={() => { setDeleteWindow(null); remove.reset(); }} onConfirm={() => deleteWindow && remove.mutate(deleteWindow.id)}><p className="confirm-copy">آیا از حذف بازه‌ی <strong>{deleteWindow ? formatPersianDate(deleteWindow.startUtc) : ''}</strong> مطمئن هستید؟</p>{remove.isError && <InlineError message={getErrorMessage(remove.error)} />}</Modal>
  </AppShell>;
}
