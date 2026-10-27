import { describe, it, expect } from 'vitest';
import { greskePoPolju, porukaGreske } from './apiError';
import { apiGreska } from '../test/utils';

describe('lib/apiError — porukaGreske', () => {
  it('spaja greske[] (422) razmakom', () => {
    const err = apiGreska(422, { greske: ['Mesec je zaključen.', 'Zbir mora biti 100.'] });
    expect(porukaGreske(err, 'fallback')).toBe('Mesec je zaključen. Zbir mora biti 100.');
  });

  it('spaja sve poruke iz errors mape (400)', () => {
    const err = apiGreska(400, { errors: { Sifra: ['Šifra je obavezna.'], Naziv: ['Naziv je obavezan.'] } });
    expect(porukaGreske(err, 'fallback')).toBe('Šifra je obavezna. Naziv je obavezan.');
  });

  it('greske ima prednost nad errors, errors nad detail, detail nad title', () => {
    expect(
      porukaGreske(apiGreska(422, { greske: ['A.'], errors: { x: ['B.'] }, detail: 'C', title: 'D' }), 'E'),
    ).toBe('A.');
    expect(porukaGreske(apiGreska(400, { errors: { x: ['B.'] }, detail: 'C', title: 'D' }), 'E')).toBe('B.');
    expect(porukaGreske(apiGreska(404, { detail: 'C', title: 'D' }), 'E')).toBe('C');
    expect(porukaGreske(apiGreska(404, { title: 'D' }), 'E')).toBe('D');
  });

  it('vraća fallback kad odgovor nema telo (mreža, timeout)', () => {
    expect(porukaGreske(new Error('Network Error'), 'Prijava nije uspela.')).toBe('Prijava nije uspela.');
    expect(porukaGreske(undefined, 'fallback')).toBe('fallback');
  });

  it('za 500 vraća detail kakav jeste — kod za podršku je već u njemu', () => {
    const detail = 'Došlo je do neočekivane greške. Navedite ovaj kod podršci: 4bf92f3577b34da6a3ce929d0e0e4736';
    const err = apiGreska(500, { detail, traceId: '4bf92f3577b34da6a3ce929d0e0e4736' });
    expect(porukaGreske(err, 'fallback')).toBe(detail);
  });
});

describe('lib/apiError — greskePoPolju', () => {
  it('mapira errors na camelCase ključeve i spaja poruke po polju', () => {
    const err = apiGreska(400, {
      errors: { Sifra: ['Šifra je obavezna.', 'Šifra je predugačka.'], naziv: ['Naziv je obavezan.'] },
    });
    expect(greskePoPolju(err)).toEqual({
      sifra: 'Šifra je obavezna. Šifra je predugačka.',
      naziv: 'Naziv je obavezan.',
    });
  });

  it('vraća prazan objekat kad nema errors mape', () => {
    expect(greskePoPolju(apiGreska(422, { greske: ['X.'] }))).toEqual({});
    expect(greskePoPolju(new Error('x'))).toEqual({});
  });
});
