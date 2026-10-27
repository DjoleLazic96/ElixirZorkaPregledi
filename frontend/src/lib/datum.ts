/**
 * Jedini izvor formata datuma i vremena (Elixir blueprint 15 §3).
 *
 * Prikaz trenutaka je u LOKALNOJ zoni browsera (korisnici su u Beogradu); jedini
 * izuzetak je stanje servera (`formatirajVremeUtc`, označeno „(UTC)").
 * Komponente nikad ne zovu `toLocaleDateString` direktno niti ručno seku ISO string.
 */

/** Uvek sa regionom — `sr-Latn` bez `-RS` daje drugačije separatore u nekim ICU verzijama. */
export const LOKAL = 'sr-Latn-RS';

const DAN_MS = 86_400_000;

/** Dva broja sa vodećom nulom. */
function dd(n: number): string {
  return String(n).padStart(2, '0');
}

/**
 * Parsiraj `YYYY-MM-DD` u lokalnu ponoć. `null` za prazan ili nevalidan ulaz
 * (uključujući npr. 2026-02-30 koji bi `Date` tiho „prelio" u mart).
 */
export function parsirajYmd(v: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(v);
  if (!m) return null;
  const [godina, mesec, dan] = [Number(m[1]), Number(m[2]), Number(m[3])];
  const d = new Date(godina, mesec - 1, dan);
  const validan = d.getFullYear() === godina && d.getMonth() === mesec - 1 && d.getDate() === dan;
  return validan ? d : null;
}

/**
 * ISO string → `Date`. Čist dan (`YYYY-MM-DD`) se tumači kao LOKALNI dan, jer bi
 * `new Date('2026-10-12')` dao UTC ponoć — u zonama zapadno od Griniča to je 11.10.
 */
function uDate(iso: string): Date {
  return parsirajYmd(iso) ?? new Date(iso);
}

/** Lokalni dan → `YYYY-MM-DD`. */
export function formatirajYmd(d: Date): string {
  return `${d.getFullYear()}-${dd(d.getMonth() + 1)}-${dd(d.getDate())}`;
}

/** Današnji LOKALNI dan kao `YYYY-MM-DD` (nikad `toISOString().slice(0, 10)`). */
export function danasYmd(): string {
  return formatirajYmd(new Date());
}

/**
 * `'12.10.2026'`. Ručno iz lokalnih delova: Intl `dateStyle: 'medium'` za
 * `sr-Latn-RS` daje „12. 10. 2026." (razmaci i tačka na kraju), što ne želimo.
 */
export function formatirajDatum(iso: string): string {
  const d = uDate(iso);
  if (Number.isNaN(d.getTime())) return '';
  return `${dd(d.getDate())}.${dd(d.getMonth() + 1)}.${d.getFullYear()}`;
}

/** `'ponedeljak, 12. oktobar 2026.'` — dan i mesec malim slovom, godina sa tačkom. */
export function formatirajDatumDug(iso: string): string {
  const d = uDate(iso);
  if (Number.isNaN(d.getTime())) return '';
  return new Intl.DateTimeFormat(LOKAL, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(d);
}

/** `'12.10.2026 09:30'` u lokalnoj zoni browsera. */
export function formatirajVreme(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return `${formatirajDatum(iso)} ${dd(d.getHours())}:${dd(d.getMinutes())}`;
}

/** `'12.10.2026 09:30:41'` u lokalnoj zoni browsera. */
export function formatirajVremeSek(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return `${formatirajVreme(iso)}:${dd(d.getSeconds())}`;
}

/** `'12.10.2026 07:30 (UTC)'` — samo za stanje servera; oznaka je deo vrednosti. */
export function formatirajVremeUtc(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return (
    `${dd(d.getUTCDate())}.${dd(d.getUTCMonth() + 1)}.${d.getUTCFullYear()} ` +
    `${dd(d.getUTCHours())}:${dd(d.getUTCMinutes())} (UTC)`
  );
}

/** Srpska množina: 1 → jednina, 2–4 → paukal, 11–14 i ostalo → množina. */
function oblik(n: number, jednina: string, paukal: string, mnozina: string): string {
  const poslednjeDve = n % 100;
  const poslednja = n % 10;
  if (poslednjeDve >= 11 && poslednjeDve <= 14) return mnozina;
  if (poslednja === 1) return jednina;
  if (poslednja >= 2 && poslednja <= 4) return paukal;
  return mnozina;
}

/**
 * `'upravo'`, `'pre 5 min'`, `'pre 3 h'`, `'juče'`, `'pre 3 dana'`; stariji od
 * 30 dana → običan datum. Samo gde ima smisla (poslednja prijava), ne u tabelama
 * istorije. `sada` je parametar radi determinističkih testova.
 */
export function relativnoVreme(iso: string, sada: Date = new Date()): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const razlikaSek = Math.floor((sada.getTime() - d.getTime()) / 1000);
  if (razlikaSek < 60) return 'upravo';
  if (razlikaSek < 3600) return `pre ${Math.floor(razlikaSek / 60)} min`;

  // Dani se broje po KALENDARU (lokalno), ne po 24 h — „juče" je jučerašnji dan,
  // bez obzira da li je prošlo 3 ili 23 sata.
  const pocetakDanas = new Date(sada.getFullYear(), sada.getMonth(), sada.getDate());
  const pocetakTog = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  const dana = Math.round((pocetakDanas.getTime() - pocetakTog.getTime()) / DAN_MS);
  if (dana <= 0) return `pre ${Math.floor(razlikaSek / 3600)} h`;
  if (dana === 1) return 'juče';
  if (dana < 30) return `pre ${dana} ${oblik(dana, 'dan', 'dana', 'dana')}`;
  return formatirajDatum(iso);
}

/** Početak LOKALNOG dana → UTC ISO sa `Z` (za `od` filter nad `timestamptz`). */
export function pocetakDanaUtc(ymd: string): string {
  return new Date(`${ymd}T00:00:00`).toISOString();
}

/** Kraj LOKALNOG dana (23:59:59.999) → UTC ISO sa `Z` (inkluzivan `do` filter). */
export function krajDanaUtc(ymd: string): string {
  return new Date(`${ymd}T23:59:59.999`).toISOString();
}
