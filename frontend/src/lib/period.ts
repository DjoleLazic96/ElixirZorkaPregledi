/** Meseci i periodi (Elixir blueprint 15 §4) — zajednički za MesecPicker, DatePicker i mesečne preglede. */

export const MESECI = [
  'Januar',
  'Februar',
  'Mart',
  'April',
  'Maj',
  'Jun',
  'Jul',
  'Avgust',
  'Septembar',
  'Oktobar',
  'Novembar',
  'Decembar',
] as const;

export interface Period {
  godina: number;
  mesec: number;
}

/** Naziv meseca (1-based); nepoznat broj se vraća kao tekst. */
export function nazivMeseca(mesec: number): string {
  return MESECI[mesec - 1] ?? String(mesec);
}

/** Opcije za Select meseca (value 1..12, label pun naziv). */
export function mesecOpcije(): { value: number; label: string }[] {
  return MESECI.map((m, i) => ({ value: i + 1, label: m }));
}

/** Opcije za Select godine: [tekuća−razmak .. tekuća+razmak]. */
export function godinaOpcije(razmak = 1): { value: number; label: string }[] {
  const sada = new Date().getFullYear();
  const out: { value: number; label: string }[] = [];
  for (let g = sada - razmak; g <= sada + razmak; g++) out.push({ value: g, label: String(g) });
  return out;
}

/** Širi opseg za preglede po periodu: [tekuća−3 .. tekuća+1]. */
export function godinaOpcijeSire(): { value: number; label: string }[] {
  const sada = new Date().getFullYear();
  const out: { value: number; label: string }[] = [];
  for (let g = sada - 3; g <= sada + 1; g++) out.push({ value: g, label: String(g) });
  return out;
}

/** `'YYYY-MM'` ključ — oblik koji ide na API i poredi se kao string. */
export function periodKljuc(godina: number, mesec: number): string {
  return `${godina}-${String(mesec).padStart(2, '0')}`;
}

/** Tekući mesec/godina (lokalno). */
export function trenutniPeriod(): Period {
  const d = new Date();
  return { godina: d.getFullYear(), mesec: d.getMonth() + 1 };
}

/** Oduzmi N meseci; radi preko granice godine (i za negativno N — dodaje). */
export function oduzmiMeseci(godina: number, mesec: number, n: number): Period {
  // 0-based indeks meseca radi lakše aritmetike.
  const ukupno = godina * 12 + (mesec - 1) - n;
  return { godina: Math.floor(ukupno / 12), mesec: (((ukupno % 12) + 12) % 12) + 1 };
}

/** `'oktobar 2026.'` — mesec malim slovom (pravopis), godina sa tačkom. */
export function formatirajPeriod(godina: number, mesec: number): string {
  return `${nazivMeseca(mesec).toLowerCase()} ${godina}.`;
}
