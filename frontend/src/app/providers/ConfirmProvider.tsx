/* Fajl izvozi i <ConfirmProvider> i useConfirm() hook (context obrazac).
 * Fast-refresh upozorenje je ovde bezopasno. */
/* eslint-disable react-refresh/only-export-components */
import {
  createContext,
  useCallback,
  useContext,
  useState,
  type ReactNode,
} from 'react';
import {
  ConfirmDialog,
  type ConfirmTone,
} from '../../components/ui/ConfirmDialog';

/**
 * Opcije za poziv <c>confirm()</c>-a. Title je obavezan (kratki naslov,
 * npr. „Deaktivacija korisnika"); message je objašnjenje šta se dešava i
 * obavezno kaže da se akcija ne može poništiti kad je to slučaj.
 */
export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: ConfirmTone;
}

/**
 * <c>useConfirm()</c> vraća funkciju koja prikazuje stilizovani dijalog
 * i vraća <c>Promise&lt;boolean&gt;</c> — <c>true</c> ako je korisnik kliknuo
 * "Potvrdi", <c>false</c> ako je odustao.
 *
 * Primer korišćenja:
 *   const confirm = useConfirm();
 *   if (await confirm({ title: 'Brisanje', message: 'Sigurno?' })) {
 *     await deleteThing();
 *   }
 */
type ConfirmFn = (opts: ConfirmOptions) => Promise<boolean>;

const ConfirmContext = createContext<ConfirmFn | null>(null);

/**
 * Provider koji se mount-uje globalno (jedan za celu aplikaciju). Drži
 * stanje trenutnog pitanja i Promise resolver-a. Konkretni dijalog
 * se renderuje samo dok je pitanje aktivno.
 */
export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [pending, setPending] = useState<{
    opts: ConfirmOptions;
    resolve: (value: boolean) => void;
  } | null>(null);

  const confirm = useCallback<ConfirmFn>(
    (opts) =>
      new Promise<boolean>((resolve) => {
        setPending({ opts, resolve });
      }),
    []
  );

  function handleConfirm() {
    if (pending) {
      pending.resolve(true);
      setPending(null);
    }
  }

  function handleCancel() {
    if (pending) {
      pending.resolve(false);
      setPending(null);
    }
  }

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      <ConfirmDialog
        open={pending !== null}
        title={pending?.opts.title ?? ''}
        message={pending?.opts.message ?? ''}
        confirmLabel={pending?.opts.confirmLabel}
        cancelLabel={pending?.opts.cancelLabel}
        tone={pending?.opts.tone}
        onConfirm={handleConfirm}
        onCancel={handleCancel}
      />
    </ConfirmContext.Provider>
  );
}

/** Hook — vidi komentar nad <c>ConfirmFn</c> tipom za primer. */
export function useConfirm(): ConfirmFn {
  const ctx = useContext(ConfirmContext);
  if (ctx === null) {
    throw new Error('useConfirm mora biti unutar <ConfirmProvider>.');
  }
  return ctx;
}
