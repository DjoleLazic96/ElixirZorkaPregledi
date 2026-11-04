/**
 * Adresa na koju se vraća posle prijave: ruta koju je neprijavljen korisnik otvorio (npr. `/qr/…` sa nalepnice), prosleđena kroz
 * `state.od`. Samo unutrašnje rute — spoljna adresa ili `//host` bi bili otvoreno preusmeravanje.
 */
export function adresaPovratka(state: unknown): string {
  const od = (state as { od?: unknown } | null)?.od;
  return typeof od === 'string' && od.startsWith('/') && !od.startsWith('//') && !od.startsWith('/login') ? od : '/';
}
