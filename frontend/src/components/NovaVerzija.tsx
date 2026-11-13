import { useState } from 'react';
import { useRegisterSW } from 'virtual:pwa-register/react';
import { RefreshCw } from 'lucide-react';
import { useToast } from '../app/providers/ToastProvider';
import { db, sinhronizuj } from '../offline/sinhronizacija';
import { Button } from './ui/Button';

/** Koliko često aplikacija proverava da li je na serveru nova verzija (otvorena aplikacija na telefonu radi i ceo dan). */
const PROVERA_MS = 30 * 60_000;

/**
 * „Dostupna je nova verzija“ (zamerka 25): service worker preuzme novu verziju u pozadini, a korisnik bira kad će osvežiti.
 * Pre osvežavanja se šalje sve što čeka na telefonu; ako ne može (nema veze), osvežavanje čeka da se ništa ne izgubi.
 */
export function NovaVerzija() {
  const toast = useToast();
  const [radi, setRadi] = useState(false);
  const {
    needRefresh: [nova],
    updateServiceWorker,
  } = useRegisterSW({
    onRegisteredSW(_url, r) {
      if (r) setInterval(() => void r.update().catch(() => {}), PROVERA_MS);
    },
  });
  if (!nova) return null;

  async function osvezi() {
    setRadi(true);
    try {
      await sinhronizuj().catch(() => {});
      const ceka = (await db.red.count()) + (await db.slike.count());
      if (ceka > 0) {
        toast.error('Na telefonu još ima neposlatih promena. Osvežite kad se pošalju (potrebna je veza).');
        return;
      }
      await updateServiceWorker(true);
    } finally {
      setRadi(false);
    }
  }

  return (
    <div role="status" className="mb-2.5 flex flex-wrap items-center justify-between gap-2 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-sm text-primary-800">
      <span className="font-medium">Dostupna je nova verzija aplikacije.</span>
      <Button size="sm" onClick={() => void osvezi()} disabled={radi}>
        <RefreshCw size={14} aria-hidden /> {radi ? 'Šaljem…' : 'Osveži'}
      </Button>
    </div>
  );
}
