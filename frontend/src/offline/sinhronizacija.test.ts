import 'fake-indexeddb/auto';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../api/client';
import { db, dodajOperaciju, dodajSliku, obrisiLokalno, postaviKorisnika, sinhronizuj } from './sinhronizacija';

vi.mock('../api/client', () => ({ api: { get: vi.fn(), post: vi.fn() } }));

type Poziv = { url: string; vrste?: string[] };
let pozivi: Poziv[];
let online: boolean;

function odgovorSinhronizacije(telo: { operacije: { opId: string }[] }) {
  return { data: { prihvacene: telo.operacije.map((o) => o.opId), odbijene: [], serverSada: new Date().toISOString() } };
}

beforeEach(async () => {
  pozivi = [];
  online = false;
  // Blob posle IndexedDB (fake-indexeddb) nije jsdom Blob, pa jsdom FormData odbija append.
  vi.stubGlobal('FormData', class { append() {} });
  vi.spyOn(navigator, 'onLine', 'get').mockImplementation(() => online);
  vi.mocked(api.get).mockResolvedValue({ status: 304, data: null, headers: {} });
  vi.mocked(api.post).mockImplementation(async (url: string, telo: unknown) => {
    if (url === '/sinhronizacija') {
      const t = telo as { operacije: { opId: string; vrsta: string }[] };
      pozivi.push({ url, vrste: t.operacije.map((o) => o.vrsta) });
      return odgovorSinhronizacije(t);
    }
    pozivi.push({ url });
    return { data: {} };
  });
  await Promise.all(db.tables.map((t) => t.clear()));
  await postaviKorisnika('ana');
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

async function radBezInterneta(pregledId: number) {
  await dodajOperaciju(pregledId, { vrsta: 'odgovor', podaci: { stavkaId: 1, vrednost: 0, napomena: null, polja: {} } });
  await dodajSliku(pregledId, 1, new Blob(['jpeg']), 'kamera');
  await dodajOperaciju(pregledId, { vrsta: 'predaja', podaci: {} });
}

describe('sinhronizacija', () => {
  it('slike pregleda se šalju pre predaje (server posle predaje slike ne prima)', async () => {
    await radBezInterneta(5);
    online = true;
    await sinhronizuj();

    expect(pozivi).toEqual([
      { url: '/sinhronizacija', vrste: ['odgovor'] },
      { url: '/fotografije' },
      { url: '/sinhronizacija', vrste: ['predaja'] },
    ]);
    expect(await db.red.count()).toBe(0);
    expect(await db.slike.count()).toBe(0);
  });

  it('slika koju server odbije ostaje na telefonu u odbijenim, a predaja se šalje', async () => {
    await radBezInterneta(5);
    const post = vi.mocked(api.post).getMockImplementation()!;
    vi.mocked(api.post).mockImplementation(async (url: string, telo: unknown) => {
      if (url === '/fotografije') throw { response: { status: 413 } };
      return post(url, telo);
    });
    online = true;
    await sinhronizuj();

    const [odbijena] = await db.odbijene.toArray();
    expect(odbijena.razlog).toMatch(/prevelika/);
    expect(odbijena.slika).toBeDefined();
    expect(await db.red.count()).toBe(0);

    expect(await obrisiLokalno()).toBe(true);
    expect((await db.odbijene.toArray()).map((x) => x.slika)).toHaveLength(1);
  });

  it('bez veze sa serverom slika ostaje u redu, a predaja čeka', async () => {
    await radBezInterneta(5);
    const post = vi.mocked(api.post).getMockImplementation()!;
    vi.mocked(api.post).mockImplementation(async (url: string, telo: unknown) => {
      if (url === '/fotografije') throw new Error('Network Error');
      return post(url, telo);
    });
    online = true;
    await expect(sinhronizuj()).rejects.toThrow();

    expect(await db.slike.count()).toBe(1);
    expect((await db.red.toArray()).map((s) => s.op.vrsta)).toEqual(['predaja']);
  });

  it('posle isteka sesije drugi korisnik ne šalje tuđi red; vlasnik ga šalje kad se prijavi', async () => {
    await dodajOperaciju(5, { vrsta: 'odgovor', podaci: { stavkaId: 1, vrednost: 0, napomena: null, polja: {} } });
    await postaviKorisnika(null);
    await postaviKorisnika('bora');
    online = true;

    await sinhronizuj();
    expect(pozivi.filter((p) => p.url === '/sinhronizacija')).toEqual([]);
    expect(await obrisiLokalno()).toBe(true);
    expect(await db.red.count()).toBe(1);

    await postaviKorisnika('ana');
    await sinhronizuj();
    expect(pozivi).toEqual([{ url: '/sinhronizacija', vrste: ['odgovor'] }]);
    expect(await db.red.count()).toBe(0);
  });

  it('bez prijavljenog korisnika radnja se ne upisuje u red', async () => {
    await postaviKorisnika(null);
    await expect(dodajOperaciju(5, { vrsta: 'predaja', podaci: {} })).rejects.toThrow();
    expect(await db.red.count()).toBe(0);
  });
});
