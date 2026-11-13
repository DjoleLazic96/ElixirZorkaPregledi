import { noviId } from '../lib/id';
import Dexie, { liveQuery, type Table } from 'dexie';
import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Operacija, PopunjavanjeDto, SinhronizacijaRezultat } from '../features/popunjavanje/hooks';
import { primeniLokalno } from './lokalno';

/**
 * Rad bez interneta (spec §5.1, odluka 2026-11-04). U IndexedDB su: paket (dodeljeni pregledi), red operacija za slanje
 * (outbox), slike koje čekaju slanje i odbijene operacije. Svaka radnja se primeni na lokalnu kopiju i stane u red;
 * `sinhronizuj()` šalje red redom (server je idempotentan po `opId`), pa preuzme novi paket (ETag) i na njega ponovo
 * primeni ono što još nije poslato. Token se ne čuva ovde (blueprint 12 §38) — samo podaci pregleda.
 *
 * Telefon može da deli više inženjera: svaka stavka reda, slika i odbijena operacija nosi `korisnikId`, a šalje se samo
 * ono što pripada prijavljenom korisniku. Neposlato urađeno prethodnog korisnika ostaje na telefonu dok se on ne prijavi.
 */
export interface StavkaReda {
  seq?: number;
  korisnikId?: string;
  opId: string;
  pregledId: number;
  klijentAt: string;
  op: Operacija;
}

export interface SlikaUCekanju {
  klijentId: string;
  /** Negativan ID pod kojim se slika vidi uz stavku dok ne stigne na server (za prikaz i brisanje). */
  lokalniId?: number;
  korisnikId?: string;
  pregledId: number;
  stavkaId: number;
  izvor: 'kamera' | 'galerija';
  blob: Blob;
}

export interface OdbijenaOperacija {
  id?: number;
  korisnikId?: string;
  opId: string;
  pregledId: number;
  razlog: string;
  at: string;
  /** Slika koju server nije primio — čuva se na telefonu da se ne izgubi. */
  slika?: Blob;
}

class TpmDb extends Dexie {
  pregledi!: Table<PopunjavanjeDto, number>;
  red!: Table<StavkaReda, number>;
  slike!: Table<SlikaUCekanju, string>;
  odbijene!: Table<OdbijenaOperacija, number>;
  meta!: Table<{ kljuc: string; vrednost: string }, string>;

  constructor() {
    super('tpm-pregledi');
    this.version(1).stores({
      pregledi: 'id',
      red: '++seq, pregledId',
      slike: 'klijentId, pregledId',
      odbijene: '++id, pregledId',
      meta: 'kljuc',
    });
    this.version(2).stores({
      red: '++seq, pregledId, korisnikId',
      slike: 'klijentId, pregledId, korisnikId',
      odbijene: '++id, pregledId, korisnikId',
    });
  }
}

export const db = new TpmDb();

let korisnik: string | null = null;

/**
 * Poziva se pri prijavi, odjavi i isteku sesije. Stavke iz verzije bez `korisnikId` pripadaju samo vlasniku paketa
 * (zamerka 14). Kad se prijavi drugi korisnik, briše se samo paket (pregledi i ETag) — tuđi red ostaje.
 */
export async function postaviKorisnika(id: string | null): Promise<void> {
  korisnik = id;
  if (id === null) return;
  await db.transaction('rw', [db.pregledi, db.red, db.slike, db.odbijene, db.meta], async () => {
    const vlasnik = (await db.meta.get('korisnikPaketa'))?.vrednost;
    // Stavke stare verzije bez korisnika pripadaju samo vlasniku paketa — ne prvom ko se prijavi (zamerka 14).
    if (vlasnik === id) {
      await db.red.filter((x) => x.korisnikId === undefined).modify({ korisnikId: id });
      await db.slike.filter((x) => x.korisnikId === undefined).modify({ korisnikId: id });
      await db.odbijene.filter((x) => x.korisnikId === undefined).modify({ korisnikId: id });
    }
    if (vlasnik !== id) {
      await db.pregledi.clear();
      await db.meta.bulkDelete(['etag', 'poslednjaSinhronizacija']);
      await db.meta.put({ kljuc: 'korisnikPaketa', vrednost: id });
    }
  });
}

