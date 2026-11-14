import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useToast } from '../../app/providers/ToastProvider';
import { Button } from '../../components/ui/Button';
import { AnchoredDropdown } from '../../components/ui/AnchoredDropdown';
import { Modal } from '../../components/ui/Modal';
import { api } from '../../api/client';
import { cn } from '../../lib/cn';
import { relativnoVreme } from '../../lib/datum';
import { iskljuciPush, pushPodrzan, trenutnaPretplata, ukljuciPush, useProcitano, useZvono, type ObavestenjeDto } from './hooks';

/**
 * Zvono u zaglavlju (R9.1, R8.12): broj = nepročitana obaveštenja + ono što čeka moju odluku. Klik na obaveštenje ga označava
 * pročitanim i otvara njegov ekran.
 */
export function Zvono() {
  const navigate = useNavigate();
  const zvono = useZvono();
  const procitano = useProcitano();
  const [anchor, setAnchor] = useState<HTMLButtonElement | null>(null);
  const [otvoreno, setOtvoreno] = useState(false);
  const d = zvono.data;
  const ukupno = (d?.neprocitanih ?? 0) + (d?.cekaOdluku ?? 0);

  function otvori(o: ObavestenjeDto) {
    setOtvoreno(false);
    if (!o.procitano) procitano.mutate([o.id]);
    if (o.link) navigate(o.link);
  }

  return (
    <>
      <button ref={setAnchor} type="button" onClick={() => setOtvoreno((x) => !x)} aria-haspopup="dialog" aria-expanded={otvoreno}
        aria-label={ukupno ? `Obaveštenja (${ukupno})` : 'Obaveštenja'}
        className="relative flex h-11 min-w-11 shrink-0 items-center justify-center rounded-lg text-xl hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500 md:h-9">
        <span aria-hidden>🔔</span>
        {ukupno > 0 && (
          <span className="absolute right-0.5 top-0.5 min-w-[18px] rounded-full bg-red-500 px-1.5 text-center text-[10px] font-semibold leading-4 text-white">{ukupno > 99 ? '99+' : ukupno}</span>
        )}
      </button>
      <AnchoredDropdown anchor={anchor} open={otvoreno} onRequestClose={() => setOtvoreno(false)} widthMode="min-anchor"
        className="w-[min(340px,calc(100vw-1.5rem))] rounded-[10px] border border-gray-200 bg-white py-1.5 shadow-[0_6px_20px_rgba(0,0,0,0.15)]">
        <div role="dialog" aria-label="Obaveštenja" className="text-sm">
          <div className="flex items-center justify-between border-b border-gray-100 px-3.5 py-1.5">
            <span className="font-semibold">Obaveštenja</span>
            {(d?.neprocitanih ?? 0) > 0 && <button type="button" className="min-h-11 text-xs text-primary-700 hover:underline md:min-h-0" onClick={() => procitano.mutate(null)}>Označi sve kao pročitano</button>}
          </div>
          {(d?.cekaOdluku ?? 0) > 0 && (
            <button type="button" onClick={() => { setOtvoreno(false); navigate('/'); }}
              className="block w-full border-b border-gray-100 bg-green-50 px-3.5 py-2.5 text-left hover:bg-green-100">
              <b>Čeka moju odluku: {d!.cekaOdluku}</b>
              <small className="block text-xs text-gray-500">Overa, dorada, prebacivanje, odsustva, predlozi šablona</small>
            </button>
          )}
          <ul className="max-h-96 overflow-y-auto">
            {!d?.stavke.length && <li className="px-3.5 py-4 text-gray-500">Nema obaveštenja.</li>}
            {d?.stavke.map((o) => (
              <li key={o.id}>
                <button type="button" onClick={() => otvori(o)} className={cn('block w-full border-b border-gray-100 px-3.5 py-2.5 text-left text-[13px] hover:bg-gray-50', !o.procitano && 'bg-primary-50/60')}>
                  <span className={cn('block break-words', !o.procitano && 'font-semibold')}>{o.naslov}</span>
                  <span className="block break-words text-xs text-gray-600">{o.tekst}</span>
                  <span className="block text-xs text-gray-400">{relativnoVreme(o.kreiranoAt)}</span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      </AnchoredDropdown>
    </>
  );
}

/** „Obaveštenja na telefonu“ iz korisničkog menija: uključi ili isključi push na ovom uređaju i pošalji probno. */
export function PushDijalog({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const [ukljuceno, setUkljuceno] = useState<boolean | null>(null);
  const [radi, setRadi] = useState(false);
  useEffect(() => {
    void trenutnaPretplata().then((p) => setUkljuceno(p !== null)).catch(() => setUkljuceno(false));
  }, []);

  async function promeni(ukljuci: boolean) {
    setRadi(true);
    try {
      if (ukljuci) await ukljuciPush();
      else await iskljuciPush();
      setUkljuceno(ukljuci);
      toast.success(ukljuci ? 'Obaveštenja na ovom uređaju su uključena.' : 'Obaveštenja na ovom uređaju su isključena.');
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Promena nije uspela.');
    } finally {
      setRadi(false);
    }
  }

  return (
    <Modal open title="Obaveštenja na telefonu" onClose={onClose}>
      <div className="space-y-3 text-sm">
        {!pushPodrzan() ? (
          <p>Ovaj pregledač ne podržava obaveštenja. Na telefonu koristite Chrome i dodajte aplikaciju na početni ekran.</p>
        ) : (
          <>
            <p>Obaveštenja stižu na ovaj uređaj i kad aplikacija nije otvorena, a ista su i u zvonu i mejlom.</p>
            <p>Stanje na ovom uređaju: <strong>{ukljuceno === null ? '…' : ukljuceno ? 'uključeno' : 'isključeno'}</strong></p>
            <div className="flex flex-wrap gap-2">
              {ukljuceno ? (
                <>
                  <Button variant="secondary" disabled={radi} onClick={() => void promeni(false)}>Isključi</Button>
                  <Button disabled={radi} onClick={() => void api.post('/obavestenja/probno').then(() => toast.success('Probno obaveštenje je poslato.'))}>Pošalji probno</Button>
                </>
              ) : (
                <Button disabled={radi || ukljuceno === null} onClick={() => void promeni(true)}>Uključi obaveštenja</Button>
              )}
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}
