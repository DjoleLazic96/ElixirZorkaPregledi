import { describe, it, expect } from 'vitest';
import type { PopunjavanjeDto } from '../features/popunjavanje/hooks';
import { nedostaje, primeniLokalno } from './lokalno';

const P: PopunjavanjeDto = {
  id: 1, broj: 'DP1-2026-0001', tip: 'DP1', status: 'planiran', planiraniDatum: '2026-11-09', zapocetAt: null, verzijaId: 3,
  sekcije: [{
    sifra: '10', moja: true, radi: 'Ana',
    uredjaji: [
      { uredjajId: 1, naziv: 'Elevator', oznaka: '10-C-13', vanFunkcije: false, prosliPutVanFunkcije: false, otvoreneAkcije: [],
        stavke: [{ stavkaId: 11, aktivnost: 'A', odgovorIspravan: 'Ispravno', odgovorNeispravan: 'Neispravno', polja: [], odgovor: null, fotografije: [], aktivnostId: 1, uputstvo: null, uputstvoSlika: 0 }] },
      { uredjajId: 2, naziv: 'Puž', oznaka: '30-F-01 M2', vanFunkcije: false, prosliPutVanFunkcije: false, otvoreneAkcije: [],
        stavke: [{ stavkaId: 12, aktivnost: 'B', odgovorIspravan: 'Ispravno', odgovorNeispravan: 'Neispravno', polja: [], odgovor: null, fotografije: [], aktivnostId: 1, uputstvo: null, uputstvoSlika: 0 }] },
    ],
  }],
};

describe('offline/lokalno — primena operacija bez interneta', () => {
  it('početak, odgovor, van funkcije, sve ispravno i predaja menjaju lokalnu kopiju, a original ostaje', () => {
    let p = primeniLokalno(P, { vrsta: 'zapocni', podaci: { verzijaId: 3 } }, '2026-11-09T07:00:00Z');
    expect(p.status).toBe('u_radu');
    expect(P.status).toBe('planiran');

    p = primeniLokalno(p, { vrsta: 'odgovor', podaci: { stavkaId: 11, vrednost: 1, napomena: null } }, 'x');
    expect(nedostaje(p)).toEqual({ odgovora: 1, napomena: 1, polja: [] });

    p = primeniLokalno(p, { vrsta: 'van_funkcije', podaci: { uredjajId: 2, vanFunkcije: true } }, 'x');
    p = primeniLokalno(p, { vrsta: 'odgovor', podaci: { stavkaId: 11, vrednost: 1, napomena: '2 karike' } }, 'x');
    expect(nedostaje(p)).toEqual({ odgovora: 0, napomena: 0, polja: [] });

    p = primeniLokalno(p, { vrsta: 'van_funkcije', podaci: { uredjajId: 2, vanFunkcije: false } }, 'x');
    p = primeniLokalno(p, { vrsta: 'sve_ispravno', podaci: { sekcija: '10' } }, 'x');
    expect(p.sekcije[0].uredjaji[1].stavke[0].odgovor?.vrednost).toBe(0);
    expect(p.sekcije[0].uredjaji[0].stavke[0].odgovor?.vrednost).toBe(1, );

    expect(primeniLokalno(p, { vrsta: 'predaja', podaci: {} }, 'x').status).toBe('predato_ceka');
  });

  it('broji i obavezna dodatna polja koja se vide uz odgovor (zamerka 13)', () => {
    const p = structuredClone(P);
    const st = p.sekcije[0].uredjaji[0].stavke[0];
    st.polja = [{ id: 5, naziv: 'Temperatura', tip: 'broj', jedinica: '°C', oznake: [], prikaz: 'uz_neispravan', obavezno: true }];
    st.odgovor = { vrednost: 0, napomena: null, polja: {} };
    expect(nedostaje(p).polja).toEqual([]);
    st.odgovor = { vrednost: 1, napomena: 'vruće', polja: {} };
    expect(nedostaje(p).polja).toHaveLength(1);
    st.odgovor = { vrednost: 1, napomena: 'vruće', polja: { '5': 82 } };
    expect(nedostaje(p).polja).toEqual([]);
  });
});
