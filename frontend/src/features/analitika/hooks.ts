import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { AkcijaDto, KvarDto } from '../akcije/hooks';

/** Ekran uređaja, analitika, Dashboard i pretraga (`/uredjaji/{id}/ekran`, `/analitika/*`, `/dashboard`, `/pretraga`). */
export type Stanje = 'ispravan' | 'neispravan' | 'van_funkcije';

export interface PregledUredjajaDto {
  pregledId: number;
  broj: string;
  tip: string;
  datum: string;
  inzenjer: string;
  stanje: Stanje;
  neispravnih: number;
  status: string;
}

export interface AnalitikaUredjaja {
  danaBezKvaraSada: number;
  najduzeBezKvara: number;
  danaOdPoslednjegKvara: number | null;
  brojKvarova: number;
  prosekTrajanja: number;
  trajanja: number[];
}

export interface DosijeStavkaDto {
  pregledId: number;
  broj: string;
  datum: string;
  aktivnost: string;
  neispravan: boolean;
  napomena: string | null;
  fotografije: number[];
}

export interface UredjajEkranDto {
  id: number;
  naziv: string;
  oznaka: string;
  prikaz: string;
  sekcija: string;
  vrsta: string;
  statusRegistra: string;
  status: 'ok' | 'kvar' | 'van_funkcije' | 'nije_pregledan';
  danaOdPregleda: number | null;
  poslednji: PregledUredjajaDto | null;
  sabloni: string[];
  od: string;
  do: string;
  istorija: { dan: string; stanje: Stanje | null }[];
  analitika: AnalitikaUredjaja;
  najcescaStavka: string | null;
  pregledi: PregledUredjajaDto[];
  dosije: DosijeStavkaDto[];
  akcije: AkcijaDto[];
  kvarovi: KvarDto[];
  qrToken: string;
}

export interface StavkaBrojDto { naziv: string; broj: number; id: number | null }
export interface TrajanjeDto { naziv: string; pregleda: number; prosekMinuta: number }

export interface OpstaAnalitikaDto {
  od: string;
  do: string;
  kvarova: number;
  prosekTrajanjaKvara: number;
  poSekciji: StavkaBrojDto[];
  poUredjaju: StavkaBrojDto[];
  poAktivnosti: StavkaBrojDto[];
  poMesecu: StavkaBrojDto[];
  najcesciKvarovi: StavkaBrojDto[];
  trajanjePoTipu: TrajanjeDto[];
  trajanjePoInzenjeru: TrajanjeDto[];
  vanFunkcije: StavkaBrojDto[];
}

export interface IspunjenjeRedDto {
  naziv: string;
  planirano: number;
  naDan: number;
  kasnije: number;
  propusteno: number;
  preneto: number;
  procenatNaDan: number;
  overeno: number;
  overenoURoku: number;
  procenatURoku: number;
  prosekOvereSati: number;
}

export interface IspunjenjeDto { od: string; do: string; ukupno: IspunjenjeRedDto; poMesecu: IspunjenjeRedDto[]; poInzenjeru: IspunjenjeRedDto[] }

export interface PregledKratkoDto { id: number; broj: string; tip: string; datum: string; status: string; inzenjer: string; overio: string | null; moj: boolean; inzenjerOdsutan?: boolean; zahtevNaCekanju?: string | null }
export interface OdlukaDto { vrsta: string; opis: string; link: string; od: string | null; rokIstekao: boolean }
export interface VanFunkcijeDto { uredjajId: number; uredjaj: string; od: string; dana: number }

export interface DashboardDto {
  moje: { danas: PregledKratkoDto[]; zapoceti: PregledKratkoDto[]; narednih7: PregledKratkoDto[]; zahtevi: OdlukaDto[]; akcije: AkcijaDto[] } | null;
  cekaOdluku: OdlukaDto[] | null;
  kpi: { procenatNaDan: number; procenatURoku: number; cekaOveru: number; otvorenihAkcija: number; otvorenihKvarova: number; vanFunkcije: number };
  danas: PregledKratkoDto[];
  sutra: PregledKratkoDto[];
  cekajuOveru: PregledKratkoDto[];
  akcijePoHitnosti: Record<string, number>;
  akcije: AkcijaDto[];
  kvarovi: KvarDto[];
  vanFunkcije: VanFunkcijeDto[];
}

export interface PretragaRezultatDto { vrsta: 'pregled' | 'akcija' | 'kvar' | 'uredjaj'; id: number; tekst: string; opis: string | null }

export function useUredjajEkran(id: number, dana: number) {
  return useQuery({
    queryKey: ['uredjaj-ekran', id, dana],
    queryFn: async () => (await api.get<UredjajEkranDto>(`/uredjaji/${id}/ekran`, { params: { dana } })).data,
    placeholderData: keepPreviousData,
  });
}

export function useOpstaAnalitika(od: string, doDana: string) {
  return useQuery({
    queryKey: ['analitika', 'opsta', od, doDana],
    queryFn: async () => (await api.get<OpstaAnalitikaDto>('/analitika/opsta', { params: { od, do: doDana } })).data,
    placeholderData: keepPreviousData,
  });
}

export function useIspunjenje(od: string, doDana: string) {
  return useQuery({
    queryKey: ['analitika', 'ispunjenje', od, doDana],
    queryFn: async () => (await api.get<IspunjenjeDto>('/analitika/ispunjenje', { params: { od, do: doDana } })).data,
    placeholderData: keepPreviousData,
  });
}

export function useDashboard() {
  return useQuery({ queryKey: ['dashboard'], queryFn: async () => (await api.get<DashboardDto>('/dashboard')).data });
}

export function usePretraga(q: string) {
  const t = q.trim();
  return useQuery({
    queryKey: ['pretraga', t],
    queryFn: async () => (await api.get<PretragaRezultatDto[]>('/pretraga', { params: { q: t } })).data,
    enabled: t.length >= 2,
    placeholderData: keepPreviousData,
  });
}

/** Ruta ekrana za rezultat pretrage. */
export function linkRezultata(r: PretragaRezultatDto): string {
  switch (r.vrsta) {
    case 'pregled': return `/pregledi/${r.id}`;
    case 'akcija': return `/akcije/${r.id}`;
    case 'kvar': return `/neispravnosti/${r.id}`;
    default: return `/uredjaji/${r.id}`;
  }
}

/**
 * Delovi teksta za žuto obeležavanje pronađenog (R8.14): „S12“ u „IS12“ daje `[{ t: 'I' }, { t: 'S12', pogodak: true }]`.
 * Bez obzira na velika i mala slova; obeležava svako pojavljivanje.
 */
export function deloviPoklapanja(tekst: string, q: string): { t: string; pogodak: boolean }[] {
  const pojam = q.trim().toLowerCase();
  if (pojam.length === 0) return [{ t: tekst, pogodak: false }];
  const rez: { t: string; pogodak: boolean }[] = [];
  const malo = tekst.toLowerCase();
  let i = 0;
  for (let j = malo.indexOf(pojam); j >= 0; j = malo.indexOf(pojam, i)) {
    if (j > i) rez.push({ t: tekst.slice(i, j), pogodak: false });
    rez.push({ t: tekst.slice(j, j + pojam.length), pogodak: true });
    i = j + pojam.length;
  }
  if (i < tekst.length) rez.push({ t: tekst.slice(i), pogodak: false });
  return rez;
}
