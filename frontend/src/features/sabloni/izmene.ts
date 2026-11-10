import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { SablonVerzijaDto } from '../../api/types';

/** Izmene šablona u aplikaciji: nacrt, objava, verzije, predlozi, istorijat i uputstva (faza 10). */
export interface RazlikaDto { vrsta: 'dodato' | 'izbaceno' | 'izmenjeno' | 'pomereno'; sekcija: string; uredjaj: string; aktivnost: string; bilo: string | null }

export interface PredlogDto {
  id: number;
  sablon: string;
  vrsta: 'dodavanje' | 'izbacivanje' | 'ispravka';
  stavkaId: number | null;
  stavka: string | null;
  opis: string;
  razlog: string;
  podneo: string;
  kreiranoAt: string;
  status: 'zahtev' | 'odobren' | 'odbijen';
  odlucio: string | null;
  odgovor: string | null;
  verzija: number | null;
}

export interface NacrtDto {
  verzija: SablonVerzijaDto;
  osnovnaVerzija: number | null;
  razlike: RazlikaDto[];
  predlozi: PredlogDto[];
  zakazana: { broj: number; vaziOd: string } | null;
}

export interface VerzijaKratkoDto { broj: number; status: string; vaziOd: string | null; objavljenoAt: string | null; objavio: string | null }
export interface SablonDogadjajDto { id: number; sablon: string; verzija: number | null; vrsta: string; opis: string; korisnikId: string | null; korisnik: string; at: string }
export interface UputstvoDto { aktivnostId: number; aktivnost: string; tekst: string | null; slike: number[]; azurirao: string | null; azuriranoAt: string | null }

export const VRSTE_PREDLOGA = [
  { value: 'dodavanje', label: 'Dodavanje' },
  { value: 'izbacivanje', label: 'Izbacivanje' },
  { value: 'ispravka', label: 'Ispravka teksta' },
] as const;

export const VRSTE_DOGADJAJA: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'prvi_uvoz', label: 'Prvi uvoz' },
  { value: 'nacrt', label: 'Otvoren nacrt' },
  { value: 'nacrt_izmena', label: 'Izmena u nacrtu' },
  { value: 'nacrt_odbacen', label: 'Odbačen nacrt' },
  { value: 'predlog', label: 'Predlog' },
  { value: 'predlog_odobren', label: 'Odobren predlog' },
  { value: 'predlog_odbijen', label: 'Odbijen predlog' },
  { value: 'objava', label: 'Objava verzije' },
  { value: 'vazi', label: 'Verzija postala važeća' },
];

export const STATUS_VERZIJE: Record<string, string> = { nacrt: 'nacrt', objavljena: 'važeća', zakazana: 'zakazana', zamenjena: 'zamenjena' };

export function useVerzije(tip: string) {
  return useQuery({ queryKey: ['sabloni', tip, 'verzije'], queryFn: async () => (await api.get<VerzijaKratkoDto[]>(`/sabloni/${tip}/verzije`)).data });
}

export function useVerzija(tip: string, broj: number | null) {
  return useQuery({
    queryKey: ['sabloni', tip, 'verzija', broj],
    queryFn: async () => (await api.get<SablonVerzijaDto>(broj === null ? `/sabloni/${tip}` : `/sabloni/${tip}/verzije/${broj}`)).data,
  });
}

export function useNacrt(tip: string) {
  return useQuery({
    queryKey: ['sabloni', tip, 'nacrt'],
    queryFn: async () => {
      const r = await api.get<NacrtDto | ''>(`/sabloni/${tip}/nacrt`);
      return r.status === 204 || !r.data ? null : r.data;
    },
  });
}

/** Svaka izmena nacrta vraća ceo nacrt (sa razlikama), pa se keš samo zameni. */
export function useIzmenaNacrta(tip: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { metod: 'post' | 'put' | 'delete'; putanja?: string; telo?: object }) => {
      const url = `/sabloni/${tip}/nacrt${v.putanja ?? ''}`;
      const r = v.metod === 'delete' ? await api.delete<NacrtDto>(url) : v.metod === 'put' ? await api.put<NacrtDto>(url, v.telo) : await api.post<NacrtDto>(url, v.telo);
      return r.status === 204 ? null : r.data;
    },
    onSuccess: (d) => qc.setQueryData(['sabloni', tip, 'nacrt'], d ?? null),
  });
}

export function useObjavi(tip: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (vaziOd: string) => (await api.post<VerzijaKratkoDto>(`/sabloni/${tip}/nacrt/objavi`, { vaziOd })).data,
    onSuccess: () => {
      qc.setQueryData(['sabloni', tip, 'nacrt'], null);
      void qc.invalidateQueries({ queryKey: ['sabloni'] });
      void qc.invalidateQueries({ queryKey: ['uredjaji'] });
    },
  });
}

export function usePredlozi(status: string | null) {
  return useQuery({
    queryKey: ['predlozi', status],
    queryFn: async () => (await api.get<PredlogDto[]>('/predlozi-izmena', { params: { status: status ?? undefined } })).data,
  });
}

export function usePodnesiPredlog() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { tip: string; vrsta: string; stavkaId: number | null; opis: string; razlog: string }) => (await api.post<PredlogDto>('/predlozi-izmena', v)).data,
    onSuccess: () => void qc.invalidateQueries({ queryKey: ['predlozi'] }),
  });
}

export function useOdlukaPredloga() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { id: number; odobren: boolean; odgovor: string | null }) =>
      (await api.post<PredlogDto>(`/predlozi-izmena/${v.id}/odluka`, { odobren: v.odobren, odgovor: v.odgovor })).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['predlozi'] });
      void qc.invalidateQueries({ queryKey: ['zvono'] });
    },
  });
}

export interface IstorijatFilteri { tip: string | null; vrsta: string | null; korisnikId: string | null; od: string; do: string }

export function useIstorijat(f: IstorijatFilteri) {
  return useQuery({
    queryKey: ['sabloni', 'istorijat', f],
    queryFn: async () =>
      (await api.get<SablonDogadjajDto[]>('/sabloni/istorijat', { params: { tip: f.tip ?? undefined, vrsta: f.vrsta ?? undefined, korisnikId: f.korisnikId ?? undefined, od: f.od || undefined, do: f.do || undefined } })).data,
  });
}

export function useUputstvo(aktivnostId: number) {
  return useQuery({ queryKey: ['uputstvo', aktivnostId], queryFn: async () => (await api.get<UputstvoDto>(`/aktivnosti/${aktivnostId}/uputstvo`)).data });
}

export function useIzmenaUputstva(aktivnostId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { tekst: string } | { slika: File } | { ukloni: number }) => {
      const url = `/aktivnosti/${aktivnostId}/uputstvo`;
      if ('tekst' in v) return (await api.put<UputstvoDto>(url, { tekst: v.tekst })).data;
      if ('ukloni' in v) return (await api.delete<UputstvoDto>(`${url}/slike/${v.ukloni}`)).data;
      const telo = new FormData();
      telo.append('fajl', v.slika);
      return (await api.post<UputstvoDto>(`${url}/slike`, telo)).data;
    },
    onSuccess: (d) => {
      qc.setQueryData(['uputstvo', aktivnostId], d);
      void qc.invalidateQueries({ queryKey: ['aktivnosti'] });
    },
  });
}
