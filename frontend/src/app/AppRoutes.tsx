import { Navigate, Route, Routes } from 'react-router-dom';
import { NijePronadjeno } from '../components/NijePronadjeno';
import { Ucitavanje } from '../components/ui/Spinner';
import { Zasticeno } from '../components/Zasticeno';
import { AdminLayout } from '../features/admin/AdminLayout';
import { KorisniciPage } from '../features/admin/korisnici/KorisniciPage';
import { AuditLogPage } from '../features/audit-log/AuditLogPage';
import { useAuth } from '../features/auth/AuthProvider';
import { LoginPage } from '../features/auth/LoginPage';
import { KategorijePage } from '../features/kategorije/KategorijePage';
import { StanjeServeraPage } from '../features/stanje-servera/StanjeServeraPage';
import { isAdmin, isKorisnik } from '../lib/permissions';
import { AppLayout } from './layout/AppLayout';

/**
 * Mapa ruta (blueprint 13 §4).
 *   - inicijalizacija → ceo ekran „Učitavanje…" (proverava se postojeća sesija)
 *   - neulogovan → samo /login, sve ostalo preusmerava tamo
 *   - ulogovan → landing je /kategorije za sve role; svaka ruta ima `Zasticeno`
 *     guard, a nepoznata adresa daje 404 stranicu (ne tihi redirect)
 */
export function AppRoutes() {
  const { me, initializing } = useAuth();

  if (initializing) return <Ucitavanje fullScreen />;

  if (!me) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    );
  }

  const roles = me.roles;
  const landing = '/kategorije';

  return (
    <Routes>
      <Route path="/login" element={<Navigate to={landing} replace />} />

      <Route element={<AppLayout />}>
        <Route path="/" element={<Navigate to={landing} replace />} />

        <Route
          path="/kategorije"
          element={
            <Zasticeno allowed={isKorisnik(roles)} landing={landing}>
              <KategorijePage />
            </Zasticeno>
          }
        />

        <Route
          path="/admin"
          element={
            <Zasticeno allowed={isAdmin(roles)} landing={landing}>
              <AdminLayout />
            </Zasticeno>
          }
        >
          <Route index element={<Navigate to="korisnici" replace />} />
          <Route path="korisnici" element={<KorisniciPage />} />
        </Route>

        {/* Dijagnostika je u levom meniju, van AdminLayout tabova. */}
        <Route
          path="/audit-log"
          element={
            <Zasticeno allowed={isAdmin(roles)} landing={landing}>
              <AuditLogPage />
            </Zasticeno>
          }
        />
        <Route
          path="/stanje-servera"
          element={
            <Zasticeno allowed={isAdmin(roles)} landing={landing}>
              <StanjeServeraPage />
            </Zasticeno>
          }
        />

        <Route path="*" element={<NijePronadjeno />} />
      </Route>
    </Routes>
  );
}
