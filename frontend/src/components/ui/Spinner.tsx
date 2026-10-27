import { Loader2 } from 'lucide-react';
import { cn } from '../../lib/cn';

interface SpinnerProps {
  /** Prečnik ikone u px (lucide `size`). Podrazumevano 16. */
  size?: number;
  className?: string;
}

/**
 * Suptilni rotirajući indikator učitavanja (brand-neutralan, siva).
 * Poštuje `prefers-reduced-motion` preko `motion-reduce:animate-none`.
 */
export function Spinner({ size = 16, className }: SpinnerProps) {
  return (
    <Loader2
      size={size}
      className={cn('animate-spin motion-reduce:animate-none text-gray-400', className)}
      aria-hidden
    />
  );
}

interface UcitavanjeProps {
  /** Tekst pored spinner-a. Podrazumevano „Učitavanje…". */
  text?: string;
  /**
   * Zauzima ceo ekran (centrirano) — za inicijalizaciju aplikacije dok se
   * proverava sesija. Inače se centrira unutar roditelja.
   */
  fullScreen?: boolean;
  className?: string;
}

/**
 * Konzistentan indikator za PRVO učitavanje (kad još nema podataka da bi se
 * prikazali „ispod"). Za pozadinsko osvežavanje koristi se `FetchingOverlay`.
 */
export function Ucitavanje({ text = 'Učitavanje…', fullScreen = false, className }: UcitavanjeProps) {
  return (
    <div
      role="status"
      className={cn(
        'flex items-center justify-center gap-2 text-sm text-gray-500',
        fullScreen ? 'h-screen' : 'py-6',
        className,
      )}
    >
      <Spinner />
      <span>{text}</span>
    </div>
  );
}
