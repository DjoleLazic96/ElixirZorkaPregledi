import { canSeeAdminSection, imaPravo, isAdmin, type Pristup } from '../../lib/permissions';

export interface NavItem {
  to: string;
  label: string;
  /** Ikonica kao u mockup-u (emoji, isti na računaru i telefonu). */
  ikona: string;
  /** Ako je undefined, stavka je vidljiva svim ulogovanim korisnicima. */
  visibleIf?: (p: Pristup) => boolean;
}

/** Redosled i ikonice prate mockup (`docs/mockup/index.html`, `NAV`); donji meni na telefonu uzima prve četiri vidljive. */
export const NAV: NavItem[] = [
  { to: '/', label: 'Početna', ikona: '🏠' },
  { to: '/pregledi', label: 'Pregledi', ikona: '📋' },
  {
    to: '/raspored', label: 'Raspored', ikona: '🗓️',
    visibleIf: (p) => imaPravo(p, 'raspored.uredjivanje') || imaPravo(p, 'odsustva.odobravanje'),
  },
  { to: '/kalendar', label: 'Kalendar', ikona: '📅' },
  { to: '/akcije', label: 'Akcije', ikona: '🛠️' },
  { to: '/neispravnosti', label: 'Neispravnosti', ikona: '⚠️' },
  { to: '/uredjaji', label: 'Uređaji', ikona: '⚙️' },
  { to: '/analitika', label: 'Analitika', ikona: '📊', visibleIf: (p) => imaPravo(p, 'analitika') },
  { to: '/sabloni', label: 'Šabloni', ikona: '🧩' },
  { to: '/admin', label: 'Administracija', ikona: '👥', visibleIf: canSeeAdminSection },
  { to: '/neradni-dani', label: 'Neradni dani', ikona: '🏖️', visibleIf: (p) => imaPravo(p, 'neradni_dani') },
  { to: '/moja-odsustva', label: 'Moja odsustva', ikona: '🌴', visibleIf: (p) => imaPravo(p, 'pregledi.popunjavanje') },
  // Dijagnostika — zasebne stavke, ne pod Administracijom: nisu šifarnici i
  // otvaraju se povremeno, pa im je mesto u glavnom meniju, ne u pod-tabovima.
  { to: '/audit-log', label: 'Audit log', ikona: '📜', visibleIf: isAdmin },
  { to: '/stanje-servera', label: 'Stanje servera', ikona: '🖥️', visibleIf: isAdmin },
];

/** Stavke menija koje korisnik vidi (isti filter za bočni i donji meni). */
export function vidljiveStavke(p: Pristup): NavItem[] {
  return NAV.filter((i) => i.visibleIf === undefined || i.visibleIf(p));
}
