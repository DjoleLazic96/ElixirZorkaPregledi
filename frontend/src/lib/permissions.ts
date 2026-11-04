/**
 * Čiste funkcije za prava na frontendu. Backend je autoritativan (vraća 403
 * ako pravo fali) — ovde samo skrivamo navigaciju i dugmad radi čistog UX-a.
 *
 * Vrednosti moraju da odgovaraju backend konstantama (`Domain/Identity/Roles.cs`, `Prava.cs`).
 * Rola daje samo podrazumevani skup prava; šta se sme gleda se po pravu (odluka 2026-11-04).
 * Imenovanje: `can<Akcija>` za akcije, `canSee<Sekcija>` za navigaciju.
 */
export type Rola = 'Inzenjer' | 'Supervizor' | 'Direktor' | 'Admin';

/** Labele rola za prikaz (jedino mesto gde se rola prevodi u tekst). */
export const ROLA_LABELE: Record<Rola, string> = {
  Inzenjer: 'Inženjer',
  Supervizor: 'Supervizor',
  Direktor: 'Direktor',
  Admin: 'Administrator',
};

export const ROLE: readonly Rola[] = ['Inzenjer', 'Supervizor', 'Direktor', 'Admin'];

export type Pravo =
  | 'pregledi.popunjavanje'
  | 'analitika'
  | 'overa'
  | 'raspored.uredjivanje'
  | 'odsustva.odobravanje'
  | 'akcije.zatvaranje'
  | 'sabloni.nacrt'
  | 'sabloni.objava'
  | 'neradni_dani'
  | 'korisnici'
  | 'podesavanja';

/** Sva prava, redom kao u matrici (`docs/04`) i u dijalogu „Prava“. */
export const PRAVA: ReadonlyArray<{ value: Pravo; label: string }> = [
  { value: 'pregledi.popunjavanje', label: 'Popunjavanje pregleda' },
  { value: 'analitika', label: 'Analitika' },
  { value: 'overa', label: 'Overa, dorada i poništavanje pregleda' },
  { value: 'raspored.uredjivanje', label: 'Raspored i PZ' },
  { value: 'odsustva.odobravanje', label: 'Odsustva' },
  { value: 'akcije.zatvaranje', label: 'Zatvaranje i dodela akcija' },
  { value: 'sabloni.nacrt', label: 'Registar opreme i nacrt šablona' },
  { value: 'sabloni.objava', label: 'Objava šablona' },
  { value: 'neradni_dani', label: 'Neradni dani' },
  { value: 'korisnici', label: 'Korisnici i prava' },
  { value: 'podesavanja', label: 'Podešavanja' },
];

/** Ogledalo `Prava.Podrazumevana` (R1.5) — za dugme „Vrati podrazumevana“. */
export const PODRAZUMEVANA_PRAVA: Record<Rola, readonly Pravo[]> = {
  Inzenjer: ['pregledi.popunjavanje', 'analitika'],
  Supervizor: ['analitika', 'overa', 'raspored.uredjivanje', 'odsustva.odobravanje', 'akcije.zatvaranje'],
  Direktor: [
    'analitika', 'overa', 'raspored.uredjivanje', 'odsustva.odobravanje', 'akcije.zatvaranje',
    'sabloni.nacrt', 'sabloni.objava', 'neradni_dani',
  ],
  Admin: ['analitika', 'sabloni.nacrt', 'neradni_dani', 'korisnici', 'podesavanja'],
};

/** Ko je prijavljen: role i prava iz `/auth/me` (ili odgovora prijave). */
export interface Pristup {
  roles: readonly string[];
  prava: readonly string[];
}

export function imaPravo(p: Pristup, pravo: Pravo): boolean {
  return p.prava.includes(pravo);
}

/** Sistemski ekrani skeleta (audit log, stanje servera) — rola Admin. */
export function isAdmin(p: Pristup): boolean {
  return p.roles.includes('Admin');
}

/** Bilo koji ulogovani korisnik sa jednom od četiri role (backend `RequireKorisnik`). */
export function isKorisnik(p: Pristup): boolean {
  return ROLE.some((r) => p.roles.includes(r));
}

/** Sekcija „Administracija“ — vidi je ko ima bar jedno administrativno pravo. */
export function canSeeAdminSection(p: Pristup): boolean {
  return imaPravo(p, 'korisnici') || imaPravo(p, 'podesavanja');
}

/** Kreiranje/izmena/deaktivacija kategorija (primer feature-a, uklanja se u fazi 02) — Admin. */
export function canEditKategorije(p: Pristup): boolean {
  return isAdmin(p);
}
