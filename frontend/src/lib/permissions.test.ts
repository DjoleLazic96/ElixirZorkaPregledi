import { describe, it, expect } from 'vitest';
import { ROLA_LABELE, canEditKategorije, canSeeAdminSection, isAdmin, isKorisnik } from './permissions';

describe('lib/permissions', () => {
  it('isAdmin je tačno samo za rolu Admin', () => {
    expect(isAdmin(['Admin'])).toBe(true);
    expect(isAdmin(['Korisnik'])).toBe(false);
    expect(isAdmin([])).toBe(false);
  });

  it('isKorisnik je tačno za Admin ili Korisnik, ne za nepoznatu rolu', () => {
    expect(isKorisnik(['Admin'])).toBe(true);
    expect(isKorisnik(['Korisnik'])).toBe(true);
    expect(isKorisnik(['Nesto'])).toBe(false);
    expect(isKorisnik([])).toBe(false);
  });

  it('canSeeAdminSection i canEditKategorije su strogo Admin', () => {
    expect(canSeeAdminSection(['Admin'])).toBe(true);
    expect(canSeeAdminSection(['Korisnik'])).toBe(false);
    expect(canEditKategorije(['Admin'])).toBe(true);
    expect(canEditKategorije(['Korisnik'])).toBe(false);
  });

  it('labele rola su na srpskom', () => {
    expect(ROLA_LABELE.Admin).toBe('Administrator');
    expect(ROLA_LABELE.Korisnik).toBe('Korisnik');
  });
});
