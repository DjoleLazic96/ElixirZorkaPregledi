import { Navigate, Route, Routes } from 'react-router-dom';
import { NijePronadjeno } from '../components/NijePronadjeno';
import { Ucitavanje } from '../components/ui/Spinner';
import { Zasticeno } from '../components/Zasticeno';
import { AdminLayout } from '../features/admin/AdminLayout';
import { PodesavanjaPage } from '../features/admin/podesavanja/PodesavanjaPage';
import { KorisniciPage } from '../features/admin/korisnici/KorisniciPage';
import { AuditLogPage } from '../features/audit-log/AuditLogPage';
import { useAuth } from '../features/auth/AuthProvider';
import { LoginPage } from '../features/auth/LoginPage';
import { SabloniPage } from '../features/sabloni/SabloniPage';
import { UredjajiPage } from '../features/sabloni/UredjajiPage';
import { StanjeServeraPage } from '../features/stanje-servera/StanjeServeraPage';
import { canSeeAdminSection, imaPravo, isAdmin, isKorisnik } from '../lib/permissions';
import { AppLayout } from './layout/AppLayout';

/**
 * Mapa ruta (blueprint 13 §4).
 *   - inicijalizacija → ceo ekran „Učitavanje…" (proverava se postojeća sesija)
 *   - neulogovan → samo /login, sve ostalo preusmerava tamo
 *   - ulogovan → landing je /sabloni za sve role (do Početne iz faze 08); svaka ruta ima `Zasticeno`
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

  const landing = '/sabloni';

  return (
    <Routes>
      <Route path="/login" element={<Navigate to={landing} replace />} />

      <Route element={<AppLayout />}>
        <Route path="/" element={<Navigate to={landing} replace />} />

        <Route
          path="/sabloni"
          element={
            <Zasticeno allowed={isKorisnik(me)} landing={landing}>
              <SabloniPage />
            </Zasticeno>
          }
        />
        <Route
          path="/uredjaji"
          element={
            <Zasticeno allowed={isKorisnik(me)} landing={landing}>
              <UredjajiPage />
            </Zasticeno>
          }
        />

        <Route
          path="/admin"
          element={
            <Zasticeno allowed={canSeeAdminSection(me)} landing={landing}>
              <AdminLayout />
            </Zasticeno>
          }
        >
          <Route index element={<Navigate to={imaPravo(me, 'korisnici') ? 'korisnici' : 'podesavanja'} replace />} />
          <Route
            path="korisnici"
            element={
              <Zasticeno allowed={imaPravo(me, 'korisnici')} landing={landing}>
                <KorisniciPage />
              </Zasticeno>
            }
          />
          <Route
            path="podesavanja"
            element={
              <Zasticeno allowed={imaPravo(me, 'podesavanja')} landing={landing}>
                <PodesavanjaPage />
              </Zasticeno>
            }
          />
        </Route>

        {/* Dijagnostika je u levom meniju, van AdminLayout tabova. */}
        <Route
          path="/audit-log"
          element={
            <Zasticeno allowed={isAdmin(me)} landing={landing}>
              <AuditLogPage />
            </Zasticeno>
          }
        />
        <Route
          path="/stanje-servera"
          element={
            <Zasticeno allowed={isAdmin(me)} landing={landing}>
              <StanjeServeraPage />
            </Zasticeno>
          }
        />

        <Route path="*" element={<NijePronadjeno />} />
      </Route>
    </Routes>
  );
}
