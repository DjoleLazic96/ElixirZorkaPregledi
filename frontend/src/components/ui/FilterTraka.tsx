import { useState, type ReactNode } from 'react';
import { SlidersHorizontal, X } from 'lucide-react';
import { cn } from '../../lib/cn';
import { Card, CardBody } from './Card';

/** Aktivan filter kao oznaka sa ×. */
export interface AktivanFilter {
  /** Jedinstven ključ (npr. naziv polja). */
  kljuc: string;
  /** Tekst oznake, npr. „Otvorene“ ili „Sekcija 30“. */
  labela: string;
  /** Skida filter (vraća polje na „sve“). */
  ukloni: () => void;
  /** Tekstualna pretraga: kucanje ne zatvara polja na telefonu. */
  kucanje?: boolean;
}

function Oznake({ aktivni, onObrisiSve }: { aktivni: AktivanFilter[]; onObrisiSve?: () => void }) {
  return (
    <>
      {aktivni.map((a) => (
        <span key={a.kljuc} className="inline-flex max-w-full items-center gap-1 rounded-full border border-primary-200 bg-primary-50 py-1 pl-3 pr-1 text-xs font-medium text-primary-800">
          <span className="truncate">{a.labela}</span>
          <button type="button" onClick={a.ukloni} aria-label={`Ukloni filter ${a.labela}`}
            className="inline-flex h-6 w-6 shrink-0 items-center justify-center rounded-full hover:bg-primary-100">
            <X size={12} aria-hidden />
          </button>
        </span>
      ))}
      {onObrisiSve && aktivni.length > 1 && (
        <button type="button" onClick={onObrisiSve} className="text-xs font-medium text-gray-600 underline-offset-2 hover:underline">Obriši sve</button>
      )}
    </>
  );
}

/**
 * Filteri iznad liste (zamerka 51). Na telefonu i uspravnom tabletu (ispod `lg`) polja su skupljena iza dugmeta
 * „Filteri (n)“, a desno od njega stoje izabrane vrednosti kao oznake sa ×; posle izbora se polja sama zatvore. Na računaru
 * su polja otvorena u jednom redu, a oznake ispod njih.
 */
export function FilterTraka({ aktivni, onObrisiSve, kolone = 'sm:grid-cols-2 lg:grid-cols-4', ugradjeno = false, children }: {
  aktivni: AktivanFilter[];
  onObrisiSve?: () => void;
  /** Tailwind klase za broj kolona mreže polja. */
  kolone?: string;
  /** Unutar postojeće kartice (npr. kartica „Pregledi“ na stranici uređaja) — bez sopstvenog okvira. */
  ugradjeno?: boolean;
  children: ReactNode;
}) {
  const [otvoren, setOtvoren] = useState(false);
  // Izbor vrednosti menja oznake — tada se polja na telefonu zatvaraju (podešavanje stanja tokom rendera, bez efekta).
  const potpis = aktivni.filter((a) => !a.kucanje).map((a) => `${a.kljuc}=${a.labela}`).join('|');
  const [prethodni, setPrethodni] = useState(potpis);
  if (potpis !== prethodni) {
    setPrethodni(potpis);
    setOtvoren(false);
  }

  const sadrzaj = (
    <>
        <div className="flex flex-wrap items-center gap-2 lg:hidden">
          <button type="button" onClick={() => setOtvoren((x) => !x)} aria-expanded={otvoren}
            className="inline-flex h-11 items-center gap-1.5 rounded-lg border border-gray-200 bg-white px-3 text-sm font-medium text-gray-800 hover:bg-gray-50">
            <SlidersHorizontal size={14} aria-hidden /> Filteri{aktivni.length > 0 && ` (${aktivni.length})`}
          </button>
          <Oznake aktivni={aktivni} onObrisiSve={onObrisiSve} />
        </div>
        <div className={cn('items-center gap-3 lg:grid', kolone, otvoren ? 'grid' : 'hidden')}>{children}</div>
        {aktivni.length > 0 && (
          <div className="hidden flex-wrap items-center gap-2 lg:flex">
            <Oznake aktivni={aktivni} onObrisiSve={onObrisiSve} />
          </div>
        )}
    </>
  );
  if (ugradjeno) return <div className="space-y-3">{sadrzaj}</div>;
  return (
    <Card>
      <CardBody className="space-y-3">{sadrzaj}</CardBody>
    </Card>
  );
}
