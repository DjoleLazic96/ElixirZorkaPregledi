import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
} from 'react';
import { Check, ChevronDown, Search } from 'lucide-react';
import { AnchoredDropdown } from './AnchoredDropdown';
import { cn } from '../../lib/cn';

export interface SelectOption<T extends string | number> {
  value: T;
  label: string;
  /**
   * Sekundarni tekst desno od labela (npr. vremenski opseg smene
   * "07:00–15:00"). Renderuje se sivo, manjim leading-om.
   */
  hint?: string;
}

interface SelectProps<T extends string | number> {
  value: T | null;
  options: ReadonlyArray<SelectOption<T>>;
  /** Tekst kad nema selekcije (default: "— odaberite —"). */
  placeholder?: string;
  /**
   * Kad je <c>true</c>, dropdown nudi stavku "— odaberite —" koja postavlja
   * vrednost na <c>null</c>. Default: <c>false</c> (lista opcija je jedini
   * izvor vrednosti).
   */
  nullable?: boolean;
  disabled?: boolean;
  onChange: (value: T | null) => void;
  /** Dodatne Tailwind klase za trigger button (npr. <c>w-full</c>, <c>min-w-44</c>). */
  className?: string;
  /** Opcioni <c>id</c> na trigger dugmetu (za povezivanje sa spoljnim &lt;label&gt;). */
  id?: string;
  /** Opcioni pristupačni naziv kad ne postoji vidljivi &lt;label&gt; (npr. u tabeli). */
  'aria-label'?: string;
  /**
   * Prag broja opcija iznad kojeg se prikazuje polje za pretragu na vrhu panela.
   * Default: 8. Ispod praga koristi se type-ahead (kucanje slova skače na opciju).
   */
  searchThreshold?: number;
}

/**
 * Uklanja dijakritike (Š→S, Ć→C, Č→C, Ž→Z, Đ→D…) i vraća lowercase — za
 * pretragu koja je i case- i dijakritik-neosetljiva. Đ/đ se ne razlažu preko
 * NFD-a pa ih tretiramo posebno.
 */
function foldSrp(s: string): string {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase();
}

/**
 * Custom dropdown — vizuelno usklađen sa <c>Input</c> komponentom (ista visina,
 * border, fokus prsten) i renderuje listu kroz <c>AnchoredDropdown</c> portal
 * pa se ne može isečeniti od overflow:hidden roditelja.
 *
 * Tip parametar T je union string-literal-a ili number — npr. <c>1 | 2 | 3</c>
 * (broj smene), <c>'rad' | 'planiran'</c> (id kategorije), itd.
 *
 * Razlog za custom umesto nativnog <c>&lt;select&gt;</c>: nativni dobija
 * OS-stil (Windows-ov plavi highlight) koji ne odgovara dizajnu aplikacije.
 *
 * <para><b>Pristupačnost (WAI-ARIA combobox/listbox):</b> trigger je
 * <c>button role="combobox"</c> sa <c>aria-expanded</c>/<c>aria-controls</c>,
 * panel je <c>role="listbox"</c>, stavke <c>role="option"</c> sa
 * <c>aria-selected</c>, a highlight se prati preko <c>aria-activedescendant</c>.</para>
 *
 * <para><b>Tastatura:</b> ↑/↓ pomera highlight (i otvara ako je zatvoreno),
 * Enter/Space bira, Esc zatvara i vraća fokus na trigger, Home/End skaču,
 * a kucanje slova radi type-ahead (ili filtrira ako je vidljiva pretraga).</para>
 *
 * <para><b>Pretraga:</b> kad broj opcija pređe <c>searchThreshold</c> (default 8),
 * na vrhu panela se prikazuje polje za pretragu (case- i dijakritik-neosetljivo).</para>
 */
