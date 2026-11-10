import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { StranicaDto } from '../../api/types';

/** Arhiva, detalj, overa, dorada i poništavanje (`/pregledi`). Svaka odluka osvežava i arhivu, i detalj, i kalendar. */
export interface PregledRedDto {
  id: number;
  broj: string;
  tip: string;
  planiraniDatum: string;
  status: string;
  dorada: number;
  inzenjer: string;
  predatAt: string | null;
  overio: string | null;
  neispravnih: number;
}

export interface DetaljStavkaDto {
  stavkaId: number;
  aktivnost: string;
  odgovor: string | null;
  neispravan: boolean;
  napomena: string | null;
  polja: string[];
  fotografije: number[];
}

export interface PregledDetaljDto {
  id: number;
  broj: string;
  tip: string;
  status: string;
  dorada: number;
  planiraniDatum: string;
  prvobitniDatum: string;
  dodeljenId: string;
  inzenjer: string;
  verzijaBroj: number | null;
  zapocetAt: string | null;
  predatAt: string | null;
  aktivnoSekundi: number;
  delova: number;
  overio: string | null;
  overenoAt: string | null;
  overioDirektor: boolean;
  ponistenRazlog: string | null;
  upozorenja: string[];
  sekcije: { sifra: string; uredjaji: { uredjajId: number; naziv: string; vanFunkcije: boolean; stavke: DetaljStavkaDto[] }[] }[];
  istorija: { vrsta: string; opis: string; korisnik: string; at: string }[];
  sporniUnosi: { id: number; korisnik: string; opis: string; razlog: string; primljenoAt: string; odluka: string | null }[];
  zahteviDorade: { id: number; podneo: string; razlog: string; status: string; kreiranoAt: string }[];
  /** Drugi inženjer na PZ-u sa svojim sekcijama (R3.14). */
  ucesnici: { korisnikId: string; ime: string; sekcije: string[] }[];
  /** Pregled u radu je vezan za uređaj na kome je započet (R10.5); Supervizor ga može otključati (zamerka 8). */
  vezanZaUredjaj?: boolean;
}

export interface PreglediFilteri {
  od: string;
  do: string;
  tip: string | null;
  status: string | null;
  inzenjerId: string | null;
  pretraga: string;
  stranica: number;
  velicina: number;
}

export function parametri(f: PreglediFilteri) {
  return {
    od: f.od || undefined, do: f.do || undefined, tip: f.tip ?? undefined, status: f.status ?? undefined,
    inzenjerId: f.inzenjerId ?? undefined, pretraga: f.pretraga || undefined,
  };
}

export function usePregledi(f: PreglediFilteri) {
  return useQuery({
    queryKey: ['pregledi', f],
    queryFn: async () => (await api.get<StranicaDto<PregledRedDto>>('/pregledi', { params: { ...parametri(f), stranica: f.stranica, velicina: f.velicina } })).data,
    placeholderData: keepPreviousData,
  });
}

export function usePregled(id: number) {
  return useQuery({ queryKey: ['pregledi', 'detalj', id], queryFn: async () => (await api.get<PregledDetaljDto>(`/pregledi/${id}`)).data });
}

/** Sve odluke nad pregledom: `putanja` je deo posle `/pregledi/{id}/`. */
export function useOdluka(id: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { putanja: string; telo?: object }) => (await api.post<PregledDetaljDto>(`/pregledi/${id}/${v.putanja}`, v.telo ?? {})).data,
    onSuccess: (d) => {
      qc.setQueryData(['pregledi', 'detalj', id], d);
      void qc.invalidateQueries({ queryKey: ['pregledi'] });
      void qc.invalidateQueries({ queryKey: ['raspored'] });
    },
  });
}

export const STATUSI: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'planiran', label: 'Planiran' },
  { value: 'u_radu', label: 'U radu' },
  { value: 'ceka_overu', label: 'Čeka overu' },
  { value: 'overen', label: 'Overen' },
  { value: 'vracen_na_doradu', label: 'Vraćen na doradu' },
  { value: 'ponisten', label: 'Poništen' },
];

export const statusLabela = (s: string) => STATUSI.find((x) => x.value === s)?.label ?? s;

/** Boja oznake statusa po mockup-u: Planiran sivo, U radu žuto, Čeka overu zeleno, Overen plavo, Vraćen na doradu crveno, Poništen sivo. */
export function tonStatusa(s: string): 'gray' | 'yellow' | 'green' | 'blue' | 'red' {
  if (s === 'ceka_overu') return 'green';
  if (s === 'overen') return 'blue';
  if (s === 'u_radu') return 'yellow';
  if (s === 'vracen_na_doradu') return 'red';
  return 'gray';
}
