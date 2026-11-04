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
 */
export interface StavkaReda {
  seq?: number;
  opId: string;
  pregledId: number;
  klijentAt: string;
  op: Operacija;
}

export interface SlikaUCekanju {
  klijentId: string;
  pregledId: number;
  stavkaId: number;
  izvor: 'kamera' | 'galerija';
  blob: Blob;
}

export interface OdbijenaOperacija {
  id?: number;
  opId: string;
  pregledId: number;
  razlog: string;
  at: string;
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
  }
}

export const db = new TpmDb();

/** ID instalacije na ovom telefonu (R10.5). Nije tajna; ako storage nije dostupan, svaki start dobija novi. */
export function uredjajKlijenta(): string {
  try {
    let id = localStorage.getItem('tpm.uredjajKlijenta');
    if (!id) {
      id = crypto.randomUUID();
      localStorage.setItem('tpm.uredjajKlijenta', id);
    }
    return id;
  } catch {
    return 'bez-storage';
  }
}

/** Radnja u pregledu: odmah na lokalnoj kopiji, pa u red za slanje; slanje kreće odmah ako ima veze. */
export async function dodajOperaciju(pregledId: number, op: Operacija): Promise<void> {
  const sada = new Date().toISOString();
  await db.transaction('rw', db.pregledi, db.red, async () => {
    const p = await db.pregledi.get(pregledId);
    if (p) await db.pregledi.put(primeniLokalno(p, op, sada));
    await db.red.add({ opId: crypto.randomUUID(), pregledId, klijentAt: sada, op });
  });
  void sinhronizuj();
}

/** Slika ide u IndexedDB i šalje se pri sinhronizaciji; lokalno se odmah broji uz stavku. */
export async function dodajSliku(pregledId: number, stavkaId: number, blob: Blob, izvor: 'kamera' | 'galerija'): Promise<void> {
  const klijentId = crypto.randomUUID();
  await db.transaction('rw', db.pregledi, db.slike, async () => {
    await db.slike.add({ klijentId, pregledId, stavkaId, izvor, blob });
    const p = await db.pregledi.get(pregledId);
    if (p) {
      for (const s of p.sekcije) for (const u of s.uredjaji) for (const st of u.stavke) if (st.stavkaId === stavkaId) st.fotografije.push(-Date.now());
      await db.pregledi.put(p);
    }
  });
  void sinhronizuj();
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
  if (!navigator.onLine) return Promise.resolve();
  uToku ??= posalji().finally(() => {
    uToku = null;
  });
  return uToku;
}

async function posalji(): Promise<void> {
  // 1) Operacije redom, u paketima.
  for (;;) {
    const deo = await db.red.orderBy('seq').limit(50).toArray();
    if (deo.length === 0) break;
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
        if (razlog) await db.odbijene.add({ opId: s.opId, pregledId: s.pregledId, razlog, at: new Date().toISOString() });
        await db.red.delete(s.seq!);
      }
    });
  }

  // 2) Slike (svaka sa svojim klijentId-jem — ponovljeno slanje ne pravi duplikat).
  for (const s of await db.slike.toArray()) {
    const telo = new FormData();
    telo.append('fajl', s.blob, 'slika.jpg');
    telo.append('klijentId', s.klijentId);
    telo.append('pregledId', String(s.pregledId));
    telo.append('stavkaId', String(s.stavkaId));
    telo.append('izvor', s.izvor);
    try {
      await api.post('/fotografije', telo);
    } catch (err) {
      const status = (err as { response?: { status?: number } }).response?.status;
      if (status === undefined || status >= 500) throw err;   // nema veze ili server ne radi — pokušava se ponovo
      const razlog = (err as { response?: { data?: { greske?: string[] } } }).response?.data?.greske?.[0] ?? 'Slika nije primljena.';
      await db.odbijene.add({ opId: s.klijentId, pregledId: s.pregledId, razlog, at: new Date().toISOString() });
    }
    await db.slike.delete(s.klijentId);
  }

  // 3) Novi paket (ETag) i ponovna primena onoga što je u međuvremenu stalo u red.
  const etag = (await db.meta.get('etag'))?.vrednost;
  const res = await api.get<{ pregledi: PopunjavanjeDto[] }>('/moj-paket', {
    headers: etag ? { 'If-None-Match': etag } : {},
    validateStatus: (s) => s === 200 || s === 304,
  });
  if (res.status === 200) {
    await db.transaction('rw', db.pregledi, db.red, db.meta, async () => {
      const naCekanju = await db.red.orderBy('seq').toArray();
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

/** Briše lokalne podatke (odjava) — samo kad ništa ne čeka slanje, inače bi se urađeno izgubilo. */
export async function obrisiLokalno(): Promise<boolean> {
  if ((await db.red.count()) + (await db.slike.count()) > 0) return false;
  await Promise.all([db.pregledi.clear(), db.odbijene.clear(), db.meta.clear()]);
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
export function useStanjeSinhronizacije() {
  const [online, setOnline] = useState(() => navigator.onLine);
  useEffect(() => {
    const on = () => { setOnline(true); void sinhronizuj(); };
    const off = () => setOnline(false);
    window.addEventListener('online', on);
    window.addEventListener('offline', off);
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off); };
  }, []);
  const promena = useLokalno(() => db.red.count(), [], 0);
  const slika = useLokalno(() => db.slike.count(), [], 0);
  const poslednja = useLokalno(async () => (await db.meta.get('poslednjaSinhronizacija'))?.vrednost ?? null, [], null as string | null);
  return { online, promena, slika, poslednja };
}
