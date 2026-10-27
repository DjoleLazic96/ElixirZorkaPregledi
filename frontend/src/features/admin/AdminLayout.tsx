import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from '../../components/ui/Tabs';

/**
 * Sekcija „Administracija" — underline tabovi + `<Outlet/>` (blueprint 14 §1).
 * Pristup štiti `Zasticeno` u `AppRoutes`. Dijagnostika (audit log, stanje
 * servera) NIJE ovde — to su zasebne stavke levog menija.
 *
 * Nova admin podstrana = red u `TABOVI` + ruta pod `/admin` u `AppRoutes`.
 */
const TABOVI = [{ key: 'korisnici', label: 'Korisnici' }] as const;

type TabKljuc = (typeof TABOVI)[number]['key'];

export function AdminLayout() {
  const location = useLocation();
  const navigate = useNavigate();

  // Aktivan tab = drugi segment putanje (/admin/<tab>); nepoznat → prvi tab.
  const segment = location.pathname.split('/')[2];
  const aktivan: TabKljuc = TABOVI.some((t) => t.key === segment) ? (segment as TabKljuc) : TABOVI[0].key;

  return (
    <div className="space-y-4">
      <Tabs items={TABOVI} value={aktivan} onChange={(k) => navigate(`/admin/${k}`)} aria-label="Administracija" />
      <Outlet />
    </div>
  );
}