function prijavljen(): string {
  if (!korisnik) throw new Error('Niste prijavljeni.');
  return korisnik;
}

/** ID instalacije na ovom telefonu (R10.5). Nije tajna; ako storage nije dostupan, svaki start dobija novi. */
function uredjajKlijenta(): string {
  try {
    let id = localStorage.getItem('tpm.uredjajKlijenta');
    if (!id) {
      id = noviId();
      localStorage.setItem('tpm.uredjajKlijenta', id);
    }
    return id;
  } catch {
    return 'bez-storage';
  }
}

/** Radnja u pregledu: odmah na lokalnoj kopiji, pa u red za slanje; slanje kreće odmah ako ima veze. */
export async function dodajOperaciju(pregledId: number, op: Operacija): Promise<void> {
  const korisnikId = prijavljen();
  const sada = new Date().toISOString();
  await db.transaction('rw', db.pregledi, db.red, async () => {
    const p = await db.pregledi.get(pregledId);
    if (p) await db.pregledi.put(primeniLokalno(p, op, sada));
    await db.red.add({ korisnikId, opId: noviId(), pregledId, klijentAt: sada, op });
  });
  void sinhronizuj();
}

/** Slika ide u IndexedDB i šalje se pri sinhronizaciji; lokalno se odmah broji uz stavku. */
export async function dodajSliku(pregledId: number, stavkaId: number, blob: Blob, izvor: 'kamera' | 'galerija'): Promise<void> {
  const korisnikId = prijavljen();
  const klijentId = noviId();
  const lokalniId = -(Date.now() * 1000 + Math.floor(Math.random() * 1000));
  await db.transaction('rw', db.pregledi, db.slike, async () => {
    await db.slike.add({ klijentId, lokalniId, korisnikId, pregledId, stavkaId, izvor, blob });
    const p = await db.pregledi.get(pregledId);
    if (p) {
      for (const s of p.sekcije) for (const u of s.uredjaji) for (const st of u.stavke) if (st.stavkaId === stavkaId) st.fotografije.push(lokalniId);
      await db.pregledi.put(p);
    }
  });
  void sinhronizuj();
}

/** Slika koja još čeka slanje (negativan ID uz stavku), za prikaz na telefonu. */
export async function lokalnaSlika(lokalniId: number): Promise<Blob | undefined> {
  return (await db.slike.filter((s) => s.lokalniId === lokalniId).first())?.blob;
}

/**
 * Brisanje slike sa stavke (zamerka 5). Neposlata slika se samo izbaci iz reda; poslata se briše operacijom koja ide
 * kao i ostale (radi i bez interneta, server je idempotentan).
 */
export async function obrisiSliku(pregledId: number, stavkaId: number, id: number): Promise<void> {
  if (id < 0) {
    await db.transaction('rw', db.pregledi, db.slike, async () => {
      await db.slike.filter((s) => s.lokalniId === id).delete();
      const p = await db.pregledi.get(pregledId);
      if (p) {
        for (const s of p.sekcije) for (const u of s.uredjaji) for (const st of u.stavke)
          if (st.stavkaId === stavkaId) st.fotografije = st.fotografije.filter((x) => x !== id);
        await db.pregledi.put(p);
      }
    });
    return;
  }
  await dodajOperaciju(pregledId, { vrsta: 'obrisi_sliku', podaci: { fotografijaId: id, stavkaId } });
}

/** Preuzima pregled sa servera ako ga nema lokalno (npr. otvoren direktno, a nije u paketu). */
export async function obezbediPregled(id: number): Promise<void> {
  if (await db.pregledi.get(id)) return;
  const p = (await api.get<PopunjavanjeDto>(`/pregledi/${id}/popunjavanje`)).data;
  await db.pregledi.put(p);
}

let uToku: Promise<void> | null = null;

