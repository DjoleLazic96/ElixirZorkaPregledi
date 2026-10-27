import { useEffect } from 'react';
import { AlertTriangle } from 'lucide-react';
import { cn } from '../../lib/cn';
import { Button } from './Button';
import { Modal } from './Modal';

export type ConfirmTone = 'danger' | 'normal';

export interface ConfirmDialogProps {
  open: boolean;
  /** Naslov dijaloga (npr. „Deaktivacija korisnika"). */
  title: string;
  /** Tekst pitanja (može biti više redova). */
  message: string;
  /** Tekst potvrdnog dugmeta. Default: "Potvrdi". */
  confirmLabel?: string;
  /** Tekst dugmeta za odustajanje. Default: "Odustani". */
  cancelLabel?: string;
  /** "danger" = crveno potvrdno dugme (za brisanja); "normal" = primary. */
  tone?: ConfirmTone;
  /** Da li je akcija u toku (mutacija). Onemogućava dugmad i pokazuje "Čuvanje…". */
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Lep dijalog za potvrdu akcije, u stilu aplikacije (umesto native
 * <c>window.confirm</c>). Po default-u je tonalitet "danger" da se vizuelno
 * razlikuje od običnih modal-a (crveno potvrdno dugme + alert ikonica).
 *
 * Razlika u odnosu na običan <see cref="Modal"/>:
 *   - Standardna 2-dugme zaglavlja (Odustani + Potvrdi)
 *   - Enter potvrđuje, Esc odustaje (uz Modal-ovo ponašanje za Esc)
 */
export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Potvrdi',
  cancelLabel  = 'Odustani',
  tone = 'danger',
  loading = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  // Enter potvrđuje (samo dok je modal otvoren).
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Enter' && !loading) {
        e.preventDefault();
        onConfirm();
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, loading, onConfirm]);

  return (
    <Modal open={open} title={title} onClose={onCancel} widthClass="max-w-md">
      <div className="space-y-4">
        <div className="flex gap-3">
          <div
            className={cn(
              'flex h-9 w-9 shrink-0 items-center justify-center rounded-full',
              tone === 'danger'
                ? 'bg-red-100 text-red-600'
                : 'bg-primary-100 text-primary-600'
            )}
          >
            <AlertTriangle size={18} />
          </div>
          <div className="text-sm leading-relaxed text-gray-700">
            {message}
          </div>
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <Button variant="ghost" onClick={onCancel} disabled={loading}>
            {cancelLabel}
          </Button>
          <Button
            variant={tone === 'danger' ? 'danger' : 'primary'}
            onClick={onConfirm}
            disabled={loading}
            autoFocus
          >
            {loading ? 'Čuvanje…' : confirmLabel}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
