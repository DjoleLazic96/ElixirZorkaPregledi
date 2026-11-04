import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';

/**
 * Popunjavanje pregleda. Svaka radnja je operacija sa `opId` i vremenom sa telefona i ide kroz `/sinhronizacija` —
 * isti ugovor koji rad bez interneta (faza 05) puni iz reda za slanje.
 */
export interface PoljeDto {
  id: number;
  naziv: string;
  tip: 'broj' | 'merenje' | 'dve_vrednosti' | 'da_ne';
  jedinica: string | null;
  oznake: string[];
  prikaz: 'uvek' | 'uz_neispravan';
  obavezno: boolean;
}

export interface OdgovorDto {
  vrednost: 0 | 1;
  napomena: string | null;
  polja: Record<string, number | number[] | boolean>;
}

export interface StavkaPopunjavanjaDto {
  stavkaId: number;
  aktivnost: string;
  odgovorIspravan: string;
  odgovorNeispravan: string;
  polja: PoljeDto[];
  odgovor: OdgovorDto | null;
  fotografije: number[];
}

export interface UredjajPopunjavanjaDto {
  uredjajId: number;
  naziv: string;
  oznaka: string;
  vanFunkcije: boolean;
  prosliPutVanFunkcije: boolean;
  stavke: StavkaPopunjavanjaDto[];
}

export interface SekcijaPopunjavanjaDto {
  sifra: string;
  moja: boolean;
  radi: string;
  uredjaji: UredjajPopunjavanjaDto[];
}

export interface PopunjavanjeDto {
  id: number;
  broj: string;
  tip: string;
  status: string;
  planiraniDatum: string;
  zapocetAt: string | null;
  sekcije: SekcijaPopunjavanjaDto[];
}

export interface SinhronizacijaRezultat {
  prihvacene: string[];
  odbijene: { opId: string; razlog: string }[];
  serverSada: string;
}

export type Operacija =
  | { vrsta: 'odgovor'; podaci: { stavkaId: number; vrednost: 0 | 1; napomena?: string | null; polja?: OdgovorDto['polja'] } }
  | { vrsta: 'van_funkcije'; podaci: { uredjajId: number; vanFunkcije: boolean } }
  | { vrsta: 'sve_ispravno'; podaci: { sekcija: string } }
  | { vrsta: 'predaja'; podaci: Record<string, never> };

export function usePopunjavanje(id: number) {
  return useQuery({
    queryKey: ['popunjavanje', id],
    queryFn: async () => (await api.get<PopunjavanjeDto>(`/pregledi/${id}/popunjavanje`)).data,
  });
}

export function useZapocni(id: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => (await api.post<PopunjavanjeDto>(`/pregledi/${id}/zapocni`)).data,
    onSuccess: (data) => {
      qc.setQueryData(['popunjavanje', id], data);
      void qc.invalidateQueries({ queryKey: ['raspored'] });
    },
  });
}

/** Šalje jednu operaciju; odbijena operacija se vraća kao greška sa razlogom za korisnika. */
export function useOperacija(pregledId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (op: Operacija) => {
      const r = (
        await api.post<SinhronizacijaRezultat>('/sinhronizacija', {
          uredjajKlijenta: null,
          klijentSada: new Date().toISOString(),
          operacije: [{ opId: crypto.randomUUID(), pregledId, klijentAt: new Date().toISOString(), ...op }],
        })
      ).data;
      if (r.odbijene.length > 0) throw new Error(r.odbijene[0].razlog);
      return r;
    },
    onSettled: () => void qc.invalidateQueries({ queryKey: ['popunjavanje', pregledId] }),
  });
}

export function useOtpremiSliku(pregledId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { stavkaId: number; slika: Blob; izvor: 'kamera' | 'galerija' }) => {
      const telo = new FormData();
      telo.append('fajl', v.slika, 'slika.jpg');
      telo.append('klijentId', crypto.randomUUID());
      telo.append('pregledId', String(pregledId));
      telo.append('stavkaId', String(v.stavkaId));
      telo.append('izvor', v.izvor);
      return (await api.post<{ id: number }>('/fotografije', telo)).data;
    },
    onSettled: () => void qc.invalidateQueries({ queryKey: ['popunjavanje', pregledId] }),
  });
}
