import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AppShell } from '../components/AppShell';
import { EmptyState, LoadingState, Modal, useFeedback } from '../components/Feedback';
import { FieldError, InlineError, PageHeader } from '../components/Page';
import { Icon } from '../components/icons';
import { projectsApi } from '../lib/management-api';
import { formatPersianDate, getErrorMessage } from '../lib/presentation';
import type { Project } from '../types/management';

interface ProjectFormProps {
  project: Project | null | undefined;
  pending: boolean;
  serverError: string;
  onClose: () => void;
  onSave: (name: string) => void;
}

function ProjectFormModal({ project, pending, serverError, onClose, onSave }: ProjectFormProps) {
  const [name, setName] = useState('');
  const [touched, setTouched] = useState(false);
  useEffect(() => { setName(project?.name ?? ''); setTouched(false); }, [project]);
  const nameError = touched && !name.trim() ? 'نام پروژه الزامی است.' : '';

  return <Modal open={project !== undefined} title={project ? 'ویرایش پروژه' : 'ایجاد پروژه'} description="نام پروژه باید در حساب شما یکتا باشد." pending={pending} confirmLabel={project ? 'ذخیره تغییرات' : 'ایجاد پروژه'} onClose={onClose} onConfirm={() => { setTouched(true); if (name.trim()) onSave(name.trim()); }}>
    <label className="form-field"><span>نام پروژه</span><input value={name} onChange={(event) => setName(event.target.value)} maxLength={128} autoFocus /><FieldError message={nameError} /></label>
    {serverError && <InlineError message={serverError} />}
  </Modal>;
}

function ProjectMembersModal({ project, onClose }: { project: Project | null; onClose: () => void }) {
  const members = useQuery({
    queryKey: ['projects', project?.id, 'experts'],
    queryFn: () => projectsApi.experts(project!.id),
    enabled: Boolean(project),
  });
  return <Modal open={Boolean(project)} title={`کارشناسان ${project?.name ?? ''}`} description="اعضای متصل به این پروژه" onClose={onClose}>
    {members.isPending && <LoadingState />}
    {members.isError && <InlineError message={getErrorMessage(members.error)} onRetry={() => void members.refetch()} />}
    {members.data?.length === 0 && <EmptyState title="کارشناسی متصل نیست" message="اتصال کارشناس به پروژه از صفحه کارشناسان انجام می‌شود." />}
    {Boolean(members.data?.length) && <div className="member-list">{members.data?.map((expert) => <div key={expert.id}><span className="avatar">{expert.fullName.slice(0, 2)}</span><span><strong>{expert.fullName}</strong><small>شناسه کارشناس: {expert.id.toLocaleString('fa-IR')}</small></span><span className={`status-chip ${expert.isActive ? 'active' : 'inactive'}`}>{expert.isActive ? 'فعال' : 'غیرفعال'}</span></div>)}</div>}
  </Modal>;
}

