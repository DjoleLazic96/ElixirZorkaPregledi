import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react';
import { mnozina } from '../../lib/broj';
import { Button } from './Button';
import { Select } from './Select';

/**
 * Server-side pager za `StranicaDto<T>` odgovore.
 *
 * Prikazuje „Prikaz X–Y od Z stavki", izbor veličine stranice i dugmad
 * prva | prethodna | sledeća | poslednja. Komponenta je prezentaciona — stanje
 * (`page`, `pageSize`) drži roditelj (po pravilu u URL-u) i šalje ga API-ju.
 * Props su engleski (code-level, blueprint 03 §4); srpski su samo DTO-i i tekst.
 */
export interface PaginationProps {
  page: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  pageSizeOptions?: number[];
  /** Oblici imenice za srpsku množinu („1 zapis / 2 zapisa / 5 zapisa"). */
  itemLabel?: { jednina: string; paukal: string; mnozina: string };
}

const DEFAULT_PAGE_SIZES = [25, 50, 100, 200];
const DEFAULT_LABEL = { jednina: 'stavka', paukal: 'stavke', mnozina: 'stavki' };

export function Pagination({
  page,
  pageSize,
  total,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = DEFAULT_PAGE_SIZES,
  itemLabel = DEFAULT_LABEL,
}: PaginationProps) {
  const totalPages = pageSize === 0 ? 0 : Math.max(1, Math.ceil(total / pageSize));
  const onFirst = page <= 1;
  const onLast = page >= totalPages;

  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);

  return (
    <div className="mt-3 flex flex-wrap items-center justify-between gap-3 border-t border-gray-100 pt-3 text-sm text-gray-700">
      <span>
        Prikaz{' '}
        <strong className="tabular-nums">
          {from}–{to}
        </strong>{' '}
        od <strong className="tabular-nums">{total}</strong> {mnozina(total, itemLabel)}
      </span>

      <div className="flex items-center gap-3">
        <div className="flex items-center gap-2 text-xs text-gray-600">
          <span>Po stranici:</span>
          <Select
            className="h-9 min-w-20"
            aria-label="Broj stavki po stranici"
            value={pageSize}
            options={pageSizeOptions.map((v) => ({ value: v, label: String(v) }))}
            onChange={(v) => v != null && onPageSizeChange(v)}
          />
        </div>

        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onPageChange(1)}
            disabled={onFirst}
            aria-label="Prva stranica"
            title="Prva stranica"
          >
            <ChevronsLeft size={14} />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onPageChange(page - 1)}
            disabled={onFirst}
            aria-label="Prethodna stranica"
            title="Prethodna stranica"
          >
            <ChevronLeft size={14} />
          </Button>

          <span className="whitespace-nowrap px-2 text-xs text-gray-600">
            Stranica <strong className="tabular-nums">{page}</strong> od{' '}
            <strong className="tabular-nums">{totalPages || 1}</strong>
          </span>

          <Button
            variant="ghost"
            size="sm"
            onClick={() => onPageChange(page + 1)}
            disabled={onLast}
            aria-label="Sledeća stranica"
            title="Sledeća stranica"
          >
            <ChevronRight size={14} />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onPageChange(totalPages)}
            disabled={onLast}
            aria-label="Poslednja stranica"
            title="Poslednja stranica"
          >
            <ChevronsRight size={14} />
          </Button>
        </div>
      </div>
    </div>
  );
}
