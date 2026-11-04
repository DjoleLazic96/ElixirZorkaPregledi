import { useEffect, useState } from 'react';
import { useToast } from '../providers/ToastProvider';
import { imaPravo } from '../../lib/permissions';
import { obrisiLokalno, sinhronizuj, useStanjeSinhronizacije } from '../../offline/sinhronizacija';
import { ChevronDown, Info, KeyRound, LogOut, UserRound } from 'lucide-react';
import { OAplikaciji } from '../../components/OAplikaciji';
import { PromenaLozinke } from '../../components/PromenaLozinke';
import { AnchoredDropdown } from '../../components/ui/AnchoredDropdown';
import { useAuth } from '../../features/auth/AuthProvider';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { DonjiMeni, Sidebar } from './Sidebar';

/**
 * App shell (blueprint 14 §1): sidebar + header (ime korisnika i meni) + sadržaj
 * rute. `AppRoutes` garantuje da je korisnik ulogovan pre nego što se ovo mount-uje.
 */
export function AppLayout() {
  const { me, logout } = useAuth();
  const toast = useToast();
  const sinh = useStanjeSinhronizacije();
  const inzenjer = me ? imaPravo(me, 'pregledi.popunjavanje') : false;

  // Inženjer: paket i red za slanje se sinhronizuju odmah, na svakih minut i pri povratku veze (R10.4).
  useEffect(() => {
    if (!inzenjer) return;
    void sinhronizuj().catch(() => {});
    const t = window.setInterval(() => void sinhronizuj().catch(() => {}), 60_000);
    return () => window.clearInterval(t);
  }, [inzenjer]);

  async function odjavi() {
    // Odjava bi obrisala neposlato urađeno; zato se čeka da red bude prazan.
    if (inzenjer && !(await obrisiLokalno())) {
      toast.error('Imate promene koje još nisu poslate. Odjavite se kad se pošalju (potreban je internet).');
      return;
    }
    await logout();
  }
  const location = useLocation();

  const [meniOtvoren, setMeniOtvoren] = useState(false);
  const [meniAnchor, setMeniAnchor] = useState<HTMLButtonElement | null>(null);
  const [oAplikacijiOtvoren, setOAplikacijiOtvoren] = useState(false);
  const [promenaLozinkeOtvorena, setPromenaLozinkeOtvorena] = useState(false);

  if (!me) return null; // teorijski nemoguće, AppRoutes garantuje

  function otvori(setter: (v: boolean) => void) {
    setMeniOtvoren(false);
    setter(true);
  }

  return (
    <div className="flex h-screen bg-gray-50">
      <Sidebar pristup={me} />

      <div className="flex flex-1 flex-col overflow-hidden">
        <header className="flex h-14 items-center justify-between border-b border-gray-200 bg-white px-4 md:justify-end md:px-6">
          {/* Na telefonu zaglavlje ima samo logo i ikonicu korisnika (spec §8.1); logo vodi na početnu. */}
          <Link to="/" className="text-base font-semibold text-primary-600 md:hidden">TPM pregledi</Link>
          <button
            ref={setMeniAnchor}
            type="button"
            onClick={() => setMeniOtvoren((o) => !o)}
            aria-haspopup="menu"
            aria-expanded={meniOtvoren}
            aria-label={me.user.fullName}
            className="flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium text-gray-800 transition-colors hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500"
          >
            <UserRound size={18} className="text-gray-600 md:hidden" aria-hidden />
            <span className="hidden md:inline">{me.user.fullName}</span>
            <ChevronDown size={14} className="hidden text-gray-500 md:inline" aria-hidden />
          </button>

          <AnchoredDropdown
            anchor={meniAnchor}
            open={meniOtvoren}
            onRequestClose={() => setMeniOtvoren(false)}
            widthMode="min-anchor"
            className="w-56 rounded-md border border-gray-200 bg-white py-1 shadow-lg"
          >
            <div role="menu" aria-label="Korisnički meni">
              <StavkaMenija icon={Info} onClick={() => otvori(setOAplikacijiOtvoren)}>
                O aplikaciji
              </StavkaMenija>
              <StavkaMenija icon={KeyRound} onClick={() => otvori(setPromenaLozinkeOtvorena)}>
                Promena lozinke
              </StavkaMenija>
              <div className="my-1 border-t border-gray-100" />
              <StavkaMenija
                icon={LogOut}
                onClick={() => {
                  setMeniOtvoren(false);
                  void odjavi();
                }}
              >
                Odjavi se
              </StavkaMenija>
            </div>
          </AnchoredDropdown>
        </header>
        {/* „Bez interneta“ je traka ispod zaglavlja, ne u zaglavlju (spec §8.1). */}
        {!sinh.online && (
          <div role="status" className="bg-amber-100 px-4 py-1.5 text-xs text-amber-900 md:px-6">
            Bez interneta{inzenjer && ` — čeka slanje: ${sinh.promena} promena, ${sinh.slika} slika`}
          </div>
        )}

        <main className="flex-1 overflow-y-auto p-4 pb-20 md:p-6 md:pb-6 [scrollbar-gutter:stable]">
          {/* `key` na putanji remount-uje omotač pri svakoj navigaciji, pa se
              `animate-fade-in` ponovo okine (isključeno pri reduced-motion). */}
          <div key={location.pathname} className="animate-fade-in">
            <Outlet />
          </div>
        </main>
        <DonjiMeni pristup={me} />
      </div>

      <OAplikaciji open={oAplikacijiOtvoren} onClose={() => setOAplikacijiOtvoren(false)} />
      {/* Uslovno renderovanje resetuje polja forme pri svakom otvaranju. */}
      {promenaLozinkeOtvorena && (
        <PromenaLozinke open onClose={() => setPromenaLozinkeOtvorena(false)} />
      )}
    </div>
  );
}

function StavkaMenija({
  icon: Icon,
  onClick,
  children,
}: {
  icon: typeof Info;
  onClick: () => void;
  children: string;
}) {
  return (
    <button
      type="button"
      role="menuitem"
      onClick={onClick}
      className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-gray-700 hover:bg-gray-100 focus:outline-none focus-visible:bg-gray-100"
    >
      <Icon size={14} className="text-gray-500" aria-hidden />
      {children}
    </button>
  );
}
