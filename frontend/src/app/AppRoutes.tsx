import { lazy } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { adresaPovratka } from '../features/auth/povratak';
import { NijePronadjeno } from '../components/NijePronadjeno';
import { Ucitavanje } from '../components/ui/Spinner';
import { Zasticeno } from '../components/Zasticeno';
import { AdminLayout } from '../features/admin/AdminLayout';
import { useAuth } from '../features/auth/AuthProvider';
import { LoginPage } from '../features/auth/LoginPage';
import { DashboardPage } from '../features/analitika/DashboardPage';
import { canEditOprema, canSeeAdminSection, imaPravo, isAdmin, isKorisnik } from '../lib/permissions';
import { AppLayout } from './layout/AppLayout';

/** Ekrani se učitavaju tek kad zatrebaju (manji prvi paket na telefonu); PWA ih ionako sve čuva za rad bez interneta. */
const PodesavanjaPage = lazy(() => import('../features/admin/podesavanja/PodesavanjaPage').then((x) => ({ default: x.PodesavanjaPage })));
const KorisniciPage = lazy(() => import('../features/admin/korisnici/KorisniciPage').then((x) => ({ default: x.KorisniciPage })));
const AuditLogPage = lazy(() => import('../features/audit-log/AuditLogPage').then((x) => ({ default: x.AuditLogPage })));
const AnalitikaPage = lazy(() => import('../features/analitika/AnalitikaPage').then((x) => ({ default: x.AnalitikaPage })));
const QrPage = lazy(() => import('../features/analitika/Pretraga').then((x) => ({ default: x.QrPage })));
const UredjajEkranPage = lazy(() => import('../features/analitika/UredjajEkranPage').then((x) => ({ default: x.UredjajEkranPage })));
const PopunjavanjePage = lazy(() => import('../features/popunjavanje/PopunjavanjePage').then((x) => ({ default: x.PopunjavanjePage })));
const AkcijaPage = lazy(() => import('../features/akcije/AkcijePages').then((x) => ({ default: x.AkcijaPage })));
const AkcijePage = lazy(() => import('../features/akcije/AkcijePages').then((x) => ({ default: x.AkcijePage })));
const KvarPage = lazy(() => import('../features/akcije/AkcijePages').then((x) => ({ default: x.KvarPage })));
const NeispravnostiPage = lazy(() => import('../features/akcije/AkcijePages').then((x) => ({ default: x.NeispravnostiPage })));
const PregledDetaljPage = lazy(() => import('../features/pregledi/PregledDetaljPage').then((x) => ({ default: x.PregledDetaljPage })));
const PreglediPage = lazy(() => import('../features/pregledi/PreglediPage').then((x) => ({ default: x.PreglediPage })));
const KalendarPage = lazy(() => import('../features/raspored/KalendarPage').then((x) => ({ default: x.KalendarPage })));
const MojaOdsustvaPage = lazy(() => import('../features/raspored/OdsustvaINeradniPages').then((x) => ({ default: x.MojaOdsustvaPage })));
const NeradniDaniPage = lazy(() => import('../features/raspored/OdsustvaINeradniPages').then((x) => ({ default: x.NeradniDaniPage })));
const RasporedPage = lazy(() => import('../features/raspored/RasporedPage').then((x) => ({ default: x.RasporedPage })));
const NacrtPage = lazy(() => import('../features/sabloni/IzmenePages').then((x) => ({ default: x.NacrtPage })));
const SabloniPage = lazy(() => import('../features/sabloni/SabloniPage').then((x) => ({ default: x.SabloniPage })));
const UredjajiPage = lazy(() => import('../features/sabloni/UredjajiPage').then((x) => ({ default: x.UredjajiPage })));
const StanjeServeraPage = lazy(() => import('../features/stanje-servera/StanjeServeraPage').then((x) => ({ default: x.StanjeServeraPage })));

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
