import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { adresaPovratka } from '../features/auth/povratak';
import { NijePronadjeno } from '../components/NijePronadjeno';
import { Ucitavanje } from '../components/ui/Spinner';
import { Zasticeno } from '../components/Zasticeno';
import { AdminLayout } from '../features/admin/AdminLayout';
import { PodesavanjaPage } from '../features/admin/podesavanja/PodesavanjaPage';
import { KorisniciPage } from '../features/admin/korisnici/KorisniciPage';
import { AuditLogPage } from '../features/audit-log/AuditLogPage';
import { useAuth } from '../features/auth/AuthProvider';
import { LoginPage } from '../features/auth/LoginPage';
import { AnalitikaPage } from '../features/analitika/AnalitikaPage';
import { DashboardPage } from '../features/analitika/DashboardPage';
import { QrPage } from '../features/analitika/Pretraga';
import { UredjajEkranPage } from '../features/analitika/UredjajEkranPage';
import { PopunjavanjePage } from '../features/popunjavanje/PopunjavanjePage';
import { AkcijaPage, AkcijePage, KvarPage, NeispravnostiPage } from '../features/akcije/AkcijePages';
import { PregledDetaljPage } from '../features/pregledi/PregledDetaljPage';
import { PreglediPage } from '../features/pregledi/PreglediPage';
import { KalendarPage } from '../features/raspored/KalendarPage';
import { MojaOdsustvaPage, NeradniDaniPage } from '../features/raspored/OdsustvaINeradniPages';
import { RasporedPage } from '../features/raspored/RasporedPage';
import { NacrtPage } from '../features/sabloni/IzmenePages';
import { SabloniPage } from '../features/sabloni/SabloniPage';
import { UredjajiPage } from '../features/sabloni/UredjajiPage';
import { StanjeServeraPage } from '../features/stanje-servera/StanjeServeraPage';
import { canEditOprema, canSeeAdminSection, imaPravo, isAdmin, isKorisnik } from '../lib/permissions';
import { AppLayout } from './layout/AppLayout';

/**
 * Mapa ruta (blueprint 13 §4).
 *   - inicijalizacija → ceo ekran „Učitavanje…" (proverava se postojeća sesija)
 *   - neulogovan → samo /login, sve ostalo preusmerava tamo (adresa se pamti i posle prijave otvara)
 *   - ulogovan → landing je Početna (/) za sve role; svaka ruta ima `Zasticeno`
 *     guard, a nepoznata adresa daje 404 stranicu (ne tihi redirect)
 */
/** Neprijavljen korisnik ide na prijavu, a adresa koju je otvorio se pamti za povratak. */
function NaPrijavu() {
  const l = useLocation();
  return <Navigate to="/login" replace state={{ od: l.pathname + l.search }} />;
}

/** Prijavljen korisnik na /login ide tamo odakle je došao, inače na Početnu. */
function SaPrijave() {
  return <Navigate to={adresaPovratka(useLocation().state)} replace />;
}

export function AppRoutes() {
  const { me, initializing } = useAuth();

  if (initializing) return <Ucitavanje fullScreen />;

  if (!me) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<NaPrijavu />} />
      </Routes>
    );
  }

  const landing = '/';

  return (
    <Routes>
      <Route path="/login" element={<SaPrijave />} />

      <Route element={<AppLayout />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/uredjaji/:id" element={<Zasticeno allowed={isKorisnik(me)} landing={landing}><UredjajEkranPage /></Zasticeno>} />
        <Route path="/qr/:token" element={<Zasticeno allowed={isKorisnik(me)} landing={landing}><QrPage /></Zasticeno>} />
        <Route path="/analitika" element={<Zasticeno allowed={imaPravo(me, 'analitika')} landing={landing}><AnalitikaPage /></Zasticeno>} />

        <Route
          path="/sabloni"
          element={
            <Zasticeno allowed={isKorisnik(me)} landing={landing}>
              <SabloniPage />
            </Zasticeno>
          }
        />
        <Route path="/sabloni/predlozi" element={<Zasticeno allowed={isKorisnik(me)} landing={landing}><SabloniPage pocetniTab="predlozi" /></Zasticeno>} />
        <Route path="/sabloni/:tip/nacrt" element={<Zasticeno allowed={canEditOprema(me)} landing={landing}><NacrtPage /></Zasticeno>} />
        <Route
          path="/pregled/:id/popuni"
          element={
            <Zasticeno allowed={imaPravo(me, 'pregledi.popunjavanje')} landing={landing}>
              <PopunjavanjePage />
            </Zasticeno>
          }
        />
        <Route path="/akcije" element={<Zasticeno allowed={isKorisnik(me)} landing={landing}><AkcijePage /></Zasticeno>} />
        <Route path="/akcije/:id" element={<Zasticeno allowed={isKorisnik(me)} landing={landing}><AkcijaPage /></Zasticeno>} />
        <Route path="/neispravnosti" element={<Zasticeno allowed={isKorisnik(me)} landing={landing}><NeispravnostiPage /></Zasticeno>} />
        <Route path="/neispravnosti/:id" element={<Zasticeno allowed={isKorisnik(me)} landing={landing}><KvarPage /></Zasticeno>} />
        <Route path="/pregledi" element={<Zasticeno allowed={isKorisnik(me)} landing={landing}><PreglediPage /></Zasticeno>} />
        <Route path="/pregledi/:id" element={<Zasticeno allowed={isKorisnik(me)} landing={landing}><PregledDetaljPage /></Zasticeno>} />
        <Route
          path="/kalendar"
          element={
            <Zasticeno allowed={isKorisnik(me)} landing={landing}>
              <KalendarPage />
            </Zasticeno>
          }
        />
        <Route
          path="/raspored"
          element={
            <Zasticeno allowed={imaPravo(me, 'raspored.uredjivanje') || imaPravo(me, 'odsustva.odobravanje')} landing={landing}>
              <RasporedPage />
            </Zasticeno>
          }
        />
        <Route
          path="/moja-odsustva"
          element={
            <Zasticeno allowed={imaPravo(me, 'pregledi.popunjavanje')} landing={landing}>
              <MojaOdsustvaPage />
            </Zasticeno>
          }
        />
        <Route
          path="/neradni-dani"
          element={
            <Zasticeno allowed={isKorisnik(me)} landing={landing}>
              <NeradniDaniPage />
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
