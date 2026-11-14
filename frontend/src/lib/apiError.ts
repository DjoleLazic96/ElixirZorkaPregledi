import type { ApiProblem } from '../api/types';

/**
 * Izvuci poruku za korisnika iz API greške (RFC 7807 problem+json).
 *
 * Redosled od najkonkretnijeg ka najopštijem:
 *  1. `greske[]` — 422 poslovno pravilo: gotove rečenice, spoji razmakom
 *  2. `errors`   — 400 validacija: mapa polje → poruke, spoji sve
 *  3. `detail`   — RFC 7807 detalj (za 500 backend već u njemu navodi kod za podršku)
 *  4. `title`    — RFC 7807 naslov (generičan)
 *  5. bez interneta — axios bez odgovora dok je telefon offline
 *  6. server nedostupan — axios bez odgovora ili 502/503/504 bez problem+json (API ili proxy ugašen)
 *  7. `fallback` — kad odgovor ne nosi ništa od gore navedenog
 */
export const PORUKA_SERVER_NEDOSTUPAN = 'Server trenutno nije dostupan. Pokušajte ponovo za minut.';

/** Telefon bez interneta (nije kriv server). */
export const PORUKA_BEZ_INTERNETA = 'Nema interneta. Pokušajte ponovo kad se veza vrati.';

export function porukaGreske(err: unknown, fallback: string): string {
  const e = err as { isAxiosError?: boolean; response?: { status?: number; data?: ApiProblem | string } } | undefined;
  const data = typeof e?.response?.data === 'object' ? e.response.data : undefined;
  if (data?.greske && data.greske.length > 0) return data.greske.join(' ');
  if (data?.errors && Object.keys(data.errors).length > 0) {
    return Object.values(data.errors).flat().join(' ');
  }
  if (data?.detail || data?.title) return (data.detail || data.title)!;
  if (e?.isAxiosError && !e.response && typeof navigator !== 'undefined' && navigator.onLine === false) return PORUKA_BEZ_INTERNETA;
  if (e?.isAxiosError && (!e.response || [502, 503, 504].includes(e.response.status ?? 0))) return PORUKA_SERVER_NEDOSTUPAN;
  return fallback;
}

/**
 * Greške po polju iz 400 odgovora, sa ključevima u camelCase (backend može da
 * vrati `Sifra` ili `sifra`). Prazan objekat ako odgovor nije validaciona greška.
 */
export function greskePoPolju(err: unknown): Record<string, string> {
  const errors = (err as { response?: { data?: ApiProblem } } | undefined)?.response?.data
    ?.errors;
  if (!errors) return {};
  const out: Record<string, string> = {};
  for (const [polje, poruke] of Object.entries(errors)) {
    const kljuc = polje.charAt(0).toLowerCase() + polje.slice(1);
    out[kljuc] = poruke.join(' ');
  }
  return out;
}
