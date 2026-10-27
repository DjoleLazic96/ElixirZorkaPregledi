import type { ReactNode } from 'react';
import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react';
import type { Smer } from '../../api/types';
import { cn } from '../../lib/cn';

export interface SortableThProps<K extends string> {
  /** Identifikator kolone za backend (`sort=` parametar). */
  column: K;
  /** Trenutno aktivna kolona sortiranja. */
  current: K | undefined;
  direction: Smer;
  onSort: (column: K, direction: Smer) => void;
  className?: string;
  children: ReactNode;
}

/**
 * Zaglavlje kolone koje sortira (server-side). Klik: neaktivna → asc; asc → desc;
 * desc → asc. Stanje drži roditelj (URL `sort`/`smer`). `aria-sort` stoji na
 * `<th>`, gde ga ARIA i očekuje.
 */
export function SortableTh<K extends string>({
  column,
  current,
  direction,
  onSort,
  className,
  children,
}: SortableThProps<K>) {
  const active = current === column;
  const nextDirection: Smer = active && direction === 'asc' ? 'desc' : 'asc';

  return (
    <th
      className={cn('px-3 py-2 text-left', className)}
      aria-sort={active ? (direction === 'asc' ? 'ascending' : 'descending') : 'none'}
    >
      <button
        type="button"
        onClick={() => onSort(column, nextDirection)}
        className={cn(
          '-mx-1 inline-flex items-center gap-1 rounded px-1 text-xs uppercase tracking-wide transition-colors',
          'hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500',
          active ? 'font-semibold text-gray-900' : 'text-gray-500',
        )}
        title={
          active
            ? `Sortirano ${direction === 'asc' ? 'rastuće' : 'opadajuće'}. Kliknite za promenu smera.`
            : 'Kliknite za sortiranje'
        }
      >
        <span>{children}</span>
        {active ? (
          direction === 'asc' ? (
            <ChevronUp size={12} className="text-primary-600" aria-hidden />
          ) : (
            <ChevronDown size={12} className="text-primary-600" aria-hidden />
          )
        ) : (
          <ChevronsUpDown size={12} className="text-gray-400" aria-hidden />
        )}
      </button>
    </th>
  );
}
