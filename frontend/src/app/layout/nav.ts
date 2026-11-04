import { Activity, BarChart3, CalendarDays, LayoutDashboard, FileCheck, ListChecks, ShieldAlert, CalendarOff, CalendarRange, CalendarX, ClipboardList, ScrollText, Settings, Wrench, type LucideIcon } from 'lucide-react';
import { canSeeAdminSection, imaPravo, isAdmin, type Pristup } from '../../lib/permissions';

export interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Ako je undefined, stavka je vidljiva svim ulogovanim korisnicima. */
  visibleIf?: (p: Pristup) => boolean;
}

export const NAV: NavItem[] = [
  { to: '/', label: 'Početna', icon: LayoutDashboard },
  { to: '/kalendar', label: 'Kalendar', icon: CalendarDays },
  { to: '/pregledi', label: 'Pregledi', icon: FileCheck },
  { to: '/akcije', label: 'Akcije', icon: ListChecks },
  { to: '/neispravnosti', label: 'Neispravnosti', icon: ShieldAlert },
  {
    to: '/raspored', label: 'Raspored', icon: CalendarRange,
    visibleIf: (p) => imaPravo(p, 'raspored.uredjivanje') || imaPravo(p, 'odsustva.odobravanje'),
  },
  { to: '/moja-odsustva', label: 'Moja odsustva', icon: CalendarOff, visibleIf: (p) => imaPravo(p, 'pregledi.popunjavanje') },
  { to: '/analitika', label: 'Analitika', icon: BarChart3, visibleIf: (p) => imaPravo(p, 'analitika') },
  { to: '/sabloni', label: 'Šabloni', icon: ClipboardList },
  { to: '/uredjaji', label: 'Uređaji', icon: Wrench },
  { to: '/neradni-dani', label: 'Neradni dani', icon: CalendarX, visibleIf: (p) => imaPravo(p, 'neradni_dani') },
  { to: '/admin', label: 'Administracija', icon: Settings, visibleIf: canSeeAdminSection },
  // Dijagnostika — zasebne stavke, ne pod Administracijom: nisu šifarnici i
  // otvaraju se povremeno, pa im je mesto u glavnom meniju, ne u pod-tabovima.
  { to: '/audit-log', label: 'Audit log', icon: ScrollText, visibleIf: isAdmin },
  { to: '/stanje-servera', label: 'Stanje servera', icon: Activity, visibleIf: isAdmin },
];

/** Stavke menija koje korisnik vidi (isti filter za bočni i donji meni). */
export function vidljiveStavke(p: Pristup): NavItem[] {
  return NAV.filter((i) => i.visibleIf === undefined || i.visibleIf(p));
}