/** Jedno slanje u isto vreme; poziva se posle svake radnje, pri povratku veze i periodično. */
export function sinhronizuj(): Promise<void> {
  const k = korisnik;
  if (!navigator.onLine || !k) return Promise.resolve();
  uToku ??= posalji(k).finally(() => {
    uToku = null;
  });
  return uToku;
}

function status(err: unknown): number | undefined {
  return (err as { response?: { status?: number } }).response?.status;
}

/** Nema veze, server ne radi ili je istovremena izmena (409) — pokušava se ponovo pri sledećoj sinhronizaciji. */
function privremena(err: unknown): boolean {
  const s = status(err);
  return s === undefined || s === 409 || s === 429 || s >= 500;
}

/**
 * Slike jednog pregleda (ili svih). Svaka ima svoj klijentId, pa ponovljeno slanje ne pravi duplikat. Slika koju server
 * trajno odbije prelazi u `odbijene` zajedno sa sadržajem — ne briše se sa telefona.
 */
async function posaljiSlike(korisnikId: string, pregledId?: number): Promise<void> {
  const slike = await db.slike.where('korisnikId').equals(korisnikId).toArray();
  for (const s of slike.filter((x) => pregledId === undefined || x.pregledId === pregledId)) {
    const telo = new FormData();
    telo.append('fajl', s.blob, 'slika.jpg');
    telo.append('klijentId', s.klijentId);
    telo.append('pregledId', String(s.pregledId));
    telo.append('stavkaId', String(s.stavkaId));
    telo.append('izvor', s.izvor);
    try {
      await api.post('/fotografije', telo);
      await db.slike.delete(s.klijentId);
    } catch (err) {
      if (privremena(err)) throw err;
      const razlog =
        status(err) === 413
          ? 'Slika je prevelika i nije poslata. Sačuvana je na telefonu.'
          : ((err as { response?: { data?: { greske?: string[] } } }).response?.data?.greske?.[0] ?? 'Slika nije primljena.');
      await db.transaction('rw', db.slike, db.odbijene, async () => {
        await db.odbijene.add({ korisnikId, opId: s.klijentId, pregledId: s.pregledId, razlog, at: new Date().toISOString(), slika: s.blob });
        await db.slike.delete(s.klijentId);
      });
    }
  }
}

async function posalji(korisnikId: string): Promise<void> {
  const odbijenihPre = await db.odbijene.where('korisnikId').equals(korisnikId).count();
  // 1) Operacije redom, u paketima. Predaja pregleda čeka da se pošalju njegove slike — server posle predaje slike ne prima.
  for (;;) {
    const red = await db.red.orderBy('seq').filter((x) => x.korisnikId === korisnikId).limit(50).toArray();
    if (red.length === 0) break;
    const saSlikama = new Set((await db.slike.where('korisnikId').equals(korisnikId).toArray()).map((x) => x.pregledId));
    const i = red.findIndex((x) => x.op.vrsta === 'predaja' && saSlikama.has(x.pregledId));
    const deo = i === -1 ? red : red.slice(0, i);
    if (deo.length === 0) {
      await posaljiSlike(korisnikId, red[0].pregledId);
      continue;
    }
    const r = (
      await api.post<SinhronizacijaRezultat>('/sinhronizacija', {
        uredjajKlijenta: uredjajKlijenta(),
        klijentSada: new Date().toISOString(),
        operacije: deo.map((s) => ({ opId: s.opId, pregledId: s.pregledId, klijentAt: s.klijentAt, vrsta: s.op.vrsta, podaci: s.op.podaci })),
      })
    ).data;
    const odbijene = new Map(r.odbijene.map((o) => [o.opId, o.razlog]));
    await db.transaction('rw', db.red, db.odbijene, async () => {
      for (const s of deo) {
        const razlog = odbijene.get(s.opId);
        if (razlog) await db.odbijene.add({ korisnikId, opId: s.opId, pregledId: s.pregledId, razlog, at: new Date().toISOString() });
        await db.red.delete(s.seq!);
      }
    });
  }

  // 2) Ostale slike.
  await posaljiSlike(korisnikId);

  // 3) Novi paket (ETag) i ponovna primena onoga što je u međuvremenu stalo u red. Ako je server nešto odbio, lokalna
  // kopija ne sme da zadrži odbijeno (npr. „Predato – čeka slanje“ za odbijenu predaju) — paket se preuzima ceo (zamerka 8).
  if ((await db.odbijene.where('korisnikId').equals(korisnikId).count()) > odbijenihPre) await db.meta.delete('etag');
  const etag = (await db.meta.get('etag'))?.vrednost;
  const res = await api.get<{ pregledi: PopunjavanjeDto[] }>('/moj-paket', {
    headers: etag ? { 'If-None-Match': etag } : {},
    validateStatus: (s) => s === 200 || s === 304,
  });
  if (res.status === 200) {
    await db.transaction('rw', db.pregledi, db.red, db.meta, async () => {
      const naCekanju = await db.red.orderBy('seq').filter((x) => x.korisnikId === korisnikId).toArray();
      await db.pregledi.clear();
      for (let p of res.data.pregledi) {
        for (const s of naCekanju.filter((x) => x.pregledId === p.id)) p = primeniLokalno(p, s.op, s.klijentAt);
        await db.pregledi.put(p);
      }
      const novi = res.headers['etag'] as string | undefined;
      if (novi) await db.meta.put({ kljuc: 'etag', vrednost: novi });
    });
  }
  await db.meta.put({ kljuc: 'poslednjaSinhronizacija', vrednost: new Date().toISOString() });
}

