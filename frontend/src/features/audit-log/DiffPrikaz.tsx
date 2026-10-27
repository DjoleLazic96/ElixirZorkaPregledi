import type { AuditAkcija } from '../../api/types';
import { poljeLabela } from './labele';

/**
 * Detalj jednog audit reda — polje-po-polje (blueprint 08 §5).
 *
 *   UPDATE — samo izmenjena polja (staro precrtano crveno → novo zeleno)
 *   CREATE — sve nove vrednosti; DELETE — sve stare
 *   LOGIN / LOGIN_FAIL / LOGOUT — samo opis događaja
 *   START  — opis + vrednosti (verzija, git SHA, okruženje) → istorija deploy-a
 *
 * Ivični slučaj: UPDATE gde su se promenila samo zaštićena polja (interceptor
 * ih ne serijalizuje) nema vidljivih izmena — prikazuje se objašnjenje, ne prazan modal.
 */
interface DiffPrikazProps {
  akcija: AuditAkcija;
  stareVrednosti: string | null;
  noveVrednosti: string | null;
}

type Vrednosti = Record<string, unknown>;

/** Bezbedno parsira JSON string u objekat; nevalidan/prazan → null. */
function parsiraj(raw: string | null): Vrednosti | null {
  if (!raw) return null;
  try {
    const o: unknown = JSON.parse(raw);
    return o && typeof o === 'object' && !Array.isArray(o) ? (o as Vrednosti) : null;
  } catch {
    return null;
  }
}

/** null → „(prazno)", bool → Da/Ne, objekat → JSON. */
function formatirajVrednost(v: unknown): string {
  if (v === null || v === undefined) return '(prazno)';
  if (typeof v === 'boolean') return v ? 'Da' : 'Ne';
  if (typeof v === 'object') return JSON.stringify(v);
  return String(v);
}

const OPIS: Partial<Record<AuditAkcija, string>> = {
  LOGIN: 'Uspešna prijava korisnika.',
  LOGIN_FAIL: 'Neuspešna prijava (pogrešni podaci, zaključan ili deaktiviran nalog).',
  LOGOUT: 'Odjava korisnika.',
  START: 'Pokretanje aplikacije (deploy ili restart).',
};

export function DiffPrikaz({ akcija, stareVrednosti, noveVrednosti }: DiffPrikazProps) {
  const opis = OPIS[akcija];
  const stare = parsiraj(stareVrednosti);
  const nove = parsiraj(noveVrednosti);

  if (akcija === 'LOGIN' || akcija === 'LOGIN_FAIL' || akcija === 'LOGOUT') {
    return <p className="text-sm text-gray-700">{opis}</p>;
  }
  if (akcija === 'START') {
    return (
      <div className="space-y-3">
        <p className="text-sm text-gray-700">{opis}</p>
        <VrednostiTabela naslov="Podaci o pokretanju" vrednosti={nove} />
      </div>
    );
  }
  if (akcija === 'CREATE') {
    return <VrednostiTabela naslov="Nove vrednosti" vrednosti={nove} />;
  }
  if (akcija === 'DELETE') {
    return <VrednostiTabela naslov="Obrisane vrednosti" vrednosti={stare} />;
  }

  // UPDATE — unija ključeva, poredi formatirane vrednosti.
  const kljucevi = new Set<string>([...Object.keys(stare ?? {}), ...Object.keys(nove ?? {})]);
  const promene = [...kljucevi]
    .map((k) => ({
      polje: k,
      staro: formatirajVrednost(stare?.[k]),
      novo: formatirajVrednost(nove?.[k]),
    }))
    .filter((p) => p.staro !== p.novo);

  if (promene.length === 0) {
    return (
      <p className="text-sm italic text-gray-500">
        Nema prikazanih izmena (izmenjena su samo zaštićena polja).
      </p>
    );
  }

  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b border-gray-200 text-left text-xs uppercase tracking-wide text-gray-500">
          <th className="py-1.5 pr-3 font-medium">Polje</th>
          <th className="py-1.5 pr-3 font-medium">Staro</th>
          <th className="py-1.5 font-medium">Novo</th>
        </tr>
      </thead>
      <tbody className="divide-y divide-gray-100">
        {promene.map((p) => (
          <tr key={p.polje}>
            <td className="py-1.5 pr-3 align-top font-medium text-gray-800">
              {poljeLabela(p.polje)}
            </td>
            {/* break-all: dugi JSON tokeni bez razmaka ne smeju da razvuku modal. */}
            <td className="break-all py-1.5 pr-3 align-top text-red-700 line-through">{p.staro}</td>
            <td className="break-all py-1.5 align-top text-primary-700">{p.novo}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

/** Prosta tabela ključ → vrednost (CREATE/DELETE/START, bez poređenja). */
function VrednostiTabela({ naslov, vrednosti }: { naslov: string; vrednosti: Vrednosti | null }) {
  const parovi = Object.entries(vrednosti ?? {});
  if (parovi.length === 0) {
    return <p className="text-sm italic text-gray-500">Nema podataka za prikaz.</p>;
  }
  return (
    <div className="space-y-1">
      <p className="text-xs uppercase tracking-wide text-gray-500">{naslov}</p>
      <table className="w-full text-sm">
        <tbody className="divide-y divide-gray-100">
          {parovi.map(([k, v]) => (
            <tr key={k}>
              <td className="py-1.5 pr-3 align-top font-medium text-gray-800">{poljeLabela(k)}</td>
              <td className="break-all py-1.5 align-top text-gray-700">{formatirajVrednost(v)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
