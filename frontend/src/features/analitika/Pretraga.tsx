import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Search, X } from 'lucide-react';
import { api } from '../../api/client';
import { Ucitavanje } from '../../components/ui/Spinner';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { linkRezultata, usePretraga, type PretragaRezultatDto } from './hooks';
import { Istaknuto } from './Prikazi';

const VRSTA: Record<PretragaRezultatDto['vrsta'], string> = { uredjaj: 'Uređaj', pregled: 'Pregled', akcija: 'Akcija', kvar: 'Kvar' };

/**
 * Pretraga u zaglavlju (R8.14): broj pregleda, akcije ili kvara, ili deo naziva uređaja. Pronađeni deo je žuto obeležen;
 * Enter ili klik otvara ekran. Na telefonu je ikonica koja otvara polje preko zaglavlja.
 */
export function Pretraga() {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const [odlozeno, setOdlozeno] = useState('');
  const [otvoreno, setOtvoreno] = useState(false);
  const [mobilno, setMobilno] = useState(false);
  const [aktivan, setAktivan] = useState(0);
  const polje = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (mobilno) polje.current?.focus();
  }, [mobilno]);
  useEffect(() => {
    const t = setTimeout(() => setOdlozeno(q), 250);
    return () => clearTimeout(t);
  }, [q]);
  const rez = usePretraga(odlozeno);
  const lista = odlozeno.trim().length >= 2 ? rez.data ?? [] : [];

  function otvori(r: PretragaRezultatDto) {
    navigate(linkRezultata(r));
    setQ('');
    setOtvoreno(false);
    setMobilno(false);
  }

  return (
    <>
    {!mobilno && (
      <button type="button" aria-label="Otvori pretragu" onClick={() => setMobilno(true)}
        className="ml-auto flex min-h-11 min-w-11 items-center justify-center rounded-md text-gray-600 md:hidden">
        <Search size={18} />
      </button>
    )}
    <div className={cn('md:relative md:mr-auto md:block md:w-80', mobilno ? 'absolute inset-x-0 top-0 z-30 flex h-14 items-center gap-2 bg-white px-4' : 'hidden')}>
      <div className="relative flex-1">
        <Search size={16} className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" aria-hidden />
        <input
          ref={polje} type="search" value={q} placeholder="Pretraga: broj ili uređaj" aria-label="Pretraga"
          role="combobox" aria-expanded={otvoreno && lista.length > 0} aria-controls="pretraga-rezultati" aria-autocomplete="list"
          onChange={(e) => { setQ(e.target.value); setOtvoreno(true); setAktivan(0); }}
          onFocus={() => setOtvoreno(true)}
          onBlur={() => setTimeout(() => setOtvoreno(false), 150)}
          onKeyDown={(e) => {
            if (e.key === 'ArrowDown') { e.preventDefault(); setAktivan((a) => Math.min(a + 1, lista.length - 1)); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); setAktivan((a) => Math.max(a - 1, 0)); }
            else if (e.key === 'Enter' && lista[aktivan]) otvori(lista[aktivan]);
            else if (e.key === 'Escape') { setOtvoreno(false); setMobilno(false); }
          }}
          className="h-9 w-full rounded-md border border-gray-300 pl-8 pr-2 text-sm focus:border-primary-500 focus:outline-none focus:ring-1 focus:ring-primary-500"
        />
        {otvoreno && odlozeno.trim().length >= 2 && (
          <ul id="pretraga-rezultati" role="listbox" className="absolute inset-x-0 top-full z-40 mt-1 max-h-96 overflow-y-auto rounded-md border border-gray-200 bg-white py-1 text-sm shadow-lg">
            {lista.length === 0 && <li className="px-3 py-2 text-gray-500">{rez.isFetching ? 'Traženje…' : 'Nema rezultata.'}</li>}
            {lista.map((r, i) => (
              <li key={`${r.vrsta}-${r.id}`} role="option" aria-selected={i === aktivan}
                onMouseDown={(e) => { e.preventDefault(); otvori(r); }} onMouseEnter={() => setAktivan(i)}
                className={cn('flex cursor-pointer items-baseline gap-2 px-3 py-2', i === aktivan && 'bg-gray-100')}>
                <span className="w-16 shrink-0 text-xs text-gray-500">{VRSTA[r.vrsta]}</span>
                <span className="min-w-0">
                  <span className="block break-words font-medium"><Istaknuto tekst={r.tekst} q={odlozeno} /></span>
                  {r.opis && <span className="block truncate text-xs text-gray-500">{r.opis}</span>}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
      {mobilno && <button type="button" aria-label="Zatvori pretragu" className="min-h-11 min-w-11 text-gray-600 md:hidden" onClick={() => setMobilno(false)}><X size={18} className="mx-auto" /></button>}
    </div>
    </>
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
