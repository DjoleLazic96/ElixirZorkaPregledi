import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Slike } from '../../components/Slike';
import { Badge } from '../../components/ui/Badge';
import { cn } from '../../lib/cn';
import { hitnostLabela, vrstaLabela } from '../akcije/oznake';
import type { PregledDetaljDto } from './hooks';

type Akcija = NonNullable<PregledDetaljDto['akcije']>[number];
type Uredjaj = PregledDetaljDto['sekcije'][number]['uredjaji'][number];

const STATUS_PREDLOGA: Record<string, string> = { zahtev: 'čeka odluku', odobren: 'odobren', odbijen: 'odbijen', duplikat: 'duplikat' };

/** „A-2026-0001 · Zameniti · Hitno — opis“ plavom bojom (zamerka 48). */
function AkcijaRed({ a, saStavkom = false }: { a: Akcija; saStavkom?: boolean }) {
  return (
    <p className="break-words text-sm text-blue-800">
      <Link to={`/akcije/${a.id}`} className="font-semibold hover:underline">{a.broj}</Link>
      {' · '}{vrstaLabela(a.vrsta)} · {hitnostLabela(a.hitnost)}{a.opis && ` — ${a.opis}`}
      {saStavkom && <span className="text-blue-700"> ({a.uredjaj} · {a.aktivnost})</span>}
    </p>
  );
}

function Odeljak({ naslov, children }: { naslov: string; children: React.ReactNode }) {
  return (
    <section className="space-y-1.5">
      <h3 className="text-sm font-semibold text-gray-800">{naslov}</h3>
      {children}
    </section>
  );
}

/**
 * Rezime pregleda (zamerka 48) — ono što kontrolor treba da vidi: neispravne stavke (sa napomenom, poljima, slikama i akcijom),
 * uređaji van funkcije, akcije otvorene na pregledu, odgovori na ranije akcije, napomene uz ispravne stavke, upotreba
 * „Sve ispravno“ i predlozi izmene šablona poslati sa ovog pregleda. Prazni odeljci se ne prikazuju.
 */
export function RezimePregleda({ p }: { p: PregledDetaljDto }) {
  const akcije = p.akcije ?? [];
  const poStavci = (id: number) => akcije.filter((a) => a.stavkaId === id);
  const svi = p.sekcije.flatMap((s) => s.uredjaji.map((u) => ({ s: s.sifra, u })));
  const problemi = svi.flatMap(({ s, u }) => (u.vanFunkcije ? [] : u.stavke.filter((x) => x.neispravan).map((x) => ({ s, u, x }))));
  const napomene = svi.flatMap(({ s, u }) => (u.vanFunkcije ? [] : u.stavke.filter((x) => !x.neispravan && x.napomena).map((x) => ({ s, u, x }))));
  const van = svi.filter(({ u }) => u.vanFunkcije);
  const sveIspravno = p.istorija.filter((d) => d.vrsta === 'sve_ispravno');

  return (
    <div className="space-y-4">
      <Odeljak naslov={`Neispravne stavke (${problemi.length})`}>
        {problemi.length === 0 && <p className="text-sm text-gray-500">Nema neispravnih stavki.</p>}
        {problemi.map(({ s, u, x }) => (
          <div key={x.stavkaId} className="space-y-1 rounded-md border border-red-200 bg-red-50 p-2 text-sm">
            <p className="break-words font-medium text-red-900">Sekcija {s} · {u.naziv} · {x.aktivnost}: {x.odgovor}</p>
            {(x.napomena || x.polja.length > 0) && <p className="break-words text-red-800">{[x.napomena, ...x.polja].filter(Boolean).join(' · ')}</p>}
            {poStavci(x.stavkaId).map((a) => <AkcijaRed key={a.id} a={a} />)}
            <Slike ids={x.fotografije} />
          </div>
        ))}
      </Odeljak>
      {van.length > 0 && (
        <Odeljak naslov="Nije u funkciji">
          <p className="break-words text-sm text-amber-800">{van.map(({ u }) => u.naziv).join(', ')}</p>
        </Odeljak>
      )}
      {akcije.length > 0 && (
        <Odeljak naslov={`Akcije otvorene na ovom pregledu (${akcije.length})`}>
          {akcije.map((a) => <AkcijaRed key={a.id} a={a} saStavkom />)}
        </Odeljak>
      )}
      {(p.odgovoriNaAkcije ?? []).length > 0 && (
        <Odeljak naslov="Odgovori na ranije akcije">
          {p.odgovoriNaAkcije!.map((o, i) => (
            <p key={i} className="break-words text-sm">
              <b>{o.broj}</b> ({o.uredjaj} · {o.aktivnost}): {o.uradjena ? 'urađena' : <span className="text-amber-800">nije urađena, ostaje otvorena</span>}
            </p>
          ))}
        </Odeljak>
      )}
      {napomene.length > 0 && (
        <Odeljak naslov="Napomene uz ispravne stavke">
          {napomene.map(({ s, u, x }) => (
            <p key={x.stavkaId} className="break-words text-sm">Sekcija {s} · {u.naziv} · {x.aktivnost}: <span className="text-gray-700">{x.napomena}</span></p>
          ))}
        </Odeljak>
      )}
      {sveIspravno.length > 0 && (
        <Odeljak naslov="„Sve ispravno“">
          {sveIspravno.map((d, i) => <p key={i} className="break-words text-sm text-gray-700">{d.opis}</p>)}
        </Odeljak>
      )}
      {(p.predlozi ?? []).length > 0 && (
        <Odeljak naslov="Predlozi izmene šablona sa ovog pregleda">
          {p.predlozi!.map((x) => (
            <p key={x.id} className="break-words text-sm">
              <Link to={`/sabloni/predlozi?predlog=${x.id}`} className="text-primary-700 hover:underline">{x.opis}</Link>
              <span className="text-gray-500"> · {STATUS_PREDLOGA[x.status] ?? x.status}</span>
            </p>
          ))}
        </Odeljak>
      )}
    </div>
  );
}

