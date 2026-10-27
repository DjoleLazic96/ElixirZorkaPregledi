/**
 * Jedini izvor formata brojeva (Elixir blueprint 15 §5). Nikad `toFixed` za
 * prikaz — bez separatora hiljada i sa tačkom umesto zareza.
 */
import { LOKAL } from './datum';

export type Jedinica = 'kWh' | 't' | 'h' | '%' | 'kg';

/** Podrazumevan broj decimala po jedinici (kWh 2, tone 3, sati 2, procenti 0, kg 2). */
const DECIMALE_PO_JEDINICI: Record<Jedinica, number> = { kWh: 2, t: 3, h: 2, '%': 0, kg: 2 };

/** Keš formatera po broju decimala — `Intl.NumberFormat` je skup za kreiranje. */
const formateri = new Map<number, Intl.NumberFormat>();
function formater(decimale: number): Intl.NumberFormat {
  let f = formateri.get(decimale);
  if (!f) {
    f = new Intl.NumberFormat(LOKAL, {
      minimumFractionDigits: decimale,
      maximumFractionDigits: decimale,
    });
    formateri.set(decimale, f);
  }
  return f;
}

/** `1234567.891 → '1.234.567,89'`; `null`/`undefined`/NaN → `prazno` („—"). */
export function formatirajBroj(
  n: number | null | undefined,
  decimale = 2,
  prazno = '—',
): string {
  if (n === null || n === undefined || Number.isNaN(n)) return prazno;
  return formater(decimale).format(n);
}

/** `'1.234,50 kWh'` — razmak pre jedinice (i pre „%", isto kao `formatirajProcenat`). */
export function formatirajKolicinu(
  n: number | null | undefined,
  jedinica: Jedinica,
  decimale = DECIMALE_PO_JEDINICI[jedinica],
): string {
  if (n === null || n === undefined || Number.isNaN(n)) return '—';
  return `${formatirajBroj(n, decimale)} ${jedinica}`;
}

/** `'87 %'` — procenti sa razmakom pre znaka. */
export function formatirajProcenat(n: number, decimale = 0): string {
  return `${formatirajBroj(n, decimale)} %`;
}

const JEDINICE_BAJTOVA = ['B', 'KB', 'MB', 'GB', 'TB'] as const;

/** `'7,5 GB'` — binarna osnova (1024), jedna decimala; ispod 1 KB bez decimala. */
export function formatirajBajte(b: number): string {
  if (!Number.isFinite(b) || b < 0) return '—';
  let vrednost = b;
  let i = 0;
  while (vrednost >= 1024 && i < JEDINICE_BAJTOVA.length - 1) {
    vrednost /= 1024;
    i++;
  }
  return `${formatirajBroj(vrednost, i === 0 ? 0 : 1)} ${JEDINICE_BAJTOVA[i]}`;
}

/**
 * Tekst → broj. Prihvata i zarez i tačku kao decimalni znak (copy-paste iz
 * Excela: `'1.234,56'`, `'1234.56'`, `'12,5'`); razmaci se ignorišu. `''` → `null`;
 * nerazumljiv ulaz → `null`. Kad postoje oba znaka, POSLEDNJI je decimalni.
 */
export function parsirajBroj(raw: string): number | null {
  const s = raw.replace(/\s/g, '');
  if (s === '') return null;

  const zarez = s.lastIndexOf(',');
  const tacka = s.lastIndexOf('.');
  let normalizovan: string;
  if (zarez >= 0 && tacka >= 0) {
    const decimalni = Math.max(zarez, tacka);
    normalizovan = s.slice(0, decimalni).replace(/[.,]/g, '') + '.' + s.slice(decimalni + 1);
  } else {
    normalizovan = s.replace(',', '.');
  }
  if (!/^-?\d+(\.\d+)?$/.test(normalizovan)) return null;
  return Number(normalizovan);
}

/** Srpska množina: 1 → jednina, 2–4 → paukal, 11–14 i ostalo → množina. */
export function mnozina(
  n: number,
  oblici: { jednina: string; paukal: string; mnozina: string },
): string {
  const poslednjeDve = n % 100;
  const poslednja = n % 10;
  if (poslednjeDve >= 11 && poslednjeDve <= 14) return oblici.mnozina;
  if (poslednja === 1) return oblici.jednina;
  if (poslednja >= 2 && poslednja <= 4) return oblici.paukal;
  return oblici.mnozina;
}
