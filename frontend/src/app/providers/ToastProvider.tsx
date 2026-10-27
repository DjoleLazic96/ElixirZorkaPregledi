/* Fajl izvozi i <ToastProvider> i useToast() hook (context obrazac).
 * Fast-refresh upozorenje je ovde bezopasno. */
/* eslint-disable react-refresh/only-export-components */
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { Toast, type ToastTone } from '../../components/ui/Toast';

/**
 * Globalni toast sistem — fiksni red u donjem desnom uglu, auto-dismiss
 * posle <c>DEFAULT_DURATION_MS</c>, manuelno zatvaranje preko X dugmeta.
 *
 * Koristi se preko <c>useToast()</c> hook-a:
 *
 *   const toast = useToast();
 *   toast.success('Sačuvano.');
 *   toast.error('Došlo je do greške…');
 *   toast.info('Informacija…', { duration: 8000 });
 */

const DEFAULT_DURATION_MS = 4000;

interface ToastOptions {
  /** Trajanje u ms pre auto-dismiss-a. Default 4s. */
  duration?: number;
}

interface ToastApi {
  success: (message: string, opts?: ToastOptions) => void;
  error: (message: string, opts?: ToastOptions) => void;
  info: (message: string, opts?: ToastOptions) => void;
}

interface ToastItem {
  id: number;
  tone: ToastTone;
  message: string;
}

const ToastContext = createContext<ToastApi | null>(null);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);
  // Drzimo aktivne timeout handle-ove da bismo ih ocistili pri unmount-u
  // ili pri ranijem manuelnom dismiss-u istog toast-a.
  const timeoutsRef = useRef<Map<number, ReturnType<typeof setTimeout>>>(new Map());
  const nextIdRef = useRef(1);

  const dismiss = useCallback((id: number) => {
    setItems((prev) => prev.filter((t) => t.id !== id));
    const handle = timeoutsRef.current.get(id);
    if (handle) {
      clearTimeout(handle);
      timeoutsRef.current.delete(id);
    }
  }, []);

  const push = useCallback(
    (tone: ToastTone, message: string, opts?: ToastOptions) => {
      const id = nextIdRef.current++;
      setItems((prev) => [...prev, { id, tone, message }]);
      const handle = setTimeout(() => {
        dismiss(id);
      }, opts?.duration ?? DEFAULT_DURATION_MS);
      timeoutsRef.current.set(id, handle);
    },
    [dismiss]
  );

  // Cleanup svih pending timeout-a kad se provider unmount-uje.
  useEffect(() => {
    const timeouts = timeoutsRef.current;
    return () => {
      timeouts.forEach((h) => clearTimeout(h));
      timeouts.clear();
    };
  }, []);

  const api = useMemo<ToastApi>(
    () => ({
      success: (msg, opts) => push('success', msg, opts),
      error: (msg, opts) => push('error', msg, opts),
      info: (msg, opts) => push('info', msg, opts),
    }),
    [push]
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div
        // Container ne hvata pointer events — samo pojedinačni toast-ovi.
        className="pointer-events-none fixed bottom-4 right-4 z-50 flex flex-col gap-2"
        aria-live="polite"
        aria-atomic="false"
      >
        {items.map((t) => (
          <Toast
            key={t.id}
            tone={t.tone}
            message={t.message}
            onDismiss={() => dismiss(t.id)}
          />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

/** Hook — vidi <c>ToastProvider</c> komentar za primere upotrebe. */
export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (ctx === null) {
    throw new Error('useToast mora biti unutar <ToastProvider>.');
  }
  return ctx;
}
