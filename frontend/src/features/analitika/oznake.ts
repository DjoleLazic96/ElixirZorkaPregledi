import type { Stanje } from './hooks';

/** Stanje uređaja: oznaka i boja (zeleno ispravan, crveno neispravan, sivo nije u funkciji). */
export const STANJA: Record<Stanje, { labela: string; boja: string }> = {
  ispravan: { labela: 'Ispravan', boja: 'bg-emerald-500' },
  neispravan: { labela: 'Neispravan', boja: 'bg-red-500' },
  van_funkcije: { labela: 'Nije u funkciji', boja: 'bg-gray-400' },
};

/** Status uređaja na ekranu (R8.4). */
export const STATUS_UREDJAJA: Record<string, { labela: string; ton: 'gray' | 'primary' | 'amber' | 'red' }> = {
  ok: { labela: 'Ispravan', ton: 'primary' },
  kvar: { labela: 'Otvoren kvar', ton: 'red' },
  van_funkcije: { labela: 'Nije u funkciji', ton: 'amber' },
  nije_pregledan: { labela: 'Nije pregledan', ton: 'gray' },
};
