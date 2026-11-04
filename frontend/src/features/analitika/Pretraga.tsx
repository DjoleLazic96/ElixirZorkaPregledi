import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../../api/client';
import { Input } from '../../components/ui/Input';
import { Modal } from '../../components/ui/Modal';
import { Ucitavanje } from '../../components/ui/Spinner';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { linkRezultata, usePretraga, type PretragaRezultatDto } from './hooks';
import { Istaknuto } from './Prikazi';

const VRSTA: Record<PretragaRezultatDto['vrsta'], string> = { uredjaj: 'Uređaj', pregled: 'Pregled', akcija: 'Akcija', kvar: 'Kvar' };

/**
 * Pretraga u zaglavlju (R8.14) po mockup-u: dugme „🔍 Pretraga“ (na telefonu samo 🔍) otvara prozor sa poljem. Traži se broj
 * pregleda, akcije ili kvara, ili deo naziva uređaja; pronađeni deo je žuto obeležen, Enter ili klik otvara ekran.
 */
export function Pretraga() {
  const [otvoreno, setOtvoreno] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOtvoreno(true)} title="Pretraga po broju ili uređaju" aria-label="Pretraga"
        className="flex h-11 min-w-11 shrink-0 items-center justify-center gap-1.5 whitespace-nowrap rounded-lg border border-gray-200 px-2.5 text-[13px] text-gray-500 hover:bg-gray-50 md:h-9">
        <span aria-hidden>🔍</span>
        <span className="hidden md:inline">Pretraga</span>
      </button>
      {otvoreno && <PretragaDijalog onClose={() => setOtvoreno(false)} />}
    </>
  );
}

function PretragaDijalog({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const [odlozeno, setOdlozeno] = useState('');
  const [aktivan, setAktivan] = useState(0);
  useEffect(() => {
    const t = setTimeout(() => setOdlozeno(q), 250);
    return () => clearTimeout(t);
  }, [q]);
  const rez = usePretraga(odlozeno);
  const lista = odlozeno.trim().length >= 2 ? rez.data ?? [] : [];

  function otvori(r: PretragaRezultatDto) {
    onClose();
    navigate(linkRezultata(r));
  }

  return (
    <Modal open title="Pretraga" onClose={onClose}>
      <div className="space-y-3">
        <Input
          autoFocus type="search" value={q} placeholder="Broj pregleda, akcije ili kvara, ili deo naziva uređaja" aria-label="Pretraga"
          role="combobox" aria-expanded={lista.length > 0} aria-controls="pretraga-rezultati" aria-autocomplete="list"
          onChange={(e) => { setQ(e.target.value); setAktivan(0); }}
          onKeyDown={(e) => {
            if (e.key === 'ArrowDown') { e.preventDefault(); setAktivan((a) => Math.min(a + 1, lista.length - 1)); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); setAktivan((a) => Math.max(a - 1, 0)); }
            else if (e.key === 'Enter' && lista[aktivan]) otvori(lista[aktivan]);
          }}
        />
        {odlozeno.trim().length < 2 ? <p className="text-xs text-gray-500">Upišite bar dva znaka.</p> : (
          <ul id="pretraga-rezultati" role="listbox" className="flex flex-col gap-2">
            {lista.length === 0 && <li className="text-sm text-gray-500">{rez.isFetching ? 'Traženje…' : 'Nema rezultata.'}</li>}
            {lista.map((r, i) => (
              <li key={`${r.vrsta}-${r.id}`} role="option" aria-selected={i === aktivan}
                onMouseDown={(e) => { e.preventDefault(); otvori(r); }} onMouseEnter={() => setAktivan(i)}
                className={cn('relative flex cursor-pointer items-baseline gap-2.5 rounded-[14px] border px-3 py-2.5 pr-8 text-sm transition-colors',
                  i === aktivan ? 'border-primary-200 bg-primary-50' : 'border-gray-200 bg-[#f7f8f6]')}>
                <span className="w-16 shrink-0 text-xs text-gray-500">{VRSTA[r.vrsta]}</span>
                <span className="min-w-0">
                  <span className="block break-words font-semibold"><Istaknuto tekst={r.tekst} q={odlozeno} /></span>
                  {r.opis && <span className="block truncate text-xs text-gray-500">{r.opis}</span>}
                </span>
                <span aria-hidden className="absolute right-3 top-2 text-lg text-gray-500">›</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}

/** `/qr/{token}` sa nalepnice: pronalazi uređaj i otvara njegov ekran (R5.6). */
export function QrPage() {
  const { token } = useParams();
  const navigate = useNavigate();
  const [greska, setGreska] = useState<string | null>(null);
  useEffect(() => {
    api.get<{ id: number }>(`/uredjaji/qr/${token}`)
      .then((r) => navigate(`/uredjaji/${r.data.id}`, { replace: true }))
      .catch((e: unknown) => setGreska(porukaGreske(e, 'Uređaj sa ovim QR kodom ne postoji.')));
  }, [token, navigate]);
  return greska ? <p role="alert" className="text-sm text-red-600">{greska}</p> : <Ucitavanje />;
}
