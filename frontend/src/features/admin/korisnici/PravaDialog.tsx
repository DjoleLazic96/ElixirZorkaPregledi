import { useState, type FormEvent } from 'react';
import type { KorisnikDto } from '../../../api/types';
import { Button } from '../../../components/ui/Button';
import { Checkbox } from '../../../components/ui/Checkbox';
import { Modal } from '../../../components/ui/Modal';
import { PODRAZUMEVANA_PRAVA, PRAVA, ROLA_LABELE, type Pravo } from '../../../lib/permissions';

interface PravaDialogProps {
  korisnik: KorisnikDto;
  saving: boolean;
  /** 422 poruka sa backenda (npr. popunjavanje samo za Inženjera). */
  greska?: string | null;
  onSubmit: (prava: Pravo[]) => void;
  onClose: () => void;
}

/**
 * Štikliranje prava (R1.6). Prezentaciono — roditelj šalje mutaciju i prikazuje toast.
 * „Popunjavanje pregleda“ je onemogućeno za sve osim Inženjera (R1.8); backend to ionako odbija.
 */
export function PravaDialog({ korisnik, saving, greska, onSubmit, onClose }: PravaDialogProps) {
  const [izabrana, setIzabrana] = useState<Set<Pravo>>(new Set(korisnik.prava));
  const samoInzenjer = korisnik.rola !== 'Inzenjer';

  const prebaci = (p: Pravo) =>
    setIzabrana((s) => {
      const n = new Set(s);
      if (n.has(p)) n.delete(p);
      else n.add(p);
      return n;
    });

  const potvrdi = (e: FormEvent) => {
    e.preventDefault();
    onSubmit(PRAVA.map((p) => p.value).filter((p) => izabrana.has(p)));
  };

  return (
    <Modal open title={`Prava — ${korisnik.ime}`} onClose={onClose}>
      <form onSubmit={potvrdi} className="space-y-4" noValidate>
        <p className="text-sm text-gray-600">
          Rola: <span className="font-medium text-gray-900">{ROLA_LABELE[korisnik.rola]}</span>. Nova prava važe
          posle sledeće prijave korisnika (najkasnije za 15 minuta).
        </p>
        <div className="space-y-2">
          {PRAVA.map((p) => {
            const zakljucano = p.value === 'pregledi.popunjavanje' && samoInzenjer;
            return (
              <Checkbox
                key={p.value}
                label={p.label}
                checked={izabrana.has(p.value)}
                disabled={saving || zakljucano}
                description={zakljucano ? 'Samo za Inženjera.' : undefined}
                onChange={() => prebaci(p.value)}
              />
            );
          })}
        </div>
        {greska && (
          <p role="alert" className="text-sm text-red-600">
            {greska}
          </p>
        )}
        <div className="flex flex-wrap justify-end gap-2 pt-2">
          <Button
            type="button"
            variant="ghost"
            onClick={() => setIzabrana(new Set(PODRAZUMEVANA_PRAVA[korisnik.rola]))}
            disabled={saving}
          >
            Vrati podrazumevana
          </Button>
          <Button type="button" variant="ghost" onClick={onClose} disabled={saving}>
            Odustani
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Čuvanje…' : 'Sačuvaj prava'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
