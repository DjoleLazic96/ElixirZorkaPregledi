import { useEffect, useState } from 'react';
import { useToast } from '../providers/ToastProvider';
import { ROLA_LABELE, imaPravo, type Rola } from '../../lib/permissions';
import { obrisiLokalno, sinhronizuj, useStanjeSinhronizacije } from '../../offline/sinhronizacija';
import { BellRing, Info, KeyRound, LogOut } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { OAplikaciji } from '../../components/OAplikaciji';
import { PromenaLozinke } from '../../components/PromenaLozinke';
import { AnchoredDropdown } from '../../components/ui/AnchoredDropdown';
import { useAuth } from '../../features/auth/AuthProvider';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Pretraga } from '../../features/analitika/Pretraga';
import { iskljuciPush } from '../../features/obavestenja/hooks';
import { PushDijalog, Zvono } from '../../features/obavestenja/Zvono';
import { DonjiMeni, Sidebar } from './Sidebar';

/**
 * Okvir aplikacije po mockup-u: zaglavlje preko cele širine, ispod zeleni levi meni i centriran sadržaj; na telefonu donji meni. `AppRoutes` garantuje da je korisnik ulogovan pre nego što se ovo mount-uje.
 */
export function AppLayout() {
  const { me, logout } = useAuth();
  const navigate = useNavigate();
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
    // Sledeći korisnik na istom telefonu ne sme da dobija obaveštenja prethodnog.
    await iskljuciPush().catch(() => {});
    // Bez ovoga bi prijava sledećeg korisnika otvorila ekran na kom je prethodni stao.
    navigate('/login', { replace: true });
    await logout();
  }
  const location = useLocation();

  const [meniOtvoren, setMeniOtvoren] = useState(false);
  const [meniAnchor, setMeniAnchor] = useState<HTMLButtonElement | null>(null);
  const [oAplikacijiOtvoren, setOAplikacijiOtvoren] = useState(false);
  const [promenaLozinkeOtvorena, setPromenaLozinkeOtvorena] = useState(false);
  const [pushOtvoren, setPushOtvoren] = useState(false);

  if (!me) return null; // teorijski nemoguće, AppRoutes garantuje

  function otvori(setter: (v: boolean) => void) {
    setMeniOtvoren(false);
    setter(true);
  }

  const rola = me.roles.map((r) => ROLA_LABELE[r as Rola] ?? r).join(', ');

  return (
    <div className="flex h-screen flex-col bg-gray-50">
      {/* Zaglavlje po mockup-u (`header.top`): logo levo, zelena linija ispod; desno pretraga, zvono i korisnik.
          Na telefonu samo logo i ikonice (spec §8.1). */}
      <header className="relative z-20 flex shrink-0 items-center gap-1.5 border-b-[3px] border-primary-500 bg-white px-3 py-2 md:gap-3 md:px-4">
        <Link to="/" title="Početna" className="flex shrink-0 items-center">
          <img src="/logo-elixirzorka.png" alt="ElixirZorka" className="h-[26px] w-auto md:h-[34px]" />
        </Link>
        <span className="hidden truncate border-l border-gray-200 pl-3 text-sm font-semibold text-gray-800 lg:inline">TPM pregledi</span>
        <span className="flex-1" />
        {!sinh.online && <Badge tone="orange" className="hidden md:inline-flex">Bez interneta</Badge>}
        <Pretraga />
        <Zvono />
        <button
          ref={setMeniAnchor}
          type="button"
          onClick={() => setMeniOtvoren((o) => !o)}
          aria-haspopup="menu"
          aria-expanded={meniOtvoren}
          aria-label={me.user.fullName}
          title="Moj nalog"
          className="flex h-11 min-w-11 items-center justify-center rounded-lg text-right transition-colors hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500 md:h-auto md:px-2 md:py-1"
        >
          <span aria-hidden className="text-lg md:hidden">👤</span>
          <span className="hidden leading-tight md:block">
            <b className="block text-[13px] font-semibold text-gray-800">{me.user.fullName}</b>
            <span className="text-xs text-gray-500">{rola}</span>
          </span>
        </button>

        <AnchoredDropdown
          anchor={meniAnchor}
          open={meniOtvoren}
          onRequestClose={() => setMeniOtvoren(false)}
          widthMode="min-anchor"
          className="w-60 rounded-[10px] border border-gray-200 bg-white py-1.5 shadow-lg"
        >
          <div role="menu" aria-label="Korisnički meni">
            <p className="border-b border-gray-100 px-3.5 pb-2 pt-1 text-xs text-gray-500 md:hidden">{me.user.fullName} · {rola}</p>
            <StavkaMenija icon={Info} onClick={() => otvori(setOAplikacijiOtvoren)}>
              O aplikaciji
            </StavkaMenija>
            <StavkaMenija icon={BellRing} onClick={() => otvori(setPushOtvoren)}>
              Obaveštenja na telefonu
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

      <div className="flex min-h-0 flex-1">
        <Sidebar pristup={me} onOdjava={() => void odjavi()} />

        <main className="min-w-0 flex-1 overflow-y-auto px-3 pb-24 pt-3 md:px-6 md:pb-10 md:pt-5 [scrollbar-gutter:stable]">
          {/* Sadržaj je centriran i iste širine na svim ekranima (mockup: radni deo do 1130 px). */}
          <div className="mx-auto w-full max-w-[1130px]">
            {/* „Bez interneta“ je traka na vrhu sadržaja, ne u zaglavlju (spec §8.1; mockup `.offbar`). */}
            {!sinh.online && (
              <div role="status" className="mb-2.5 rounded-lg border border-[#f0b37a] bg-[#fff3e0] px-2.5 py-1.5 text-[13px] font-semibold text-[#9a4a00]">
                Bez interneta{inzenjer && ` — čeka slanje: ${sinh.promena} promena, ${sinh.slika} slika`}
              </div>
            )}
            {/* `key` na putanji remount-uje omotač pri svakoj navigaciji, pa se
                `animate-fade-in` ponovo okine (isključeno pri reduced-motion). */}
            <div key={location.pathname} className="animate-fade-in">
              <Outlet />
            </div>
          </div>
        </main>
      </div>
      <DonjiMeni pristup={me} />

      <OAplikaciji open={oAplikacijiOtvoren} onClose={() => setOAplikacijiOtvoren(false)} />
      {/* Uslovno renderovanje resetuje polja forme pri svakom otvaranju. */}
      {promenaLozinkeOtvorena && (
        <PromenaLozinke open onClose={() => setPromenaLozinkeOtvorena(false)} />
      )}
      {pushOtvoren && <PushDijalog onClose={() => setPushOtvoren(false)} />}
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
      className="flex min-h-11 w-full items-center gap-2.5 px-3.5 text-left text-sm text-gray-800 hover:bg-primary-50 focus:outline-none focus-visible:bg-primary-50 md:min-h-10"
    >
      <Icon size={14} className="text-gray-500" aria-hidden />
      {children}
    </button>
  );
}
