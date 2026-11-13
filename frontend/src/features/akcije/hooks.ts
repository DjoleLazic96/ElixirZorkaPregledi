import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';

/** Potrebne akcije i neispravnosti (`/akcije`, `/kvarovi`). */
export interface AkcijaDto {
  id: number;
  broj: string;
  pregledId: number;
  pregled: string;
  uredjajId: number;
  uredjaj: string;
  sekcija: string;
  aktivnost: string;
  vrsta: string;
  hitnost: string;
  opis: string | null;
  rok: string | null;
  rokIstekao: boolean;
  status: 'otvorena' | 'zatvorena' | 'otkazana';
  zaduzen: string | null;
  prijavio: string;
  kreiranoAt: string;
  razlog: string | null;
  stavkaSadaIspravna: boolean;
  kvarBroj: string | null;
  kvarOtvoren: boolean;
  /** Kako je zatvorena (zamerka 49). */
  uradjeno?: string | null;
  uradjenoDatum?: string | null;
  izvrsilac?: string | null;
  imaSlikuZatvaranja?: boolean;
  /** Zahtev za zatvaranje koji čeka odluku. */
  zahtevZatvaranja?: { id: number; podneoId: string; podneo: string; uradjeno: string; datum: string; izvrsilac: string; imaSliku: boolean; kreiranoAt: string } | null;
}

export interface AkcijaDetaljDto {
  akcija: AkcijaDto;
  istorija: { vrsta: string; tekst: string; korisnik: string; at: string }[];
}

export interface KvarDto {
  id: number;
  broj: string;
  uredjajId: number;
  uredjaj: string;
  sekcija: string;
  aktivnost: string;
  otvoren: string;
  zatvoren: string | null;
  trajanjeDana: number;
  prijava: number;
  poslednjaNapomena: string | null;
  otvorioPregledId: number;
  otvorioPregled: string;
  otvorenihAkcija: number;
}

export interface KvarDetaljDto {
  kvar: KvarDto;
  prijave: { pregledId: number; broj: string; datum: string; napomena: string | null }[];
  akcije: AkcijaDto[];
}

export interface AkcijeFilteri {
  status: string | null;
  hitnost: string | null;
  sekcijaId: number | null;
  sadaIspravna: boolean;
  /** Samo akcije sa zahtevom za zatvaranje na čekanju (zamerka 49). */
  saZahtevom?: boolean;
}

export const parametriAkcija = (f: AkcijeFilteri) => ({
  status: f.status ?? undefined, hitnost: f.hitnost ?? undefined, sekcijaId: f.sekcijaId ?? undefined, sadaIspravna: f.sadaIspravna || undefined,
  saZahtevom: f.saZahtevom || undefined,
});

export function useAkcije(f: AkcijeFilteri, enabled = true) {
  return useQuery({ enabled, queryKey: ['akcije', f], queryFn: async () => (await api.get<AkcijaDto[]>('/akcije', { params: parametriAkcija(f) })).data });
}

export function useAkcija(id: number) {
  return useQuery({ queryKey: ['akcije', 'detalj', id], queryFn: async () => (await api.get<AkcijaDetaljDto>(`/akcije/${id}`)).data });
}

export function useOdlukaAkcije(id: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { putanja: 'otkazi'; razlog: string } | { putanja: 'zaduzen'; zaduzen: string | null }) =>
      v.putanja === 'zaduzen'
        ? (await api.put<AkcijaDetaljDto>(`/akcije/${id}/zaduzen`, { zaduzen: v.zaduzen })).data
        : (await api.post<AkcijaDetaljDto>(`/akcije/${id}/${v.putanja}`, { razlog: v.razlog })).data,
    onSuccess: (d) => {
      qc.setQueryData(['akcije', 'detalj', id], d);
      void qc.invalidateQueries({ queryKey: ['akcije'] });
      void qc.invalidateQueries({ queryKey: ['kvarovi'] });
    },
  });
}

export interface KvaroviFilteri {
  otvoreno: boolean | null;
  sekcijaId: number | null;
  pretraga: string;
}

export const parametriKvarova = (f: KvaroviFilteri) => ({
  otvoreno: f.otvoreno ?? undefined, sekcijaId: f.sekcijaId ?? undefined, pretraga: f.pretraga || undefined,
});

export function useKvarovi(f: KvaroviFilteri) {
  return useQuery({ queryKey: ['kvarovi', f], queryFn: async () => (await api.get<KvarDto[]>('/kvarovi', { params: parametriKvarova(f) })).data });
}

export function useKvar(id: number) {
  return useQuery({ queryKey: ['kvarovi', 'detalj', id], queryFn: async () => (await api.get<KvarDetaljDto>(`/kvarovi/${id}`)).data });
}
