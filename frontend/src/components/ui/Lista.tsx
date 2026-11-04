import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { cn } from '../../lib/cn';

/** Spisak stavki po mockup-u (`.list`): razmak 8 px između stavki. */
export function Lista({ children, className }: { children: ReactNode; className?: string }) {
  return <ul className={cn('flex flex-col gap-2', className)}>{children}</ul>;
}

interface StavkaListeProps {
  /** Emoji ikonica levo (kao u mockup-u), opciono. */
  ikona?: string;
  /** Broj (DP1-2026-0142, A-2026-0031…) iznad naslova, zelen. */
  broj?: string;
  naslov: ReactNode;
  opis?: ReactNode;
  /** Oznake i dugmad (prelaze u novi red na uskom ekranu). */
  desno?: ReactNode;
  /** Ruta koju stavka otvara. */
  to?: string;
  /** Moja stavka — svetlo zelena linija levo (mockup `.it.mi`). */
  moje?: boolean;
}

/**
 * Stavka liste po mockup-u (`.list .it`): svetla podloga, zaobljenje 14 px, „›“ desno kad vodi negde; na prelaz zeleni ton.
 * Cela stavka je klikabilna preko „rastegnutog“ linka na broju (ili naslovu), pa dugmad u `desno` ostaju zasebna (bez ugnežđenih linkova).
 */
export function StavkaListe({ ikona, broj, naslov, opis, desno, to, moje }: StavkaListeProps) {
  const rastegnut = 'after:absolute after:inset-0 after:rounded-[14px] focus:outline-none focus-visible:after:ring-2 focus-visible:after:ring-primary-500';
  const brojEl = broj && (to
    ? <Link to={to} className={cn('block text-[11px] font-bold tracking-wide text-primary-500', rastegnut)}>{broj}</Link>
    : <span className="block text-[11px] font-bold tracking-wide text-primary-500">{broj}</span>);
  const naslovEl = to && !broj
    ? <Link to={to} className={cn('block break-words font-semibold text-gray-800', rastegnut)}>{naslov}</Link>
    : <b className="block break-words font-semibold text-gray-800">{naslov}</b>;
  return (
    <li
      className={cn(
        'relative flex flex-wrap items-start gap-x-2.5 gap-y-1.5 rounded-[14px] border border-gray-200 bg-[#f7f8f6] py-2.5 pl-3 text-sm transition-colors',
        to ? 'pr-8 hover:border-primary-200 hover:bg-primary-50' : 'pr-3',
        moje && 'border-l-4 border-l-accent-500',
      )}
    >
      {ikona && <span aria-hidden className="text-lg leading-tight">{ikona}</span>}
      <span className="min-w-0 flex-[1_1_calc(100%-40px)]">
        {brojEl}
        {naslovEl}
        {opis && <span className="block text-xs text-gray-500">{opis}</span>}
      </span>
      {desno && <span className="relative z-10 flex flex-wrap items-center gap-1.5">{desno}</span>}
      {to && <span aria-hidden className="pointer-events-none absolute right-3 top-2 text-lg text-gray-500">›</span>}
    </li>
  );
}
