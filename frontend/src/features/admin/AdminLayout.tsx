import { useAuth } from '../auth/AuthProvider';
import { imaPravo } from '../../lib/permissions';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from '../../components/ui/Tabs';

/**
 * Sekcija „Administracija" — underline tabovi + `<Outlet/>` (blueprint 14 §1).
 * Pristup štiti `Zasticeno` u `AppRoutes`. Dijagnostika (audit log, stanje
 * servera) NIJE ovde — to su zasebne stavke levog menija.
 *
 * Nova admin podstrana = red u `TABOVI` + ruta pod `/admin` u `AppRoutes`.
 */
const SVI_TABOVI = [
  { key: 'korisnici', label: 'Korisnici', pravo: 'korisnici' },
  { key: 'podesavanja', label: 'Podešavanja', pravo: 'podesavanja' },
] as const;

type TabKljuc = (typeof SVI_TABOVI)[number]['key'];

export function AdminLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  const { me } = useAuth();
  // Tab se vidi samo uz pravo; ruta je dodatno zaštićena u AppRoutes.
  const TABOVI = SVI_TABOVI.filter((t) => me && imaPravo(me, t.pravo));

  // Aktivan tab = drugi segment putanje (/admin/<tab>); nepoznat → prvi tab.
  const segment = location.pathname.split('/')[2];
  const aktivan: TabKljuc = TABOVI.some((t) => t.key === segment) ? (segment as TabKljuc) : (TABOVI[0]?.key ?? 'korisnici');

  return (
    <div className="space-y-4">
      <Tabs items={TABOVI} value={aktivan} onChange={(k) => navigate(`/admin/${k}`)} aria-label="Administracija" />
      <Outlet />
    </div>
  );
}
