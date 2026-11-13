import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { expect, request, test as setup, type APIRequestContext } from '@playwright/test';
import { ADMIN, lozinka, NALOZI, PREGLED, stanje, type Rola } from './podaci';

const BAZA = 'http://localhost:4173';

/** Prijava kroz API; refresh cookie ostaje u kontekstu i čuva se kao stanje te role. */
async function prijava(ime: string, sifra: string): Promise<{ ctx: APIRequestContext; token: string }> {
  const ctx = await request.newContext({ baseURL: BAZA });
  const r = await ctx.post('/api/v1/auth/login', { data: { userName: ime, password: sifra, zapamtiMe: true } });
  expect(r.status(), `prijava ${ime}: ${await r.text()}`).toBe(200);
  return { ctx, token: (await r.json()).accessToken };
}

const sa = (token: string) => ({ Authorization: `Bearer ${token}` });

setup('priprema: nalozi, šabloni, PZ za danas i prijava svake role', async () => {
  mkdirSync(dirname(PREGLED), { recursive: true });
  const admin = await prijava(ADMIN.ime, ADMIN.lozinka());

  for (const n of Object.values(NALOZI)) {
    const r = await admin.ctx.post('/api/v1/korisnici', {
      headers: sa(admin.token),
      data: { korisnickoIme: n.ime, ime: n.puno, email: `${n.ime}@tpm-pregledi.local`, lozinka: lozinka(), rola: n.rola },
    });
    // Nalog već postoji (ponovljeno lokalno pokretanje) — u redu.
    expect([200, 201, 409, 422], `nalog ${n.ime}: ${await r.text()}`).toContain(r.status());
  }

  const xlsx = readFileSync(resolve(import.meta.dirname, '../../docs/sabloni/sabloni-za-uvoz.xlsx'));
  const uvoz = await admin.ctx.post('/api/v1/sabloni/uvoz', {
    headers: sa(admin.token),
    multipart: { fajl: { name: 'sabloni-za-uvoz.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: xlsx } },
  });
  // 422 = šabloni su već uvezeni (ponovljeno lokalno pokretanje).
  expect([200, 422], `uvoz šablona: ${await uvoz.text()}`).toContain(uvoz.status());

  const sup = await prijava(NALOZI.supervizor.ime, lozinka());
  const inzenjeri: { id: string; ime: string }[] = await (await sup.ctx.get('/api/v1/raspored/inzenjeri', { headers: sa(sup.token) })).json();
  const inz = inzenjeri.find((i) => i.ime === NALOZI.inzenjer.puno);
  expect(inz, 'probni inženjer je u rotaciji').toBeDefined();
  const danas = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Belgrade' }).format(new Date());
  const pz = await sup.ctx.post('/api/v1/raspored/pz', { headers: sa(sup.token), data: { datum: danas, tip: null, inzenjerId: inz!.id, napomena: 'E2E' } });
  expect(pz.status(), `zakazivanje PZ-a: ${await pz.text()}`).toBe(200);
  // Drugi PZ (sutra) služi za otkazivanje planiranog pregleda (zamerka 27).
  const sutra = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Belgrade' }).format(new Date(Date.now() + 86_400_000));
  const pz2 = await sup.ctx.post('/api/v1/raspored/pz', { headers: sa(sup.token), data: { datum: sutra, tip: null, inzenjerId: inz!.id, napomena: 'E2E otkazivanje' } });
  expect(pz2.status(), `zakazivanje drugog PZ-a: ${await pz2.text()}`).toBe(200);
  writeFileSync(PREGLED, JSON.stringify({ id: (await pz.json()).id, zaOtkazivanje: (await pz2.json()).id }));

  const sacuvaj = async (rola: Rola, ctx: APIRequestContext) => {
    await ctx.storageState({ path: stanje(rola) });
    await ctx.dispose();
  };
  await sacuvaj('admin', admin.ctx);
  await sacuvaj('supervizor', sup.ctx);
  await sacuvaj('inzenjer', (await prijava(NALOZI.inzenjer.ime, lozinka())).ctx);
  await sacuvaj('direktor', (await prijava(NALOZI.direktor.ime, lozinka())).ctx);
});
