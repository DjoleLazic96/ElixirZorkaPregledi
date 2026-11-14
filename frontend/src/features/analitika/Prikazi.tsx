import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { cn } from '../../lib/cn';
import { Printer } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { Modal } from '../../components/ui/Modal';
import { formatirajDatum } from '../../lib/datum';
import { formatirajBroj } from '../../lib/broj';
import { deloviPoklapanja, type Stanje, type StavkaBrojDto } from './hooks';
import { STANJA } from './oznake';

/** Pronađeni deo teksta žuto obeležen (R8.14). */
export function Istaknuto({ tekst, q }: { tekst: string; q: string }) {
  return <>{deloviPoklapanja(tekst, q).map((d, i) => (d.pogodak ? <mark key={i} className="rounded-sm bg-yellow-200 px-0">{d.t}</mark> : <span key={i}>{d.t}</span>))}</>;
}

/**
 * Vremenska linija uređaja (R8.6): jedan stubić po danu, boja je stanje sa poslednjeg pregleda (R8.7). Stanje nosi i
 * legenda i tooltip, ne samo boja.
 */
export function VremenskaLinija({ dani }: { dani: { dan: string; stanje: Stanje | null }[] }) {
  return (
    <div>
      <div className="flex h-8 items-stretch gap-px" role="img" aria-label="Istorija stanja po danima">
        {dani.map((d) => (
          <span key={d.dan} title={`${formatirajDatum(d.dan)}: ${d.stanje ? STANJA[d.stanje].labela : 'nije bilo pregleda'}`}
            className={`min-w-px flex-1 first:rounded-l last:rounded-r ${d.stanje ? STANJA[d.stanje].boja : 'bg-gray-100'}`} />
        ))}
      </div>
      <div className="mt-1 flex justify-between text-xs text-gray-500">
        <span>{dani.length > 0 && formatirajDatum(dani[0].dan)}</span>
        <span>{dani.length > 0 && formatirajDatum(dani[dani.length - 1].dan)}</span>
      </div>
      <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-gray-700">
        {Object.values(STANJA).map((s) => (
          <li key={s.labela} className="flex items-center gap-1.5"><span className={`inline-block h-3 w-3 rounded-sm ${s.boja}`} aria-hidden />{s.labela}</li>
        ))}
        <li className="flex items-center gap-1.5"><span className="inline-block h-3 w-3 rounded-sm bg-gray-100 ring-1 ring-gray-200" aria-hidden />Bez pregleda</li>
      </ul>
    </div>
  );
}

/**
 * Pločica sa brojem po mockup-u (`.kpi`): broj 26 px zelen (crven kad je upozorenje), ispod opis sivim. Sa `to` je cela pločica link.
 */
export function Plocica({ naslov, vrednost, opis, upozorenje, to }: { naslov: string; vrednost: string; opis?: string; upozorenje?: boolean; to?: string }) {
  const sadrzaj = (
    <>
      <b className={cn('block text-[26px] font-bold leading-tight tabular-nums', upozorenje ? 'text-red-500' : 'text-primary-500')}>{vrednost}</b>
      <span className="block text-xs text-gray-500">{naslov}</span>
      {opis && <span className="block text-xs text-gray-400">{opis}</span>}
    </>
  );
  const klasa = 'block rounded-[10px] border border-gray-200 bg-white px-3.5 py-3';
  return to ? <Link to={to} className={cn(klasa, 'transition-colors hover:border-primary-200 hover:bg-primary-50')}>{sadrzaj}</Link> : <div className={klasa}>{sadrzaj}</div>;
}

/** Jedna serija vodoravnih stubića (jedna boja; vrednost ispisana pored, uređaj vodi na svoj ekran). */
export function Stubici({ stavke, jedinica = '', prazno = 'Nema podataka za period.' }: { stavke: StavkaBrojDto[]; jedinica?: string; prazno?: string }) {
  if (stavke.length === 0) return <p className="text-sm text-gray-500">{prazno}</p>;
  const max = Math.max(...stavke.map((s) => s.broj), 1);
  return (
    <ul className="space-y-2 text-sm">
      {stavke.map((s) => (
        <li key={`${s.naziv}-${s.id ?? ''}`} title={`${s.naziv}: ${formatirajBroj(s.broj, 0)}${jedinica}`}>
          <div className="flex justify-between gap-2">
            <span className="min-w-0 truncate">{s.id ? <Link className="text-primary-700 hover:underline" to={`/uredjaji/${s.id}`}>{s.naziv}</Link> : s.naziv}</span>
            <span className="whitespace-nowrap tabular-nums text-gray-700">{formatirajBroj(s.broj, 0)}{jedinica}</span>
          </div>
          <div className="mt-1 h-2 rounded-r bg-gray-100"><div className="h-2 rounded-r bg-primary-500" style={{ width: `${(100 * s.broj) / max}%` }} /></div>
        </li>
      ))}
    </ul>
  );
}

/** QR nalepnica uređaja (R5.6): kod vodi na `/qr/{token}`, odakle se otvara ekran uređaja. */
export function QrDijalog({ token, naziv, onClose }: { token: string; naziv: string; onClose: () => void }) {
  const [src, setSrc] = useState<string | null>(null);
  const url = `${window.location.origin}/qr/${token}`;
  useEffect(() => {
    // Biblioteka se učitava tek kad se nalepnica otvori — ne ide u glavni paket (pretraga u zaglavlju uvozi ovaj fajl).
    void import('qrcode').then(({ default: QRCode }) => QRCode.toDataURL(url, { margin: 1, width: 320 })).then(setSrc);
  }, [url]);
  function stampaj() {
    const w = window.open('', '_blank', 'width=420,height=520');
    if (!w || !src) return;
    w.document.title = naziv;
    const img = w.document.createElement('img');
    img.src = src;
    img.style.width = '60mm';
    const p = w.document.createElement('p');
    p.textContent = naziv;
    p.style.font = '600 14pt sans-serif';
    w.document.body.append(img, p);
    img.onload = () => w.print();
  }
  return (
    <Modal open title={`QR nalepnica — ${naziv}`} onClose={onClose}>
      <div className="flex flex-col items-center gap-3">
        {src ? <img src={src} alt={`QR kod za ${naziv}`} className="h-64 w-64" /> : <div className="h-64 w-64 animate-pulse rounded bg-gray-100" />}
        <p className="break-all text-center text-xs text-gray-500">{url}</p>
        <Button onClick={stampaj} disabled={!src}><Printer size={14} aria-hidden /> Štampaj</Button>
      </div>
    </Modal>
  );
}
