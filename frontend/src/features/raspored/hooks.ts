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
  /** Planiran pregled je ostao inženjeru koji je tog dana odsutan (zamerka 3). */
  inzenjerOdsutan?: boolean;
  /** Zahtev inženjera koji čeka odluku (zamerke 9 i 10). */
  zahtevNaCekanju?: 'prebacivanje' | 'raniji_pocetak' | null;
  /** Propušten i automatski prenet (crveno „nije urađen – prenet“); inače ručno pomeren (sivo, zamerka 29). */
  propusten?: boolean;
  /** Posle izmene: dan koji je ostao bez DP-a usred niza — nudi se „Povuci raspored“ (zamerka 29). */
  slobodanDan?: string | null;
  /** DP stoji na neradnom danu (zamerka 52). */
  naNeradniDan?: boolean;
}

export interface RasporedDto {
  pregledi: PregledKalendarDto[];
  neradniDani: { datum: string; naziv: string }[];
  odsustva: { korisnikId: string; ime: string; od: string; do: string; vrsta: string }[];
  /** DP se radi i na dan PZ-a (R3.9) — tada se DP-ovi ne pomeraju. */
  dpNaDanPz?: boolean;
  /** Subote i nedelje unete kao radni dan (zamerka 68) — tu se DP dodaje ručno. */
  radniDani?: { datum: string; naziv: string }[];
}

export interface PredlogStavka {
  datum: string;
  tip: string;
  inzenjerId: string | null;
  inzenjer: string;
  /** Posle ovog dana već postoje planirani DP-ovi — dan je rupa usred niza (zamerka 30). */
  rupa?: boolean;
  /** Poslednji postojeći DP pre ovog dana (za preračun tipa kad se dan odštiklira). */
  datumPre?: string | null;
  tipPre?: string | null;
}

export interface InzenjerDto {
  id: string;
  ime: string;
}

/** Oznaka i boja statusa odsustva. */
export const STATUS_ODSUSTVA = { zahtev: ['amber', 'čeka odluku'], odobreno: ['primary', 'odobreno'], odbijeno: ['red', 'odbijeno'], otkazano: ['gray', 'otkazano'] } as const;

export interface OdsustvoDto {
  id: number;
  korisnikId: string;
  ime: string;
  od: string;
  do: string;
  vrsta: string;
  napomena: string | null;
  status: 'zahtev' | 'odobreno' | 'odbijeno' | 'otkazano';
  razlogOdbijanja: string | null;
  kreiranoAt: string;
  /** Posle skraćivanja ili otkazivanja: pregledi prebačeni zbog ovog odsustva koji mogu da se vrate (zamerka 55). */
  zaVracanje?: PregledKalendarDto[] | null;
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
  vrsta: 'drzavni' | 'verski' | 'fabrika' | 'radni';
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
  { value: 'radni', label: 'Radni dan (subota/nedelja)' },
];

const KEYS = ['raspored', 'odsustva', 'zahtevi-prebacivanja', 'neradni-dani'] as const;

function useInvalidiraj() {
  const qc = useQueryClient();
  return () => KEYS.forEach((k) => void qc.invalidateQueries({ queryKey: [k] }));
}

export function useRaspored(od: string, doDatuma: string, enabled = true) {
  return useQuery({
    enabled,
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
    mutationFn: async (v: { stavke: { datum: string; tip: string; inzenjerId: string | null }[]; uskladi: boolean; postujTip?: boolean }) =>
      (await api.post<PregledKalendarDto[]>('/raspored/potvrdi', v)).data,
    onSuccess: inv,
  });
}

/** Predlog za ručno dodavanje DP-a na jedan dan (zamerka 68): sledeći DP po redu i inženjer po rotaciji. */
export function usePredlogDana(datum: string) {
  return useQuery({
    queryKey: ['raspored', 'predlog-dana', datum],
    queryFn: async () => (await api.get<PredlogStavka>('/raspored/predlog-dana', { params: { datum } })).data,
    retry: false,
  });
}