/** Uređaj sa svim stavkama: neispravno crveno, akcija plavo ispod stavke, van funkcije narandžasto (zamerka 48). */
export function UredjajStavke({ u, akcije = [] }: { u: Uredjaj; akcije?: Akcija[] }) {
  return (
    <div>
      <p className="break-words font-medium">{u.naziv}{u.vanFunkcije && <Badge tone="amber" className="ml-2">nije u funkciji</Badge>}</p>
      <ul className="mt-1 space-y-1 text-sm">
        {u.stavke.map((x) => (
          <li key={x.stavkaId} className="space-y-0.5">
            <div className="flex flex-wrap justify-between gap-x-3">
              <span className={cn('min-w-0 break-words', x.neispravan && 'text-red-800')}>
                {x.aktivnost}{x.napomena && <span className={x.neispravan ? 'text-red-700' : 'text-gray-500'}> — {x.napomena}</span>}
              </span>
              <span className={cn('whitespace-nowrap', u.vanFunkcije ? 'text-amber-700' : x.neispravan ? 'font-semibold text-red-700' : 'text-gray-600')}>
                {u.vanFunkcije ? 'nije u funkciji' : x.odgovor ?? 'bez odgovora'}
              </span>
            </div>
            {x.polja.length > 0 && <p className={cn('break-words text-xs', x.neispravan ? 'text-red-700' : 'text-gray-500')}>{x.polja.join(' · ')}</p>}
            {akcije.filter((a) => a.stavkaId === x.stavkaId).map((a) => <div key={a.id} className="pl-3"><AkcijaRed a={a} /></div>)}
            {x.fotografije.length > 0 && <Slike ids={x.fotografije} />}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Ceo pregled (zamerka 48): sekcije kao red dugmadi na vrhu (kao na popunjavanju) — prikazuje se izabrana sekcija umesto
 * duge liste; sekcija sa neispravnom stavkom je označena crveno.
 */
export function CeoPregled({ p }: { p: PregledDetaljDto }) {
  const imaProblem = (s: PregledDetaljDto['sekcije'][number]) => s.uredjaji.some((u) => !u.vanFunkcije && u.stavke.some((x) => x.neispravan));
  const [izabrana, setIzabrana] = useState(() => (p.sekcije.find(imaProblem) ?? p.sekcije[0])?.sifra ?? null);
  const sek = p.sekcije.find((s) => s.sifra === izabrana);
  if (p.sekcije.length === 0) return <p className="text-sm text-gray-500">Pregled još nije započet.</p>;
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-1.5" role="group" aria-label="Sekcije">
        {p.sekcije.map((s) => (
          <button key={s.sifra} type="button" aria-pressed={s.sifra === izabrana} onClick={() => setIzabrana(s.sifra)}
            className={cn('min-h-9 rounded-lg border px-3 text-sm font-medium',
              s.sifra === izabrana ? (imaProblem(s) ? 'border-red-600 bg-red-600 text-white' : 'border-primary-500 bg-primary-500 text-white')
                : imaProblem(s) ? 'border-red-300 bg-red-50 text-red-800' : 'border-gray-200 bg-white text-gray-800 hover:bg-gray-50')}>
            Sekcija {s.sifra}
          </button>
        ))}
      </div>
      {sek && <div className="space-y-3">{sek.uredjaji.map((u) => <UredjajStavke key={u.uredjajId} u={u} akcije={p.akcije ?? []} />)}</div>}
    </div>
  );
}
