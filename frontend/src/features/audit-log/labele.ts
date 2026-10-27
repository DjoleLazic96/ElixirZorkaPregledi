import type { BadgeProps } from '../../components/ui/Badge';
import type { AuditAkcija } from '../../api/types';

/**
 * Labele i boje akcija audit loga (blueprint 08 §5). Entiteti dolaze sa
 * backenda (`/audit/entiteti`, `entitetLabela`), pa ih ovde nema.
 */
export const AKCIJE: ReadonlyArray<{
  value: AuditAkcija;
  label: string;
  tone: NonNullable<BadgeProps['tone']>;
}> = [
  { value: 'CREATE', label: 'Kreiranje', tone: 'primary' },
  { value: 'UPDATE', label: 'Izmena', tone: 'amber' },
  { value: 'DELETE', label: 'Brisanje', tone: 'red' },
  { value: 'LOGIN', label: 'Prijava', tone: 'gray' },
  { value: 'LOGIN_FAIL', label: 'Neuspešna prijava', tone: 'red' },
  { value: 'LOGOUT', label: 'Odjava', tone: 'gray' },
  { value: 'START', label: 'Pokretanje', tone: 'gray' },
];

/** Nepoznat kod (noviji backend) se prikazuje sirov umesto da obori prikaz. */
export function akcijaLabela(akcija: AuditAkcija): string {
  return AKCIJE.find((a) => a.value === akcija)?.label ?? akcija;
}

export function akcijaTon(akcija: AuditAkcija): NonNullable<BadgeProps['tone']> {
  return AKCIJE.find((a) => a.value === akcija)?.tone ?? 'gray';
}

/**
 * Labele polja u diff prikazu za najčešća imena (interceptor serijalizuje
 * CLR imena, PascalCase). Ključevi se upoređuju sa malim početnim slovom;
 * nepoznato polje se prikazuje sirovo.
 */
const POLJA: Record<string, string> = {
  sifra: 'Šifra',
  naziv: 'Naziv',
  opis: 'Opis',
  aktivan: 'Aktivan',
  ime: 'Ime',
  email: 'E-mail',
  userName: 'Korisničko ime',
  fullName: 'Ime i prezime',
  korisnickoIme: 'Korisničko ime',
  rola: 'Rola',
  lastLoginAt: 'Poslednja prijava',
  poslednjaPrijava: 'Poslednja prijava',
  verzija: 'Verzija',
  gitSha: 'Git SHA',
  okruzenje: 'Okruženje',
  createdAt: 'Kreirano',
  createdBy: 'Kreirao',
  updatedAt: 'Izmenjeno',
  updatedBy: 'Izmenio',
};

export function poljeLabela(polje: string): string {
  const kljuc = polje.charAt(0).toLowerCase() + polje.slice(1);
  return POLJA[kljuc] ?? polje;
}
