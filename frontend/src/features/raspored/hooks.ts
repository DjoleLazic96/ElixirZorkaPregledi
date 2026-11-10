import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';

/**
 * Tipovi i hookovi rasporeda, kalendara, odsustava, neradnih dana i prebacivanja. Sve izmene menjaju kalendar,
 * pa invalidiraju sve ključeve ove celine (jedan pregled se pojavljuje na više ekrana).
 */
export interface UcesnikDto {
  korisnikId: string;
  ime: string;
  sekcije: string[];
}

export interface PregledKalendarDto {
  id: number;
  broj: string;
  tip: string;
  planiraniDatum: string;
  prvobitniDatum: string;
  status: 'planiran' | 'u_radu' | 'ceka_overu' | 'overen' | 'vracen_na_doradu' | 'ponisten';
  dodeljenId: string;
  dodeljen: string;
  ucesnici: UcesnikDto[];
  napomena: string | null;
}

export interface RasporedDto {
  pregledi: PregledKalendarDto[];
  neradniDani: { datum: string; naziv: string }[];
  odsustva: { korisnikId: string; ime: string; od: string; do: string; vrsta: string }[];
}

export interface PredlogStavka {
  datum: string;
  tip: string;
  inzenjerId: string | null;
  inzenjer: string;
}

export interface InzenjerDto {
  id: string;
  ime: string;
}

export interface OdsustvoDto {
  id: number;
  korisnikId: string;
  ime: string;
  od: string;
  do: string;
  vrsta: string;
  napomena: string | null;
  status: 'zahtev' | 'odobreno' | 'odbijeno';
  razlogOdbijanja: string | null;
  kreiranoAt: string;
}

export interface PregledZaPrebacivanjeDto {
  pregled: PregledKalendarDto;
  predlogZameneId: string | null;
  predlogZamene: string;
}

export interface ZahtevPrebacivanjaDto {
  id: number;
  /** `prebacivanje` (na drugog inženjera) ili `raniji_pocetak` (PZ pre zakazanog dana). */
  vrsta: 'prebacivanje' | 'raniji_pocetak';
  pregled: PregledKalendarDto;
  podneoId: string;
  podneo: string;
  predlozenaZamenaId: string | null;
  predlozenaZamena: string;
  razlog: string;
  status: 'zahtev' | 'odobreno' | 'odbijeno';
  zamena: string;
  kreiranoAt: string;
}

export interface NeradniDanDto {
  id: number;
  datum: string;
  naziv: string;
  vrsta: 'drzavni' | 'verski' | 'fabrika';
}

export interface Prebacivanje {
  pregledId: number;
  zamenaId: string;
}

export const VRSTE_ODSUSTVA: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'godisnji', label: 'Godišnji odmor' },
  { value: 'bolovanje', label: 'Bolovanje' },
  { value: 'slobodan_dan', label: 'Slobodan dan' },
  { value: 'sluzbeni_put', label: 'Službeni put' },
  { value: 'ostalo', label: 'Ostalo' },
];

export const VRSTE_NERADNIH: ReadonlyArray<{ value: NeradniDanDto['vrsta']; label: string }> = [
  { value: 'drzavni', label: 'Državni praznik' },
  { value: 'verski', label: 'Verski praznik' },
  { value: 'fabrika', label: 'Neradni dan fabrike' },
];

const KEYS = ['raspored', 'odsustva', 'zahtevi-prebacivanja', 'neradni-dani'] as const;

function useInvalidiraj() {
  const qc = useQueryClient();
  return () => KEYS.forEach((k) => void qc.invalidateQueries({ queryKey: [k] }));
}

export function useRaspored(od: string, doDatuma: string) {
  return useQuery({
    queryKey: ['raspored', od, doDatuma],
    queryFn: async () => (await api.get<RasporedDto>('/raspored', { params: { od, do: doDatuma } })).data,
  });
}

export function useInzenjeri() {
  return useQuery({
    queryKey: ['raspored', 'inzenjeri'],
    queryFn: async () => (await api.get<InzenjerDto[]>('/raspored/inzenjeri')).data,
  });
}

export function usePredlog() {
  return useMutation({
    mutationFn: async (od: string) => (await api.post<PredlogStavka[]>('/raspored/predlog', { od })).data,
  });
}

export function usePotvrdi() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (stavke: { datum: string; tip: string; inzenjerId: string | null }[]) =>
      (await api.post<PregledKalendarDto[]>('/raspored/potvrdi', { stavke })).data,
    onSuccess: inv,
  });
}

