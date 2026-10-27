import { cn } from '../../lib/cn';

interface ProgressTrakaProps {
  /** 0–100; `null` = nedostupno (prazna siva traka). */
  percent: number | null;
  /** Prag za žuto. */
  warnAt: number;
  /** Prag za crveno. */
  dangerAt: number;
  /**
   * `false` (default): VIŠE je lošije (CPU, RAM) — žuto od `warnAt`, crveno od `dangerAt`.
   * `true`: MANJE je lošije (slobodan disk) — žuto do `warnAt`, crveno ispod `dangerAt`.
   */
  inverted?: boolean;
  'aria-label': string;
  className?: string;
}

type Ton = 'ok' | 'upozorenje' | 'kriticno' | 'nepoznato';

/** Boja po pragovima — samo vizuelni signal, bez alarma (blueprint 10 §2). */
function ton(percent: number | null, warnAt: number, dangerAt: number, inverted: boolean): Ton {
  if (percent === null || Number.isNaN(percent)) return 'nepoznato';
  if (inverted) {
    if (percent < dangerAt) return 'kriticno';
    if (percent <= warnAt) return 'upozorenje';
    return 'ok';
  }
  if (percent >= dangerAt) return 'kriticno';
  if (percent >= warnAt) return 'upozorenje';
  return 'ok';
}

const BOJA: Record<Ton, string> = {
  ok: 'bg-primary-500',
  upozorenje: 'bg-amber-500',
  kriticno: 'bg-red-500',
  nepoznato: 'bg-gray-300',
};

/** Tanka traka napretka u boji praga; vrednost i tekst ispisuje roditelj. */
export function ProgressTraka({
  percent,
  warnAt,
  dangerAt,
  inverted = false,
  'aria-label': ariaLabel,
  className,
}: ProgressTrakaProps) {
  const t = ton(percent, warnAt, dangerAt, inverted);
  const width = percent === null ? 0 : Math.min(100, Math.max(0, percent));
  return (
    <div
      role="progressbar"
      aria-label={ariaLabel}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={percent ?? undefined}
      aria-valuetext={percent === null ? 'nedostupno' : undefined}
      data-ton={t}
      className={cn('h-2 w-full overflow-hidden rounded-full bg-gray-100', className)}
    >
      <div
        className={cn('h-full rounded-full transition-[width] motion-reduce:transition-none', BOJA[t])}
        style={{ width: `${width}%` }}
      />
    </div>
  );
}
