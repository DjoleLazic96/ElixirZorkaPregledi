import { describe, it, expect } from 'vitest';
import {
  MESECI,
  formatirajPeriod,
  godinaOpcije,
  godinaOpcijeSire,
  mesecOpcije,
  nazivMeseca,
  oduzmiMeseci,
  periodKljuc,
  trenutniPeriod,
} from './period';

describe('lib/period', () => {
  it('nazivMeseca je 1-based; nepoznat broj se vraća kao tekst', () => {
    expect(nazivMeseca(1)).toBe('Januar');
    expect(nazivMeseca(10)).toBe('Oktobar');
    expect(nazivMeseca(13)).toBe('13');
  });

  it('mesecOpcije daje 12 opcija sa vrednostima 1..12', () => {
    const o = mesecOpcije();
    expect(o).toHaveLength(12);
    expect(o[0]).toEqual({ value: 1, label: 'Januar' });
    expect(o[11]).toEqual({ value: 12, label: MESECI[11] });
  });

  it('godinaOpcije i godinaOpcijeSire se centriraju oko tekuće godine', () => {
    const g = new Date().getFullYear();
    expect(godinaOpcije().map((o) => o.value)).toEqual([g - 1, g, g + 1]);
    expect(godinaOpcije(2).map((o) => o.value)).toEqual([g - 2, g - 1, g, g + 1, g + 2]);
    expect(godinaOpcijeSire().map((o) => o.value)).toEqual([g - 3, g - 2, g - 1, g, g + 1]);
  });

  it('periodKljuc dopunjava mesec nulom', () => {
    expect(periodKljuc(2026, 3)).toBe('2026-03');
    expect(periodKljuc(2026, 12)).toBe('2026-12');
  });

  it('trenutniPeriod vraća tekući mesec i godinu', () => {
    const d = new Date();
    expect(trenutniPeriod()).toEqual({ godina: d.getFullYear(), mesec: d.getMonth() + 1 });
  });

  it('oduzmiMeseci radi preko granice godine u oba smera', () => {
    expect(oduzmiMeseci(2026, 1, 1)).toEqual({ godina: 2025, mesec: 12 });
    expect(oduzmiMeseci(2026, 3, 6)).toEqual({ godina: 2025, mesec: 9 });
    expect(oduzmiMeseci(2026, 3, -10)).toEqual({ godina: 2027, mesec: 1 });
    expect(oduzmiMeseci(2026, 12, 0)).toEqual({ godina: 2026, mesec: 12 });
  });

  it('formatirajPeriod daje mesec malim slovom i godinu sa tačkom', () => {
    expect(formatirajPeriod(2026, 10)).toBe('oktobar 2026.');
  });
});
