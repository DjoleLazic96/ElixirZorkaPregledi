import { describe, it, expect } from 'vitest';
import {
  LOKAL,
  danasYmd,
  formatirajDatum,
  formatirajDatumDug,
  formatirajVreme,
  formatirajVremeSek,
  formatirajVremeUtc,
  formatirajYmd,
  krajDanaUtc,
  parsirajYmd,
  pocetakDanaUtc,
  relativnoVreme,
} from './datum';

/** Lokalni trenutak 12.10.2026 09:30:41 (ponedeljak) kao ISO sa Z — nezavisno od zone mašine. */
const LOKALNI_TRENUTAK = new Date(2026, 9, 12, 9, 30, 41).toISOString();

describe('lib/datum', () => {
  it('LOKAL je uvek sa regionom', () => {
    expect(LOKAL).toBe('sr-Latn-RS');
  });

  it('parsirajYmd prihvata samo ispravan YYYY-MM-DD i odbija nepostojeće datume', () => {
    const d = parsirajYmd('2026-10-12')!;
    expect([d.getFullYear(), d.getMonth(), d.getDate()]).toEqual([2026, 9, 12]);
    expect(parsirajYmd('')).toBeNull();
    expect(parsirajYmd('12.10.2026')).toBeNull();
    expect(parsirajYmd('2026-02-30')).toBeNull();
  });

  it('formatirajYmd i danasYmd daju lokalni dan bez UTC pomaka', () => {
    expect(formatirajYmd(new Date(2026, 0, 5))).toBe('2026-01-05');
    const sada = new Date();
    expect(danasYmd()).toBe(formatirajYmd(sada));
  });

  it('formatirajDatum daje dd.MM.yyyy, i za čist dan i za ISO trenutak', () => {
    expect(formatirajDatum('2026-10-12')).toBe('12.10.2026');
    expect(formatirajDatum(LOKALNI_TRENUTAK)).toBe('12.10.2026');
    expect(formatirajDatum('nije datum')).toBe('');
  });

  it('formatirajDatumDug daje dan i mesec malim slovom i godinu sa tačkom', () => {
    expect(formatirajDatumDug('2026-10-12')).toBe('ponedeljak, 12. oktobar 2026.');
  });

  it('formatirajVreme i formatirajVremeSek prikazuju lokalno vreme', () => {
    expect(formatirajVreme(LOKALNI_TRENUTAK)).toBe('12.10.2026 09:30');
    expect(formatirajVremeSek(LOKALNI_TRENUTAK)).toBe('12.10.2026 09:30:41');
    expect(formatirajVreme('x')).toBe('');
  });

  it('formatirajVremeUtc prikazuje UTC vrednost sa oznakom', () => {
    expect(formatirajVremeUtc('2026-10-12T07:30:00Z')).toBe('12.10.2026 07:30 (UTC)');
  });

  it('relativnoVreme bira oblik po starosti', () => {
    const sada = new Date(2026, 9, 12, 12, 0, 0);
    const pre = (ms: number) => new Date(sada.getTime() - ms).toISOString();
    expect(relativnoVreme(pre(30_000), sada)).toBe('upravo');
    expect(relativnoVreme(pre(5 * 60_000), sada)).toBe('pre 5 min');
    expect(relativnoVreme(pre(3 * 3_600_000), sada)).toBe('pre 3 h');
    expect(relativnoVreme(new Date(2026, 9, 11, 23, 0).toISOString(), sada)).toBe('juče');
    expect(relativnoVreme(new Date(2026, 9, 9, 8, 0).toISOString(), sada)).toBe('pre 3 dana');
    expect(relativnoVreme(new Date(2026, 8, 21, 8, 0).toISOString(), sada)).toBe('pre 21 dan');
    expect(relativnoVreme(new Date(2026, 7, 1, 8, 0).toISOString(), sada)).toBe('01.08.2026');
  });

  it('pocetakDanaUtc i krajDanaUtc daju granice LOKALNOG dana kao UTC ISO sa Z', () => {
    const od = pocetakDanaUtc('2026-10-12');
    const doo = krajDanaUtc('2026-10-12');
    expect(od).toMatch(/Z$/);
    expect(doo).toMatch(/Z$/);
    const odLokalno = new Date(od);
    expect([odLokalno.getHours(), odLokalno.getMinutes(), odLokalno.getDate()]).toEqual([0, 0, 12]);
    const doLokalno = new Date(doo);
    expect([doLokalno.getHours(), doLokalno.getMinutes(), doLokalno.getSeconds()]).toEqual([23, 59, 59]);
    expect(doLokalno.getMilliseconds()).toBe(999);
  });
});
