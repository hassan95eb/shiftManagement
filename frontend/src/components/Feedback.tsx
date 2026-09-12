import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { Icon } from './icons';

interface ToastMessage { title: string; message: string }
interface FeedbackContextValue { showToast: (message: string, title?: string) => void }

const FeedbackContext = createContext<FeedbackContextValue | null>(null);

export function FeedbackProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<ToastMessage | null>(null);
  const timer = useRef<number | undefined>(undefined);

  const showToast = useCallback((message: string, title = 'انجام شد') => {
    window.clearTimeout(timer.current);
    setToast({ title, message });
    timer.current = window.setTimeout(() => setToast(null), 4_000);
  }, []);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  return <FeedbackContext.Provider value={{ showToast }}>
    {children}
    {toast && <aside className="toast" role="status" aria-live="polite"><span><Icon name="shield" /></span><div><strong>{toast.title}</strong><small>{toast.message}</small></div><button type="button" onClick={() => setToast(null)} aria-label="بستن اعلان"><Icon name="close" /></button></aside>}
  </FeedbackContext.Provider>;
}

export function useFeedback() {
  const value = useContext(FeedbackContext);
  if (!value) throw new Error('useFeedback must be used inside FeedbackProvider.');
  return value;
}

interface ModalProps {
  open: boolean;
  title: string;
  description?: string;
  children: ReactNode;
  confirmLabel?: string;
  pending?: boolean;
  onClose: () => void;
  onConfirm?: () => void;
}

export function Modal({ open, title, description, children, confirmLabel = 'تأیید و ثبت', pending, onClose, onConfirm }: ModalProps) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (open && !element.open) element.showModal();
    if (!open && element.open) element.close();
  }, [open]);

  return <dialog ref={dialog} className="modal" onCancel={(event) => { event.preventDefault(); onClose(); }} onClose={onClose}>
    <header><div><span>ثبت اطلاعات</span><h2>{title}</h2>{description && <p>{description}</p>}</div><button type="button" onClick={onClose} aria-label="بستن"><Icon name="close" /></button></header>
    <div className="modal__content">{children}</div>
    <footer><button className="secondary-button" type="button" onClick={onClose}>انصراف</button>{onConfirm && <button className="primary-button" type="button" disabled={pending} onClick={onConfirm}>{pending ? 'در حال ثبت…' : confirmLabel}</button>}</footer>
  </dialog>;
}

export function LoadingState({ label = 'در حال دریافت اطلاعات…' }: { label?: string }) {
  return <div className="state-panel" role="status"><i className="spinner" /><strong>{label}</strong></div>;
}

export function EmptyState({ title, message }: { title: string; message: string }) {
  return <div className="state-panel"><Icon name="folder" /><strong>{title}</strong><p>{message}</p></div>;
}
