import { forwardRef, useMemo, useState } from 'react';
import { Calendar, ChevronLeft, ChevronRight, X } from 'lucide-react';
import { AnchoredDropdown } from './AnchoredDropdown';
import { cn } from '../../lib/cn';
import { danasYmd, formatirajDatum, formatirajYmd, parsirajYmd } from '../../lib/datum';
import { MESECI } from '../../lib/period';

export interface DatePickerProps {
  /** Vrednost `YYYY-MM-DD` ili prazan string. */
  value: string;
  onChange: (value: string) => void;
  /** `YYYY-MM-DD` — dani pre `min` / posle `max` su onemogućeni. */
  min?: string;
  max?: string;
  disabled?: boolean;
  /** Tekst kad nema vrednosti. Podrazumevano „Odaberite datum". */
  placeholder?: string;
  /** Dozvoli brisanje (X u polju + „Obriši" u footeru). Podrazumevano `true`. */
  clearable?: boolean;
  /** `id` trigger dugmeta — za `<label htmlFor>` (FormField). */
  id?: string;
  /** Pristupačni naziv kad nema vidljive labele (npr. u tabeli). */
  'aria-label'?: string;
  /** Klase za omotač (širina, npr. `w-44`). */
  className?: string;
}

const DANI = ['Pon', 'Uto', 'Sre', 'Čet', 'Pet', 'Sub', 'Ned'];

/**
 * Picker za jedan dan u stilu aplikacije (blueprint 15 §2).
 *
 * Custom umesto `<input type="date">` jer native picker izgleda drugačije u svakom
 * browseru, nema srpske nazive dana/meseci ni „Danas"/„Obriši". Ulaz/izlaz je
 * `YYYY-MM-DD` string — isti oblik ide na API (`DateOnly`). Popover ide kroz
 * `AnchoredDropdown` portal, pa ga tabela/kartica sa overflow-om ne seče; Esc
 * zatvara (obrađuje ga AnchoredDropdown).
 */