export function ProjectsPage() {
  const queryClient = useQueryClient();
  const { showToast } = useFeedback();
  const [formProject, setFormProject] = useState<Project | null | undefined>(undefined);
  const [deleteProject, setDeleteProject] = useState<Project | null>(null);
  const [membersProject, setMembersProject] = useState<Project | null>(null);
  const [operationError, setOperationError] = useState('');

  const projects = useQuery({ queryKey: ['projects'], queryFn: projectsApi.list });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['projects'] });

  const createProject = useMutation({
    mutationFn: projectsApi.create,
    onSuccess: async () => { await refresh(); setFormProject(undefined); showToast('پروژه جدید ایجاد شد.'); },
  });
  const updateProject = useMutation({
    mutationFn: ({ id, name, isActive }: { id: number; name: string; isActive: boolean }) => projectsApi.update(id, { name, isActive }),
    onSuccess: async (_, variables) => { await refresh(); setFormProject(undefined); showToast(variables.isActive ? 'اطلاعات پروژه ذخیره شد.' : 'پروژه غیرفعال شد.'); },
  });
  const removeProject = useMutation({
    mutationFn: projectsApi.remove,
    onSuccess: async () => { await refresh(); setDeleteProject(null); showToast('پروژه حذف شد.'); },
  });

  const formError = createProject.error ?? updateProject.error;

  return <AppShell>
    <PageHeader title="پروژه‌ها" description="ایجاد پروژه و مدیریت وضعیت پروژه‌های شما" action={<button className="primary-button" type="button" onClick={() => { setOperationError(''); setFormProject(null); }}><Icon name="folder" />ایجاد پروژه</button>} />

    {operationError && <InlineError message={operationError} />}
    {projects.isPending && <LoadingState label="در حال دریافت پروژه‌ها…" />}
    {projects.isError && <InlineError message={getErrorMessage(projects.error)} onRetry={() => void projects.refetch()} />}
    {projects.data?.length === 0 && <EmptyState title="هنوز پروژه‌ای ندارید" message="اولین پروژه را ایجاد کنید تا بتوانید کارشناس و شیفت به آن اضافه کنید." />}
    {Boolean(projects.data?.length) && <section className="data-card"><header><div><h2>فهرست پروژه‌ها</h2><p>{projects.data?.length.toLocaleString('fa-IR')} پروژه ثبت شده</p></div></header><div className="table-scroll"><table><thead><tr><th>نام پروژه</th><th>تاریخ ایجاد</th><th>وضعیت</th><th>کارشناسان</th><th>اقدام</th></tr></thead><tbody>{projects.data?.map((project) => <tr key={project.id}><td><strong>{project.name}</strong><small>شناسه {project.id.toLocaleString('fa-IR')}</small></td><td>{formatPersianDate(project.createdAtUtc)}</td><td><button className={`status-toggle ${project.isActive ? 'is-active' : ''}`} type="button" disabled={updateProject.isPending} onClick={() => { setOperationError(''); updateProject.mutate({ id: project.id, name: project.name, isActive: !project.isActive }, { onError: (error) => setOperationError(getErrorMessage(error)) }); }}><i />{project.isActive ? 'فعال' : 'غیرفعال'}</button></td><td><button className="text-button" type="button" onClick={() => setMembersProject(project)}>مشاهده اعضا</button></td><td><div className="row-actions"><button className="secondary-button compact" type="button" onClick={() => { setOperationError(''); setFormProject(project); }}>ویرایش</button><button className="danger-button compact" type="button" onClick={() => { setOperationError(''); setDeleteProject(project); }}>حذف</button></div></td></tr>)}</tbody></table></div></section>}

    <ProjectFormModal project={formProject} pending={createProject.isPending || updateProject.isPending} serverError={formError ? getErrorMessage(formError) : ''} onClose={() => { setFormProject(undefined); createProject.reset(); updateProject.reset(); }} onSave={(name) => formProject ? updateProject.mutate({ id: formProject.id, name, isActive: formProject.isActive }) : createProject.mutate({ name })} />
    <ProjectMembersModal project={membersProject} onClose={() => setMembersProject(null)} />
    <Modal open={Boolean(deleteProject)} title="حذف پروژه" description="این عملیات فقط برای پروژه‌ی بدون شیفت امکان‌پذیر است." confirmLabel="حذف پروژه" pending={removeProject.isPending} onClose={() => { setDeleteProject(null); removeProject.reset(); }} onConfirm={() => deleteProject && removeProject.mutate(deleteProject.id, { onError: (error) => setOperationError(getErrorMessage(error)) })}>
      <p className="confirm-copy">آیا از حذف پروژه‌ی <strong>{deleteProject?.name}</strong> مطمئن هستید؟ اگر پروژه شیفت داشته باشد، سرور حذف را متوقف می‌کند و می‌توانید آن را غیرفعال کنید.</p>
      {removeProject.isError && <InlineError message={getErrorMessage(removeProject.error)} />}
    </Modal>
  </AppShell>;
}
