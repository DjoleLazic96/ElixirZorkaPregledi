import { useState } from 'react';
import { useToast } from '../../app/providers/ToastProvider';
import { Button } from '../../components/ui/Button';
import { ConfirmDialog } from '../../components/ui/ConfirmDialog';
import { porukaGreske } from '../../lib/apiError';

/**
 * „Otkaži zahtev“ (zamerka 47): inženjer povlači svoj zahtev dok čeka odluku — uz potvrdu. Zahtev nestaje sa spiska kod
 * Supervizora i Direktora, u istoriji pregleda ostaje zapis, a posle toga može nov zahtev.
 */
export function OtkaziZahtev({ otkazi, opis }: { otkazi: () => Promise<unknown>; opis: string }) {
  const toast = useToast();
  const [pitaj, setPitaj] = useState(false);
  const [salje, setSalje] = useState(false);

  async function potvrdi() {
    setSalje(true);
    try {
      await otkazi();
      toast.success('Zahtev je otkazan.');
      setPitaj(false);
    } catch (err) {
      toast.error(porukaGreske(err, 'Zahtev nije otkazan.'));
    } finally {
      setSalje(false);
    }
  }

  return (
    <>
      <Button size="sm" variant="ghost" onClick={() => setPitaj(true)}>Otkaži zahtev</Button>
      <ConfirmDialog open={pitaj} title="Otkaži zahtev" message={`Otkazati ${opis}? Posle toga možete da pošaljete nov zahtev.`}
        confirmLabel="Otkaži zahtev" cancelLabel="Nazad" loading={salje} onConfirm={() => void potvrdi()} onCancel={() => setPitaj(false)} />
    </>
  );
}