export const DatePicker = forwardRef<HTMLButtonElement, DatePickerProps>(
  (
    {
      value,
      onChange,
      min,
      max,
      disabled,
      placeholder = 'Odaberite datum',
      clearable = true,
      id,
      'aria-label': ariaLabel,
      className,
    },
    ref,
  ) => {
    const [open, setOpen] = useState(false);
    // Anchor kao state (ne ref) da AnchoredDropdown dobije element bez čitanja
    // ref-a tokom rendera.
    const [anchor, setAnchor] = useState<HTMLButtonElement | null>(null);

    const izabran = parsirajYmd(value);

    // Prikazani mesec. Prati `value` i kad se promeni spolja (npr. „Danas" iz
    // roditelja) — obrazac „izvedeno stanje iz prop-a" bez sync-effect-a.
    const [prikaz, setPrikaz] = useState(() => pocetniPrikaz(izabran));
    const [prethodnaVrednost, setPrethodnaVrednost] = useState(value);
    if (value !== prethodnaVrednost) {
      setPrethodnaVrednost(value);
      if (izabran) setPrikaz({ godina: izabran.getFullYear(), mesec: izabran.getMonth() });
    }

    const danas = danasYmd();

    // 6×7 grid; prvi prikazan dan je ponedeljak nedelje koja sadrži 1. u mesecu.
    const dani = useMemo(() => {
      const prvi = new Date(prikaz.godina, prikaz.mesec, 1);
      const pomak = (prvi.getDay() + 6) % 7; // 0 = ponedeljak
      const start = new Date(prikaz.godina, prikaz.mesec, 1 - pomak);
      return Array.from({ length: 42 }, (_, i) => {
        const d = new Date(start);
        d.setDate(start.getDate() + i);
        return d;
      });
    }, [prikaz]);

    function uOpsegu(ymd: string): boolean {
      if (min && ymd < min) return false;
      if (max && ymd > max) return false;
      return true;
    }

    function pomeriMesec(delta: number) {
      const d = new Date(prikaz.godina, prikaz.mesec + delta, 1);
      setPrikaz({ godina: d.getFullYear(), mesec: d.getMonth() });
    }

    function izaberi(ymd: string) {
      onChange(ymd);
      setOpen(false);
    }

    const setRefs = (el: HTMLButtonElement | null) => {
      setAnchor(el);
      if (typeof ref === 'function') ref(el);
      else if (ref) ref.current = el;
    };

    return (
      <div className={cn('relative', className)}>
        <button
          ref={setRefs}
          id={id}
          type="button"
          disabled={disabled}
          aria-label={ariaLabel}
          aria-haspopup="dialog"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
          className={cn(
            'flex h-10 w-full items-center gap-2 rounded-md border bg-white px-3 text-sm transition-colors',
            'border-gray-300 hover:border-gray-400 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500',
            'disabled:cursor-not-allowed disabled:bg-gray-50 disabled:text-gray-500',
            clearable && izabran && 'pr-8',
            !izabran && 'text-gray-400',
          )}
        >
          <Calendar size={14} className="shrink-0 text-gray-500" />
          <span className="truncate">{izabran ? formatirajDatum(value) : placeholder}</span>
        </button>

        {clearable && izabran && !disabled && (
          <button
            type="button"
            onClick={() => onChange('')}
            aria-label="Obriši datum"
            className="absolute right-2 top-1/2 -translate-y-1/2 rounded p-0.5 text-gray-400 hover:bg-gray-100 hover:text-gray-700"
          >
            <X size={14} />
          </button>
        )}

        <AnchoredDropdown
          anchor={anchor}
          open={open && !disabled}
          onRequestClose={() => setOpen(false)}
          widthMode="auto"
          className="w-80 rounded-md border border-gray-200 bg-white p-3 shadow-lg"
        >
          <div className="mb-2 flex items-center justify-between">
            <button
              type="button"
              onClick={() => pomeriMesec(-1)}
              className="rounded p-1 text-gray-500 hover:bg-gray-100"
              aria-label="Prethodni mesec"
            >
              <ChevronLeft size={16} />
            </button>
            <div className="text-sm font-semibold text-gray-800">
              {MESECI[prikaz.mesec]} {prikaz.godina}
            </div>
            <button
              type="button"
              onClick={() => pomeriMesec(1)}
              className="rounded p-1 text-gray-500 hover:bg-gray-100"
              aria-label="Sledeći mesec"
            >
              <ChevronRight size={16} />
            </button>
          </div>

          <div className="mb-1 grid grid-cols-7 text-center text-[10px] font-medium uppercase tracking-wide text-gray-500">
            {DANI.map((d) => (
              <div key={d}>{d}</div>
            ))}
          </div>

          <div className="grid grid-cols-7 gap-y-1 text-center text-sm">
            {dani.map((d) => {
              const ymd = formatirajYmd(d);
              const uMesecu = d.getMonth() === prikaz.mesec;
              const jeDanas = ymd === danas;
              const jeIzabran = ymd === value;
              const dozvoljen = uOpsegu(ymd);
              return (
                <button
                  key={ymd}
                  type="button"
                  disabled={!dozvoljen}
                  onClick={() => izaberi(ymd)}
                  aria-label={formatirajDatum(ymd)}
                  aria-pressed={jeIzabran}
                  className={cn(
                    'mx-auto h-8 w-8 rounded-full text-xs transition-colors',
                    jeIzabran
                      ? 'bg-primary-500 font-semibold text-white hover:bg-primary-600'
                      : jeDanas
                        ? 'border border-primary-500 text-primary-700'
                        : uMesecu
                          ? 'text-gray-800 hover:bg-gray-100'
                          : 'text-gray-300 hover:bg-gray-50',
                    !dozvoljen && 'cursor-not-allowed text-gray-300 line-through hover:bg-transparent',
                  )}
                >
                  {d.getDate()}
                </button>
              );
            })}
          </div>

          <div className="mt-3 flex items-center justify-between border-t border-gray-100 pt-2 text-xs">
            <button
              type="button"
              disabled={!uOpsegu(danas)}
              onClick={() => izaberi(danas)}
              className="font-medium text-primary-700 hover:underline disabled:text-gray-400 disabled:no-underline"
            >
              Danas
            </button>
            {clearable && izabran && (
              <button
                type="button"
                onClick={() => izaberi('')}
                className="text-gray-500 hover:text-red-600"
              >
                Obriši
              </button>
            )}
          </div>
        </AnchoredDropdown>
      </div>
    );
  },
);
DatePicker.displayName = 'DatePicker';

/** Mesec koji se prikazuje pri otvaranju: izabrani datum ili tekući mesec. */
function pocetniPrikaz(izabran: Date | null): { godina: number; mesec: number } {
  const d = izabran ?? new Date();
  return { godina: d.getFullYear(), mesec: d.getMonth() };
}
