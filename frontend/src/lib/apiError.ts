import type { ApiProblem } from '../api/types';

/**
 * Izvuci poruku za korisnika iz API greške (RFC 7807 problem+json).
 *
 * Redosled od najkonkretnijeg ka najopštijem:
 *  1. `greske[]` — 422 poslovno pravilo: gotove rečenice, spoji razmakom
 *  2. `errors`   — 400 validacija: mapa polje → poruke, spoji sve
 *  3. `detail`   — RFC 7807 detalj (za 500 backend već u njemu navodi kod za podršku)
 *  4. `title`    — RFC 7807 naslov (generičan)
 *  5. `fallback` — kad odgovor ne nosi ništa od gore navedenog (mreža, timeout)
 */
export function porukaGreske(err: unknown, fallback: string): string {
  const data = (err as { response?: { data?: ApiProblem } } | undefined)?.response?.data;
  if (data?.greske && data.greske.length > 0) return data.greske.join(' ');
  if (data?.errors && Object.keys(data.errors).length > 0) {
    return Object.values(data.errors).flat().join(' ');
  }
  return data?.detail || data?.title || fallback;
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
