import type { ReactNode } from 'react';
import { cn } from '../../lib/cn';
import { Spinner } from './Spinner';

interface FetchingOverlayProps {
  /**
   * Da li je pozadinsko osvežavanje u toku (`isFetching` uz već prikazane
   * podatke). Kad je `true`, sadržaj se suptilno zatamni i onemoguće za klik,
   * a u gornjem desnom uglu se pojavi mali spinner sa oznakom „Osvežavanje…".
   */
  fetching: boolean;
  children: ReactNode;
  className?: string;
}

/**
 * Suptilni sloj za pozadinsko osvežavanje (background refetch).
 *
 * Umesto da `keepPreviousData` tiho zameni podatke (ili da ekran blesne u
 * prazno), ovaj wrapper daje diskretan vizuelni znak da se podaci ažuriraju:
 *  - sadržaj `opacity-60` + `pointer-events-none` dok traje fetch,
 *  - mali spinner sa tekstom „Osvežavanje…" u uglu.
 *
 * Nema layout jump-a — overlay je apsolutno pozicioniran preko sadržaja koji
 * ostaje na svom mestu. Tranzicija opaciteta je kratka i poštuje
 * `prefers-reduced-motion` (`motion-reduce:transition-none`).
 */
export function FetchingOverlay({ fetching, children, className }: FetchingOverlayProps) {
  return (
    <div className={cn('relative', className)}>
      <div
        className={cn(
          'transition-opacity duration-200 motion-reduce:transition-none',
          fetching && 'pointer-events-none select-none opacity-60',
        )}
        aria-busy={fetching}
      >
        {children}
      </div>

      {fetching && (
        <div
          role="status"
          className="pointer-events-none absolute right-2 top-2 flex items-center gap-1.5 rounded-full border border-gray-200 bg-white/90 px-2.5 py-1 text-xs text-gray-500 shadow-sm backdrop-blur-sm"
        >
          <Spinner size={14} />
          <span>Osvežavanje…</span>
        </div>
      )}
    </div>
  );
}
