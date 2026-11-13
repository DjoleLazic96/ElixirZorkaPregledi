import { useMutation } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useAuth } from '../auth/AuthProvider';
import { db, dodajOperaciju, dodajSliku, obezbediPregled, useLokalno } from '../../offline/sinhronizacija';

/**
 * Popunjavanje pregleda radi nad lokalnom kopijom u IndexedDB-u (rad bez interneta, faza 05): svaka radnja se odmah
 * vidi i staje u red za slanje kroz `/sinhronizacija`.
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
  /** Slike iz ranije predate verzije (pre ove dorade): vide se, ali se ne brišu (zamerka 15). */
  stareFotografije?: number[];
  /** Uputstvo aktivnosti (R2.4): tekst je u paketu, pa radi i bez interneta; slike se učitavaju sa servera. */
  aktivnostId: number;
  uputstvo: string | null;
  uputstvoSlika: number;
}

export interface AkcijaKratkoDto {
  id: number;
  broj: string;
  aktivnost: string;
  stavkaId: number | null;
  vrsta: string;
  hitnost: string;
  opis: string | null;
}

export interface UredjajPopunjavanjaDto {
  uredjajId: number;
  naziv: string;
  oznaka: string;
  vanFunkcije: boolean;
  prosliPutVanFunkcije: boolean;
  stavke: StavkaPopunjavanjaDto[];
  /** Otvorene akcije — pop-up „da li je urađena“ (R4.7). */
  otvoreneAkcije: AkcijaKratkoDto[];
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
  /** Server: planiran, u_radu, ceka_overu…; samo na telefonu: `predato_ceka` („Predato – čeka slanje“). */
  status: string;
  planiraniDatum: string;
  zapocetAt: string | null;
  verzijaId: number | null;
  sekcije: SekcijaPopunjavanjaDto[];
}

export interface SinhronizacijaRezultat {
  prihvacene: string[];
  odbijene: { opId: string; razlog: string }[];
  serverSada: string;
}

export type Operacija =
  | { vrsta: 'zapocni'; podaci: { verzijaId: number | null } }
  | { vrsta: 'odgovor'; podaci: { stavkaId: number; vrednost: 0 | 1; napomena?: string | null; polja?: OdgovorDto['polja'] } }
  | { vrsta: 'van_funkcije'; podaci: { uredjajId: number; vanFunkcije: boolean } }
  | { vrsta: 'sve_ispravno'; podaci: { sekcija: string } }
  | { vrsta: 'predaja'; podaci: Record<string, never> }
  | { vrsta: 'akcija'; podaci: { stavkaId: number; vrsta: string; hitnost: string; opis: string | null } }
  | { vrsta: 'akcija_odgovor'; podaci: { akcijaId: number; uradjeno: boolean } }
  | { vrsta: 'obrisi_sliku'; podaci: { fotografijaId: number; stavkaId: number } };

/** Pregled iz lokalne kopije; ako ga nema, preuzima se sa servera (potrebna je veza samo prvi put). */
export function usePopunjavanje(id: number) {
  const pregled = useLokalno(() => db.pregledi.get(id), [id], undefined as PopunjavanjeDto | undefined);
  const [greska, setGreska] = useState<unknown>(null);
  const [ucitava, setUcitava] = useState(true);
  useEffect(() => {
    let ziv = true;
    obezbediPregled(id).catch((e) => ziv && setGreska(e)).finally(() => ziv && setUcitava(false));
    return () => { ziv = false; };
  }, [id]);
  const korisnikId = useAuth().me?.user.id;
  const odbijene = useLokalno(() => db.odbijene.where('pregledId').equals(id).filter((o) => o.korisnikId === korisnikId && o.slika === undefined).toArray(), [id, korisnikId], []);
  return { data: pregled, isLoading: ucitava && !pregled, isError: !pregled && greska !== null, error: greska, odbijene };
}

export function useZapocni(id: number) {
  return useMutation({
    networkMode: 'always',
    mutationFn: async (verzijaId: number | null) => dodajOperaciju(id, { vrsta: 'zapocni', podaci: { verzijaId } }),
  });
}

/** Radnja u pregledu: lokalno odmah, server kad stigne veza; razlog odbijanja stiže u `odbijene`. */
export function useOperacija(pregledId: number) {
  // Upis je lokalan (IndexedDB), pa ne sme da čeka internet; podrazumevani 'online' bi bez veze pauzirao svaki klik.
  return useMutation({ networkMode: 'always', mutationFn: async (op: Operacija) => dodajOperaciju(pregledId, op) });
}

export function useOtpremiSliku(pregledId: number) {
  return useMutation({
    networkMode: 'always',
    mutationFn: async (v: { stavkaId: number; slika: Blob; izvor: 'kamera' | 'galerija' }) => dodajSliku(pregledId, v.stavkaId, v.slika, v.izvor),
  });
}
