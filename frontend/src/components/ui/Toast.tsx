import { CheckCircle2, Info, X, XCircle } from 'lucide-react';
import { cn } from '../../lib/cn';

/**
 * Tonovi toast-a — success (zelena, brand primary), error (crvena),
 * info (siva, neutralno).
 */
export type ToastTone = 'success' | 'error' | 'info';

export interface ToastProps {
  tone: ToastTone;
  message: string;
  onDismiss: () => void;
}

/**
 * Jedan toast u donjem desnom uglu. Animira se ulazak (slide-in s desna),
 * dismiss-uje se klikom na X dugme ili automatski iz <ToastProvider/> nakon
 * vremena trajanja.
 */
export function Toast({ tone, message, onDismiss }: ToastProps) {
  const Icon = tone === 'error' ? XCircle : tone === 'success' ? CheckCircle2 : Info;
  return (
    <div
      role="status"
      className={cn(
        'pointer-events-auto flex w-80 items-start gap-2 rounded-md border px-3 py-2 text-sm shadow-lg',
        'animate-toast-in',
        tone === 'success' && 'border-primary-200 bg-primary-50 text-primary-800',
        tone === 'error' && 'border-red-200 bg-red-50 text-red-700',
        tone === 'info' && 'border-gray-200 bg-white text-gray-800'
      )}
    >
      <Icon size={16} className="mt-0.5 shrink-0" />
      <div className="flex-1">{message}</div>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="Zatvori notifikaciju"
        className="opacity-60 transition-opacity hover:opacity-100"
      >
        <X size={14} />
      </button>
    </div>
  );
}
