/**
 * Čiste funkcije za prava na frontendu. Backend je autoritativan (vraća 403
 * ako rola fali) — ovde samo skrivamo navigaciju i dugmad radi čistog UX-a.
 *
 * Vrednosti moraju da odgovaraju backend konstantama (`Domain/Identity/Roles.cs`).
 * Imenovanje: `can<Akcija>` za akcije, `canSee<Sekcija>` za navigaciju.
 */
export type Rola = 'Admin' | 'Korisnik';

/** Labele rola za prikaz (jedino mesto gde se rola prevodi u tekst). */
export const ROLA_LABELE: Record<Rola, string> = {
  Admin: 'Administrator',
  Korisnik: 'Korisnik',
};

/** Pun pristup — administracija, audit log, stanje servera, sve izmene. */
export function isAdmin(roles: readonly string[]): boolean {
  return roles.includes('Admin');
}

/**
 * Bilo koji ulogovani korisnik aplikacije (Admin ima nadskup prava).
 * Ekvivalent backend politike `RequireKorisnik`.
 */
export function isKorisnik(roles: readonly string[]): boolean {
  return roles.includes('Admin') || roles.includes('Korisnik');
}

/** Sekcija „Administracija" (korisnici) — strogo Admin. */
export function canSeeAdminSection(roles: readonly string[]): boolean {
  return isAdmin(roles);
}

/** Kreiranje/izmena/deaktivacija kategorija (primer feature-a) — strogo Admin. */
export function canEditKategorije(roles: readonly string[]): boolean {
  return isAdmin(roles);
}
