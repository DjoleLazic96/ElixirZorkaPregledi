import { useRef, useState } from 'react';
import { HelpCircle } from 'lucide-react';
import { useToast } from '../../app/providers/ToastProvider';
import { Slike } from '../../components/Slike';
import { Button } from '../../components/ui/Button';
import { Modal } from '../../components/ui/Modal';
import { Textarea } from '../../components/ui/Textarea';
import { porukaGreske } from '../../lib/apiError';
import { formatirajVreme } from '../../lib/datum';
import { useIzmenaUputstva, useUputstvo } from './izmene';

/** Ikonica „?“ uz aktivnost (R2.4); otvara uputstvo. */
export function UputstvoDugme({ aktivnostId, aktivnost, smeDaMenja, offlineTekst }: { aktivnostId: number; aktivnost: string; smeDaMenja: boolean; offlineTekst?: string | null }) {
  const [otvoreno, setOtvoreno] = useState(false);
  return (
    <>
      <button type="button" aria-label={`Uputstvo — ${aktivnost}`} onClick={() => setOtvoreno(true)}
        className="inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-gray-500 hover:bg-gray-100 hover:text-primary-700">
        <HelpCircle size={16} aria-hidden />
      </button>
      {otvoreno && <UputstvoDijalog aktivnostId={aktivnostId} aktivnost={aktivnost} smeDaMenja={smeDaMenja} offlineTekst={offlineTekst} onClose={() => setOtvoreno(false)} />}
    </>
  );
}

/**
 * Uputstvo aktivnosti: tekst i do 3 slike; važi u svim šablonima. Admin i Direktor ga unose ovde („Unesi / Izmeni uputstvo“).
 * Bez interneta prikazuje tekst iz paketa za popunjavanje (`offlineTekst`).
 */
export function UputstvoDijalog({ aktivnostId, aktivnost, smeDaMenja, offlineTekst, onClose }: {
  aktivnostId: number; aktivnost: string; smeDaMenja: boolean; offlineTekst?: string | null; onClose: () => void;
}) {
  const u = useUputstvo(aktivnostId);
  const izmena = useIzmenaUputstva(aktivnostId);
  const toast = useToast();
  const [uredjuje, setUredjuje] = useState(false);
  const [tekst, setTekst] = useState('');
  const ulaz = useRef<HTMLInputElement>(null);
  const tekstPrikaz = u.data ? u.data.tekst : offlineTekst ?? null;

  async function izmeni(v: Parameters<typeof izmena.mutateAsync>[0], uspeh: string) {
    try {
      await izmena.mutateAsync(v);
      toast.success(uspeh);
      setUredjuje(false);
    } catch (e) {
      toast.error(porukaGreske(e, 'Izmena uputstva nije uspela.'));
    }
  }

  return (
    <Modal open title={`Uputstvo — ${aktivnost}`} onClose={onClose} widthClass="max-w-lg">
      <div className="space-y-3 text-sm">
        {uredjuje ? (
          <>
            <Textarea aria-label="Tekst uputstva" rows={6} value={tekst} onChange={(e) => setTekst(e.target.value)} maxLength={4000} />
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setUredjuje(false)}>Odustani</Button>
              <Button disabled={izmena.isPending} onClick={() => void izmeni({ tekst }, 'Uputstvo je sačuvano.')}>Sačuvaj</Button>
            </div>
          </>
        ) : (
          <p className="whitespace-pre-wrap break-words">{tekstPrikaz ?? <span className="text-gray-500">Uputstvo još nije uneto.</span>}</p>
        )}
        {u.isError && !u.data && <p className="text-xs text-gray-500">Slike uputstva se vide kad ima interneta.</p>}
        {u.data && u.data.slike.length > 0 && (
          <div className="space-y-1">
            <Slike ids={u.data.slike} putanja={(id) => `/aktivnosti/${aktivnostId}/uputstvo/slike/${id}`} />
            {smeDaMenja && (
              <div className="flex flex-wrap gap-2">
                {u.data.slike.map((id, i) => (
                  <Button key={id} size="sm" variant="ghost" onClick={() => void izmeni({ ukloni: id }, 'Slika je uklonjena.')}>Ukloni sliku {i + 1}</Button>
                ))}
              </div>
            )}
          </div>
        )}
        {u.data?.azuriranoAt && <p className="text-xs text-gray-500">Poslednja izmena: {u.data.azurirao}, {formatirajVreme(u.data.azuriranoAt)}</p>}
        {smeDaMenja && u.data && !uredjuje && (
          <div className="flex flex-wrap gap-2 border-t border-gray-100 pt-3">
            <Button variant="secondary" onClick={() => { setTekst(u.data?.tekst ?? ''); setUredjuje(true); }}>
              {u.data.tekst ? 'Izmeni uputstvo' : 'Unesi uputstvo'}
            </Button>
            {u.data.slike.length < 3 && (
              <>
                <input ref={ulaz} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" aria-label="Slika uputstva"
                  onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ''; if (f) void izmeni({ slika: f }, 'Slika je dodata.'); }} />
                <Button variant="secondary" disabled={izmena.isPending} onClick={() => ulaz.current?.click()}>Dodaj sliku ({u.data.slike.length}/3)</Button>
              </>
            )}
          </div>
        )}
      </div>
    </Modal>
  );
}
