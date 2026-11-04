import type { PregledKalendarDto, RasporedDto } from './hooks';

/**
 * Boje statusa (R3.22) po mockup-u (`.ev`): zeleno završen, plavo overen, crveno nije započet tog dana, žuto u radu, belo sa okvirom planiran.
 */
export function bojaPregleda(p: PregledKalendarDto, danas: string): string {
  if (p.status === 'overen') return 'border-transparent bg-blue-50 text-blue-700';
  if (p.status === 'ceka_overu' || p.status === 'vracen_na_doradu') return 'border-transparent bg-green-50 text-green-700';
  if (p.status === 'u_radu') return 'border-transparent bg-amber-50 text-amber-800';
  if (p.status === 'ponisten') return 'border-transparent bg-gray-100 text-gray-400 line-through';
  if (p.planiraniDatum < danas) return 'border-transparent bg-red-50 text-red-700';
  return 'border-gray-200 bg-white text-gray-500';
}

export const STATUS_LABELA: Record<PregledKalendarDto['status'], string> = {
  planiran: 'Planiran',
  u_radu: 'U radu',
  ceka_overu: 'Čeka overu',
  overen: 'Overen',
  vracen_na_doradu: 'Vraćen na doradu',
  ponisten: 'Poništen',
};

/** Šta se prikazuje jednog dana: pregledi tog dana i oznake „prenet na …“ za preglede kojima je to bio prvobitni dan. */
export function stavkeDana(r: RasporedDto | undefined, dan: string) {
  const pregledi = (r?.pregledi ?? []).filter((p) => p.planiraniDatum === dan);
  const preneti = (r?.pregledi ?? []).filter((p) => p.prvobitniDatum === dan && p.planiraniDatum !== dan);
  const neradni = r?.neradniDani.find((n) => n.datum === dan);
  const odsutni = (r?.odsustva ?? []).filter((o) => o.od <= dan && o.do >= dan);
  return { pregledi, preneti, neradni, odsutni };
}