/** „U redu“ na obaveštenju o odbijenim promenama: brišu se poruke za pregled (odbijene slike ostaju — jedini primerak). */
export async function zatvoriOdbijene(pregledId: number): Promise<void> {
  await db.odbijene.where('pregledId').equals(pregledId).filter((x) => x.slika === undefined).delete();
}

/** Briše lokalne podatke prijavljenog korisnika (odjava) — samo kad ništa njegovo ne čeka slanje, inače bi se urađeno izgubilo. Odbijene slike ostaju (jedini primerak). */
export async function obrisiLokalno(): Promise<boolean> {
  const k = korisnik;
  if (!k) return true;
  const ceka = (await db.red.where('korisnikId').equals(k).count()) + (await db.slike.where('korisnikId').equals(k).count());
  if (ceka > 0) return false;
  await db.transaction('rw', db.pregledi, db.odbijene, db.meta, async () => {
    await db.pregledi.clear();
    await db.odbijene.where('korisnikId').equals(k).filter((x) => x.slika === undefined).delete();
    await db.meta.bulkDelete(['etag', 'poslednjaSinhronizacija', 'korisnikPaketa']);
  });
  return true;
}

/** React pretplata na IndexedDB upit (Dexie `liveQuery`). */
export function useLokalno<T>(upit: () => Promise<T> | T, zavisnosti: unknown[], pocetno: T): T {
  const [v, setV] = useState<T>(pocetno);
  useEffect(() => {
    const sub = liveQuery(upit).subscribe({ next: setV, error: () => setV(pocetno) });
    return () => sub.unsubscribe();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, zavisnosti);
  return v;
}

/** Stanje veze i reda: za traku „Bez interneta“ i „Poslednja sinhronizacija“ (R10.3, R10.4). */
export function useStanjeSinhronizacije(korisnikId: string | undefined) {
  const [online, setOnline] = useState(() => navigator.onLine);
  useEffect(() => {
    const on = () => { setOnline(true); void sinhronizuj(); };
    const off = () => setOnline(false);
    window.addEventListener('online', on);
    window.addEventListener('offline', off);
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off); };
  }, []);
  const k = korisnikId ?? '';
  const promena = useLokalno(() => db.red.where('korisnikId').equals(k).count(), [k], 0);
  const slika = useLokalno(() => db.slike.where('korisnikId').equals(k).count(), [k], 0);
  const poslednja = useLokalno(async () => (await db.meta.get('poslednjaSinhronizacija'))?.vrednost ?? null, [], null as string | null);
  return { online, promena, slika, poslednja };
}
