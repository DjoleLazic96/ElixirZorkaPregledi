import { resolve } from 'node:path';

/**
 * Lozinke nisu u kodu: CI ih pravi nasumično za svako pokretanje (i istu daje API-ju kao početnu lozinku admina),
 * a lokalno se postave promenljive okruženja pre `npm run e2e`.
 */
function obavezno(ime: string): string {
  const v = process.env[ime];
  if (!v) throw new Error(`Postavite promenljivu okruženja ${ime} (vidi frontend/e2e/README.md).`);
  return v;
}

export const ADMIN = { ime: 'admin', lozinka: () => obavezno('E2E_ADMIN_LOZINKA') };
/** Lozinka probnih naloga koje priprema pravi u test bazi. */
export const lozinka = () => obavezno('E2E_LOZINKA');
export const NALOZI = {
  inzenjer: { ime: 'provera.inzenjer', puno: 'Provera Inženjer', rola: 'Inzenjer' },
  supervizor: { ime: 'provera.supervizor', puno: 'Provera Supervizor', rola: 'Supervizor' },
  direktor: { ime: 'provera.direktor', puno: 'Provera Direktor', rola: 'Direktor' },
} as const;

export type Rola = 'admin' | keyof typeof NALOZI;

/** Sačuvana prijava po roli (refresh cookie). Svaku koristi tačno jedan test — refresh token se rotira pri upotrebi. */
export const stanje = (rola: Rola) => resolve(import.meta.dirname, '.stanje', `${rola}.json`);
/** ID pregleda koji je priprema zakazala inženjeru za danas. */
export const PREGLED = resolve(import.meta.dirname, '.stanje', 'pregled.json');
