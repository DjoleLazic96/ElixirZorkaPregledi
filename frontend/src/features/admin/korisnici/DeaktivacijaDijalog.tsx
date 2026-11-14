import { useState } from 'react';
import type { KorisnikDto } from '../../../api/types';
import { Button } from '../../../components/ui/Button';
import { Modal } from '../../../components/ui/Modal';
import { Select } from '../../../components/ui/Select';
import { formatirajDatum, kratakDan } from '../../../lib/datum';
import { useInzenjeri } from '../../raspored/hooks';

/** Planiran pregled korisnika sa predlogom zamene (isti oblik kao kod odsustva). */
export interface PregledZaPrebacivanje {
  pregled: { id: number; broj: string; planiraniDatum: string; status: string };
  predlogZameneId: string | null;
}

/**
 * Deaktivacija inženjera sa zakazanim pregledima (zamerka 54): spisak planiranih pregleda sa izborom zamene (predlog po
 * rotaciji, kao kod odsustva). Započet pregled se ne prebacuje automatski.
 */
export function DeaktivacijaDijalog({ korisnik, pregledi, salje, onPotvrdi, onClose }: {
  korisnik: KorisnikDto;
  pregledi: PregledZaPrebacivanje[];
  salje: boolean;
  onPotvrdi: (prebacivanja: { pregledId: number; zamenaId: string }[]) => void;
  onClose: () => void;
}) {
  const inzenjeri = useInzenjeri();
  const opcije = (inzenjeri.data ?? []).filter((i) => i.id !== korisnik.id).map((i) => ({ value: i.id, label: i.ime }));
  const [zamene, setZamene] = useState<Record<number, string | null>>(
    () => Object.fromEntries(pregledi.map((x) => [x.pregled.id, x.pregled.status === 'planiran' ? x.predlogZameneId : null])),
  );
  const prebacivanja = pregledi
    .filter((x) => x.pregled.status === 'planiran' && zamene[x.pregled.id])
    .map((x) => ({ pregledId: x.pregled.id, zamenaId: zamene[x.pregled.id]! }));

  return (
    <Modal open title="Deaktivacija korisnika" onClose={onClose}>
      <div className="space-y-3 text-sm">
        <p>„{korisnik.ime}“ više neće moći da se prijavi. Ima zakazanih pregleda — izaberite ko ih preuzima (prazno = pregled ostaje i rešava se kasnije u rasporedu).</p>
        <ul className="space-y-2">
          {pregledi.map((x) => (
            <li key={x.pregled.id} className="flex flex-wrap items-center gap-2">
              {/* Ista širina natpisa u svakom redu, pa su liste zamena poravnate u kolonu (zamerka 67). */}
              <span className="w-full sm:w-56 sm:shrink-0 sm:whitespace-nowrap">{x.pregled.broj} · {kratakDan(x.pregled.planiraniDatum)}, {formatirajDatum(x.pregled.planiraniDatum)}</span>
              {x.pregled.status === 'planiran' ? (
                <Select className="w-full min-w-0 sm:w-auto sm:flex-1" aria-label={`Zamena za ${x.pregled.broj}`} value={zamene[x.pregled.id] ?? null}
                  nullable placeholder="Ne prebacuj" options={opcije} onChange={(v) => setZamene({ ...zamene, [x.pregled.id]: v })} />
              ) : (
                <span className="text-xs text-amber-700">započet — ne prebacuje se automatski</span>
              )}
            </li>
          ))}
        </ul>
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="ghost" onClick={onClose} disabled={salje}>Odustani</Button>
          <Button variant="danger" onClick={() => onPotvrdi(prebacivanja)} disabled={salje}>
            {prebacivanja.length ? 'Deaktiviraj i prebaci' : 'Deaktiviraj'}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