/** Otkazivanje planiranog pregleda (zamerka 27); za DP `pomeri` = „Pomeri raspored“ umesto poništavanja. */
export function useOtkaziPregled() {
  const inv = useInvalidiraj();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { id: number; razlog: string; pomeri: boolean }) =>
      (await api.post<PregledKalendarDto>(`/pregledi/${v.id}/otkazi`, { razlog: v.razlog, pomeri: v.pomeri })).data,
    onSuccess: () => {
      inv();
      void qc.invalidateQueries({ queryKey: ['pregledi'] });
    },
  });
}

/** „Povuci raspored“ u oslobođen dan (zamerka 29). */
export function usePovuci() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (datum: string) => (await api.post<{ pomereno: number }>('/raspored/povuci', { datum })).data,
    onSuccess: inv,
  });
}

/** „Promeni DP“ (zamerka 31): drugi tip planiranog DP-a, uz usklađivanje narednih. */
export function usePromeniTipDp() {
  const inv = useInvalidiraj();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: { id: number; tip: string; uskladi: boolean }) =>
      (await api.put<PregledKalendarDto>(`/pregledi/${v.id}/tip`, { tip: v.tip, uskladi: v.uskladi })).data,
    onSuccess: () => {
      inv();
      void qc.invalidateQueries({ queryKey: ['pregledi'] });
    },
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

/** Izmena perioda odsustva (Supervizor/Direktor). */
export function useIzmeniOdsustvo() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { id: number; od: string; do: string }) => (await api.put<OdsustvoDto>(`/odsustva/${v.id}`, { od: v.od, do: v.do })).data,
    onSuccess: inv,
  });
}

/** „Vrati preglede“ posle skraćenog ili otkazanog odsustva (zamerka 55). */
export function useVratiPreglede() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { id: number; pregledIds: number[] }) => (await api.post<{ vraceno: number }>(`/odsustva/${v.id}/vrati-preglede`, { pregledIds: v.pregledIds })).data,
    onSuccess: inv,
  });
}

/** Otkazivanje odsustva; inženjer tako povlači svoj zahtev dok o njemu nije odlučeno. */
export function useOtkaziOdsustvo() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (id: number) => (await api.post<OdsustvoDto>(`/odsustva/${id}/otkazi`)).data,
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

export function useZahteviPrebacivanja(status?: string, enabled = true) {
  return useQuery({
    enabled,
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

/** Inženjer otkazuje svoj zahtev (prebacivanje ili raniji početak) dok čeka odluku (zamerka 47). */
export function useOtkaziZahtevPrebacivanja() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (id: number) => (await api.post<ZahtevPrebacivanjaDto>(`/zahtevi-prebacivanja/${id}/otkazi`)).data,
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
    mutationFn: async (v: { od: string; do: string; naziv: string; vrsta: string; pomeriDp?: boolean }) =>
      (await api.post<NeradniDanDto[]>('/neradni-dani', v)).data,
    onSuccess: inv,
  });
}

export function useObrisiNeradni() {
  const inv = useInvalidiraj();
  return useMutation({
    mutationFn: async (v: { id: number; pomeriDp?: boolean }) =>
      (await api.delete<{ slobodanDan: string | null } | ''>(`/neradni-dani/${v.id}`, { params: v.pomeriDp ? { pomeriDp: true } : undefined })).data || { slobodanDan: null },
    onSuccess: inv,
  });
}

/**
 * DP na dan za koji se zakazuje ili na koji se pomera PZ: nezapočet se pomera napred (R3.9, zamerka 43), a započet ostaje
 * i taj dan se rade i DP i PZ (zamerka 53). Null kad tog dana nema DP-a ili se DP radi i na dan PZ-a (podešavanje).
 */
export function useDpKojiSePomera(datum: string | null): PregledKalendarDto | null {
  const r = useRaspored(datum ?? '', datum ?? '', datum !== null);
  if (!datum || !r.data || r.data.dpNaDanPz) return null;
  return r.data.pregledi.find((p) => p.tip.startsWith('DP') && (p.status === 'planiran' || p.status === 'u_radu') && p.planiraniDatum === datum) ?? null;
}