export function useSledeciPz(enabled: boolean) {
  return useQuery({
    queryKey: ['raspored', 'pz-sledeci'],
    queryFn: async () => (await api.get<{ tip: string }>('/raspored/pz/sledeci')).data.tip,
    enabled,
  });
}

export function useZakaziPz() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { datum: string; tip: string | null; inzenjerId: string; napomena: string | null }) =>
      (await api.post<PregledKalendarDto>('/raspored/pz', v)).data,
    onSuccess: inv,
  });
}

/** Drugi inženjer na PZ-u sa svojim sekcijama (R3.14); prazan spisak sekcija ga uklanja. */
export function usePostaviUcesnika() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { id: number; korisnikId: string; sekcije: string[] }) =>
      (await api.post<PregledKalendarDto>(`/pregledi/${v.id}/ucesnici`, { korisnikId: v.korisnikId, sekcije: v.sekcije })).data,
    onSuccess: inv,
  });
}

export function useIzmeniDodelu() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { id: number; inzenjerId: string | null; datum: string | null }) =>
      (await api.put<PregledKalendarDto>(`/pregledi/${v.id}/dodela`, { inzenjerId: v.inzenjerId, datum: v.datum })).data,
    onSuccess: inv,
  });
}

export function useOdsustva(status?: string) {
  return useQuery({
    queryKey: ['odsustva', status ?? 'sva'],
    queryFn: async () => (await api.get<OdsustvoDto[]>('/odsustva', { params: { status } })).data,
  });
}

export function useZahtevOdsustva() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { od: string; do: string; vrsta: string; napomena: string | null }) =>
      (await api.post<OdsustvoDto>('/odsustva/zahtev', v)).data,
    onSuccess: inv,
  });
}

export function useUnesiOdsustvo() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { korisnikId: string; od: string; do: string; vrsta: string; napomena: string | null; prebacivanja: Prebacivanje[] }) =>
      (await api.post<OdsustvoDto>('/odsustva', v)).data,
    onSuccess: inv,
  });
}

export function useOdluciOdsustvo() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { id: number; odobreno: boolean; razlog: string | null; prebacivanja: Prebacivanje[] }) =>
      (await api.post<OdsustvoDto>(`/odsustva/${v.id}/odluka`, v)).data,
    onSuccess: inv,
  });
}

export function usePreglediUOdsustvu(korisnikId: string | null, od: string, doDatuma: string) {
  return useQuery({
    queryKey: ['odsustva', 'pregledi', korisnikId, od, doDatuma],
    queryFn: async () =>
      (await api.get<PregledZaPrebacivanjeDto[]>(`/odsustva/${korisnikId}/pregledi`, { params: { od, do: doDatuma } })).data,
    enabled: korisnikId !== null && od !== '' && doDatuma !== '' && od <= doDatuma,
  });
}

export function useZahteviPrebacivanja(status?: string) {
  return useQuery({
    queryKey: ['zahtevi-prebacivanja', status ?? 'svi'],
    queryFn: async () => (await api.get<ZahtevPrebacivanjaDto[]>('/zahtevi-prebacivanja', { params: { status } })).data,
  });
}

export function usePodnesiPrebacivanje() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { pregledId: number; razlog: string; predlozenaZamenaId: string | null }) =>
      (await api.post<ZahtevPrebacivanjaDto>('/zahtevi-prebacivanja', v)).data,
    onSuccess: inv,
  });
}

/** Inženjer traži da započne svoj PZ pre zakazanog dana; odlučuju Supervizor i Direktor. */
export function useZatraziRanijiPocetak() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { pregledId: number; razlog: string }) =>
      (await api.post<ZahtevPrebacivanjaDto>('/zahtevi-prebacivanja/raniji-pocetak', v)).data,
    onSuccess: inv,
  });
}

export function useOdluciPrebacivanje() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { id: number; odobreno: boolean; zamenaId: string | null }) =>
      (await api.post<ZahtevPrebacivanjaDto>(`/zahtevi-prebacivanja/${v.id}/odluka`, v)).data,
    onSuccess: inv,
  });
}

export function useNeradniDani(godina: number) {
  return useQuery({
    queryKey: ['neradni-dani', godina],
    queryFn: async () => (await api.get<NeradniDanDto[]>('/neradni-dani', { params: { godina } })).data,
  });
}

export function useUnesiNeradne() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { od: string; do: string; naziv: string; vrsta: string }) =>
      (await api.post<NeradniDanDto[]>('/neradni-dani', v)).data,
    onSuccess: inv,
  });
}

export function useObrisiNeradni() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`/neradni-dani/${id}`);
    },
    onSuccess: inv,
  });
}