export function Select<T extends string | number>({
  value,
  options,
  placeholder = '— odaberite —',
  nullable = false,
  disabled,
  onChange,
  className,
  id,
  'aria-label': ariaLabel,
  searchThreshold = 8,
}: SelectProps<T>) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  // Indeks highlight-ovane stavke unutar `stavke` (vidi dole). -1 = ništa.
  const [highlight, setHighlight] = useState(-1);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  // Ref ka kontejneru panela (unutar portala). Treba nam za containment proveru
  // pri gubitku fokusa — panel je portalovan VAN DOM podstabla trigger-a, pa
  // wrapper onBlur ne bi video prelaz fokusa između trigger-a i search polja.
  const panelRef = useRef<HTMLDivElement>(null);
  // Bafer za type-ahead (kad nema search polja): akumulira otkucana slova.
  const typeahead = useRef<{ buf: string; ts: number }>({ buf: '', ts: 0 });

  const reactId = useId();
  const listboxId = `${reactId}-listbox`;
  const optionId = (i: number) => `${reactId}-opt-${i}`;

  const selected = options.find((o) => o.value === value) ?? null;
  const showSearch = options.length >= searchThreshold;

  // Prikazani tekst na trigger-u (izabrana labela ili placeholder).
  const displayText = selected ? selected.label : placeholder;

  // Pristupačni naziv: role="combobox" NE izvodi ime iz sadržaja (ARIA pravilo),
  // pa mu moramo dati eksplicitno. Prioritet: prosleđeni aria-label; ako ga nema
  // ni povezanog <label> preko id-a, koristi prikazani tekst kao fallback.
  const accessibleName = ariaLabel ?? (id ? undefined : displayText);

  // Filtrirane opcije (samo kad je pretraga vidljiva i uneta). Nullable stavka
  // ("— odaberite —") se uvek nudi kao prva pseudo-opcija sa vrednošću null.
  const filtered = useMemo(() => {
    if (!showSearch || query.trim() === '') return options;
    const q = foldSrp(query.trim());
    return options.filter((o) => foldSrp(o.label).includes(q));
  }, [options, showSearch, query]);

  // Ravan spisak „stavki" nad kojima se kreće highlight: opciono null-stavka
  // pa filtrirane opcije. Vrednost `null` predstavlja „— odaberite —".
  const stavke = useMemo<Array<{ value: T | null; label: string; hint?: string }>>(() => {
    const base = filtered.map((o) => ({ value: o.value, label: o.label, hint: o.hint }));
    // Null-stavka („— odaberite —") se prikazuje kao prva pseudo-opcija samo kad
    // NEMA aktivne pretrage; dok korisnik kuca u polje za pretragu placeholder
    // bi bio šum na svakom upitu, pa ga tada sakrivamo.
    const showNull = nullable && query.trim() === '';
    return showNull ? [{ value: null, label: placeholder }, ...base] : base;
  }, [filtered, nullable, placeholder, query]);

  // Pri otvaranju: highlight na trenutno izabranu (ili prvu) stavku i reset pretrage.
  // Namerno sinhronizujemo lokalni UI-state na prelazu open→false/true; nije
  // eksterni sistem pa lint pravilo ne važi ovde.
  useEffect(() => {
    if (!open) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setQuery('');
      setHighlight(-1);
      return;
    }
    const idx = stavke.findIndex((s) => s.value === value);
    setHighlight(idx >= 0 ? idx : stavke.length > 0 ? 0 : -1);
    // Fokus na search polje kad postoji (da odmah može da se kuca).
    if (showSearch) {
      // rAF: sačekaj da AnchoredDropdown mount-uje panel u portal.
      requestAnimationFrame(() => searchInputRef.current?.focus());
    }
    // Namerno bez `stavke`/`value` u deps — highlight računamo samo na prelazu open→true.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // Kad se lista promeni zbog pretrage, drži highlight u granicama.
  useEffect(() => {
    if (!open) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setHighlight((h) => (stavke.length === 0 ? -1 : Math.min(Math.max(h, 0), stavke.length - 1)));
  }, [stavke.length, open]);

  // Skroluj highlight-ovanu stavku u vidljivi deo panela.
  useEffect(() => {
    if (!open || highlight < 0) return;
    const el = listRef.current?.querySelector<HTMLElement>(`#${CSS.escape(optionId(highlight))}`);
    // scrollIntoView nije implementiran u jsdom (test okruženje) — zato try/catch.
    try {
      el?.scrollIntoView({ block: 'nearest' });
    } catch {
      /* no-op u okruženjima bez layout-a */
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [highlight, open]);

  // Zatvori panel kad tastaturski fokus napusti CEO widget (npr. korisnik
  // pritisne Tab). AnchoredDropdown zatvara samo na outside-mousedown, pa bi
  // Tab-away ostavio siroče: otvoren listbox sa aria-expanded=true. Slušamo
  // document `focusin` i zatvaramo ako novi fokus nije ni u trigger-u ni u
  // panelu (koji je portalovan pa nije u istom podstablu). Namerno NE vraćamo
  // fokus na trigger — korisnik svesno pomera fokus dalje.
  useEffect(() => {
    if (!open) return;
    function onFocusIn(e: FocusEvent) {
      const target = e.target as Node | null;
      if (!target) return;
      if (triggerRef.current?.contains(target)) return; // fokus u trigger — ne zatvaraj
      if (panelRef.current?.contains(target)) return; // fokus u panel (npr. search) — ne zatvaraj
      setOpen(false);
    }
    document.addEventListener('focusin', onFocusIn);
    return () => document.removeEventListener('focusin', onFocusIn);
  }, [open]);

  function commit(idx: number) {
    const s = stavke[idx];
    if (!s) return;
    onChange(s.value);
    setOpen(false);
    triggerRef.current?.focus();
  }

  function openAndHighlight(dir: 1 | -1) {
    setOpen(true);
    // Highlight se postavlja u effect-u na otvaranju; ovde samo osiguraj smer
    // kad je već otvoreno.
    if (open) {
      setHighlight((h) => {
        if (stavke.length === 0) return -1;
        const next = h < 0 ? (dir === 1 ? 0 : stavke.length - 1) : h + dir;
        return Math.min(Math.max(next, 0), stavke.length - 1);
      });
    }
  }

  // Type-ahead kad NEMA search polja: kucanje slova skače na prvu opciju čiji
  // label (dijakritik-neosetljivo) počinje akumuliranim baferom.
  function typeaheadJump(char: string) {
    const now = Date.now();
    const buf = now - typeahead.current.ts > 700 ? char : typeahead.current.buf + char;
    typeahead.current = { buf, ts: now };
    const q = foldSrp(buf);
    const idx = stavke.findIndex((s) => foldSrp(s.label).startsWith(q));
    if (idx >= 0) setHighlight(idx);
  }

  function onTriggerKeyDown(e: KeyboardEvent<HTMLButtonElement>) {
    if (disabled) return;
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        openAndHighlight(1);
        break;
      case 'ArrowUp':
        e.preventDefault();
        openAndHighlight(-1);
        break;
      case 'Enter':
      case ' ':
        e.preventDefault();
        if (!open) setOpen(true);
        else if (highlight >= 0) commit(highlight);
        break;
      case 'Escape':
        if (open) {
          e.preventDefault();
          setOpen(false);
        }
        break;
      case 'Home':
        if (open) {
          e.preventDefault();
          setHighlight(0);
        }
        break;
      case 'End':
        if (open) {
          e.preventDefault();
          setHighlight(stavke.length - 1);
        }
        break;
      default:
        // Štampani karakter (jedan znak, bez modifikatora) — kao kod nativnog
        // <select>-a reaguje i kad je zatvoreno.
        if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey) {
          if (!open) {
            // Zatvoreno: otvori panel. Za search-varijantu je otvaranje dovoljno
            // (fokus ide u polje za pretragu pa korisnik nastavlja da kuca) —
            // ne dupliramo obradu; za type-ahead varijantu skoči na poklapanje.
            e.preventDefault();
            setOpen(true);
            // rAF: effect na otvaranju (open→true) resetuje highlight na izabranu
            // stavku; skok primenjujemo posle njega da ga ne pregazi.
            if (!showSearch) {
              const ch = e.key;
              requestAnimationFrame(() => typeaheadJump(ch));
            }
          } else if (!showSearch) {
            // Otvoreno bez search polja — standardni type-ahead skok.
            typeaheadJump(e.key);
          }
        }
    }
  }

  // Tastatura unutar search polja: ↑/↓/Enter/Esc/Home/End upravljaju listom,
  // ostalo (kucanje) filtrira.
  function onSearchKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        setHighlight((h) => Math.min(h + 1, stavke.length - 1));
        break;
      case 'ArrowUp':
        e.preventDefault();
        setHighlight((h) => Math.max(h - 1, 0));
        break;
      case 'Enter':
        e.preventDefault();
        if (highlight >= 0) commit(highlight);
        break;
      case 'Escape':
        e.preventDefault();
        setOpen(false);
        triggerRef.current?.focus();
        break;
      case 'Home':
        e.preventDefault();
        setHighlight(0);
        break;
      case 'End':
        e.preventDefault();
        setHighlight(stavke.length - 1);
        break;
    }
  }

  return (
    <>
      <button
        ref={triggerRef}
        id={id}
        type="button"
        disabled={disabled}
        onClick={() => setOpen((o) => !o)}
        onKeyDown={onTriggerKeyDown}
        role="combobox"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={open ? listboxId : undefined}
        aria-activedescendant={open && highlight >= 0 ? optionId(highlight) : undefined}
        aria-label={accessibleName}
        className={cn(
          'flex h-10 items-center justify-between gap-2 rounded-md border border-gray-300 bg-white px-3 text-sm',
          'transition-colors hover:border-gray-400 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500',
          'disabled:cursor-not-allowed disabled:bg-gray-50 disabled:text-gray-500 disabled:hover:border-gray-300',
          className ?? 'w-full'
        )}
      >
        <span
          className={cn(
            'truncate',
            selected ? 'text-gray-800' : 'text-gray-400 italic'
          )}
        >
          {selected ? selected.label : placeholder}
          {selected?.hint && (
            <span className="ml-1 text-gray-400">{selected.hint}</span>
          )}
        </span>
        <ChevronDown
          size={16}
          className={cn(
            'shrink-0 text-gray-400 transition-transform',
            open && 'rotate-180'
          )}
        />
      </button>
      <AnchoredDropdown
        // Namerno čitamo ref tokom render-a: AnchoredDropdown re-meri anchor
        // preko getBoundingClientRect u sopstvenom effect-u, pa je stale-vrednost
        // pri prvom render-u (null) bezopasna — popup je ionako zatvoren.
        // eslint-disable-next-line react-hooks/refs
        anchor={triggerRef.current}
        open={open && !disabled}
        onRequestClose={() => setOpen(false)}
        widthMode="min-anchor"
        className="flex max-h-72 flex-col overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg ring-1 ring-black/5"
      >
        <div ref={panelRef} className="flex min-h-0 flex-col">
        {showSearch && (
          <div className="border-b border-gray-100 p-2">
            <div className="relative">
              <Search
                size={14}
                className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400"
              />
              <input
                ref={searchInputRef}
                type="text"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={onSearchKeyDown}
                placeholder="Pretraži…"
                aria-label="Pretraži opcije"
                aria-controls={listboxId}
                aria-activedescendant={highlight >= 0 ? optionId(highlight) : undefined}
                className="w-full rounded-md border border-gray-300 py-1.5 pl-8 pr-2 text-sm focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500"
              />
            </div>
          </div>
        )}
        <ul ref={listRef} id={listboxId} role="listbox" className="overflow-y-auto py-1">
          {stavke.length === 0 && (
            <li className="px-3 py-2 text-sm italic text-gray-400" aria-disabled="true">
              Nema rezultata
            </li>
          )}
          {stavke.map((s, i) => {
            const isSelected = s.value === value;
            const isActive = i === highlight;
            const isNull = s.value === null;
            return (
              <li
                key={s.value === null ? '__null__' : String(s.value)}
                id={optionId(i)}
                role="option"
                aria-selected={isSelected}
                // onMouseDown umesto onClick: sprečava da AnchoredDropdown-ov
                // outside-mousedown zatvori panel pre nego što stigne click.
                onMouseDown={(e) => {
                  e.preventDefault();
                  commit(i);
                }}
                onMouseEnter={() => setHighlight(i)}
                className={cn(
                  'flex cursor-pointer items-center justify-between gap-2 px-3 py-2 text-sm transition-colors',
                  isActive && 'bg-primary-50',
                  isSelected
                    ? 'font-medium text-primary-700'
                    : isNull
                      ? 'italic text-gray-500'
                      : 'text-gray-700'
                )}
              >
                <span className="truncate">
                  {s.label}
                  {s.hint && (
                    <span
                      className={cn(
                        'ml-1',
                        isSelected ? 'text-primary-500' : 'text-gray-400'
                      )}
                    >
                      {s.hint}
                    </span>
                  )}
                </span>
                {isSelected && (
                  <Check size={14} className="shrink-0 text-primary-600" />
                )}
              </li>
            );
          })}
        </ul>
        </div>
      </AnchoredDropdown>
    </>
  );
}
