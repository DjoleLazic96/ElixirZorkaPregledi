import { cn } from '../../lib/cn';
import { godinaOpcije, mesecOpcije } from '../../lib/period';
import { Select } from './Select';

interface MesecPickerProps {
  year: number;
  /** 1–12. */
  month: number;
  onChange: (year: number, month: number) => void;
  /** Opcije godina; podrazumevano tekuća ±1 (`godinaOpcije()`), šire po potrebi (`godinaOpcijeSire()`). */
  yearOptions?: ReadonlyArray<{ value: number; label: string }>;
  disabled?: boolean;
  className?: string;
}

/**
 * Izbor meseca: dva `Select`-a (mesec sa imenima, godina). Vrednost u stanju je
 * `{ godina, mesec }`; na API ide `periodKljuc(g, m)` ili ruta `/{g}/{m}`.
 */
export function MesecPicker({
  year,
  month,
  onChange,
  yearOptions = godinaOpcije(),
  disabled,
  className,
}: MesecPickerProps) {
  // Ako trenutna godina nije u ponuđenom opsegu (npr. stari zapis), dodaj je da
  // Select ne prikaže placeholder umesto stvarne vrednosti.
  const options = yearOptions.some((g) => g.value === year)
    ? yearOptions
    : [...yearOptions, { value: year, label: String(year) }].sort((a, b) => a.value - b.value);

  return (
    <div className={cn('flex gap-2', className)}>
      <Select
        aria-label="Mesec"
        className="w-40"
        value={month}
        options={mesecOpcije()}
        disabled={disabled}
        onChange={(m) => m != null && onChange(year, m)}
      />
      <Select
        aria-label="Godina"
        className="w-28"
        value={year}
        options={options}
        disabled={disabled}
        onChange={(g) => g != null && onChange(g, month)}
      />
    </div>
  );
}
