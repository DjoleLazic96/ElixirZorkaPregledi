import { useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { ChevronDown, Info, KeyRound, LogOut } from 'lucide-react';
import { OAplikaciji } from '../../components/OAplikaciji';
import { PromenaLozinke } from '../../components/PromenaLozinke';
import { AnchoredDropdown } from '../../components/ui/AnchoredDropdown';
import { useAuth } from '../../features/auth/AuthProvider';
import { Sidebar } from './Sidebar';

/**
 * App shell (blueprint 14 §1): sidebar + header (ime korisnika i meni) + sadržaj
 * rute. `AppRoutes` garantuje da je korisnik ulogovan pre nego što se ovo mount-uje.
 */
export function AppLayout() {
  const { me, logout } = useAuth();
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
      <Sidebar roles={me.roles} />

      <div className="flex flex-1 flex-col overflow-hidden">
        <header className="flex h-14 items-center justify-end border-b border-gray-200 bg-white px-6">
          <button
            ref={setMeniAnchor}
            type="button"
            onClick={() => setMeniOtvoren((o) => !o)}
            aria-haspopup="menu"
            aria-expanded={meniOtvoren}
            className="flex items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium text-gray-800 transition-colors hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500"
          >
            {me.user.fullName}
            <ChevronDown size={14} className="text-gray-500" aria-hidden />
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
                  void logout();
                }}
              >
                Odjavi se
              </StavkaMenija>
            </div>
          </AnchoredDropdown>
        </header>

        <main className="flex-1 overflow-y-auto p-6 [scrollbar-gutter:stable]">
          {/* `key` na putanji remount-uje omotač pri svakoj navigaciji, pa se
              `animate-fade-in` ponovo okine (isključeno pri reduced-motion). */}
          <div key={location.pathname} className="animate-fade-in">
            <Outlet />
          </div>
        </main>
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
