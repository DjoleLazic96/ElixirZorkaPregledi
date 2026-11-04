import { describe, it, expect } from 'vitest';
import {
  PODRAZUMEVANA_PRAVA,
  PRAVA,
  ROLA_LABELE,
  canEditKategorije,
  canSeeAdminSection,
  imaPravo,
  isAdmin,
  isKorisnik,
} from './permissions';

const p = (roles: string[], prava: string[] = []) => ({ roles, prava });

describe('lib/permissions', () => {
  it('isAdmin je tačno samo za rolu Admin', () => {
    expect(isAdmin(p(['Admin']))).toBe(true);
    expect(isAdmin(p(['Direktor']))).toBe(false);
    expect(isAdmin(p([]))).toBe(false);
  });

  it('isKorisnik je tačno za svaku od četiri role, ne za nepoznatu', () => {
    ['Inzenjer', 'Supervizor', 'Direktor', 'Admin'].forEach((r) => expect(isKorisnik(p([r]))).toBe(true));
    expect(isKorisnik(p(['Korisnik']))).toBe(false);
    expect(isKorisnik(p([]))).toBe(false);
  });

  it('administracija se vidi po pravu, ne po roli', () => {
    expect(canSeeAdminSection(p(['Direktor'], ['podesavanja']))).toBe(true);
    expect(canSeeAdminSection(p(['Admin'], ['analitika']))).toBe(false);
    expect(imaPravo(p(['Supervizor'], ['overa']), 'overa')).toBe(true);
    expect(canEditKategorije(p(['Admin']))).toBe(true);
    expect(canEditKategorije(p(['Inzenjer']))).toBe(false);
  });

  it('popunjavanje je podrazumevano samo kod Inženjera', () => {
    expect(PODRAZUMEVANA_PRAVA.Inzenjer).toContain('pregledi.popunjavanje');
    (['Supervizor', 'Direktor', 'Admin'] as const).forEach((r) =>
      expect(PODRAZUMEVANA_PRAVA[r]).not.toContain('pregledi.popunjavanje'),
    );
  });

  it('sva prava imaju labelu, a labele rola su na srpskom', () => {
    expect(PRAVA).toHaveLength(11);
    expect(ROLA_LABELE.Inzenjer).toBe('Inženjer');
    expect(ROLA_LABELE.Admin).toBe('Administrator');
  });
});
