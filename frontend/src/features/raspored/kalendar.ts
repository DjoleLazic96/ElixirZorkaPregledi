import type { PregledKalendarDto, RasporedDto } from './hooks';

/** Boje statusa (R3.22): zeleno završen, plavo overen, crveno nije započet tog dana, žuto u radu, bez boje planiran. */
export function bojaPregleda(p: PregledKalendarDto, danas: string): string {
  if (p.status === 'overen') return 'border-blue-300 bg-blue-50 text-blue-900';
  if (p.status === 'ceka_overu' || p.status === 'vracen_na_doradu') return 'border-green-300 bg-green-50 text-green-900';
  if (p.status === 'u_radu') return 'border-amber-300 bg-amber-50 text-amber-900';
  if (p.status === 'ponisten') return 'border-gray-200 bg-gray-50 text-gray-400 line-through';
  if (p.planiraniDatum < danas) return 'border-red-300 bg-red-50 text-red-900';
  return 'border-gray-200 bg-white text-gray-900';
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
