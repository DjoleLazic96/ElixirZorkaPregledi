import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Badge } from '../../components/ui/Badge';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { PageHeader } from '../../components/ui/PageHeader';
import { Ucitavanje } from '../../components/ui/Spinner';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { formatirajDatum, relativnoVreme } from '../../lib/datum';
import { formatirajProcenat } from '../../lib/broj';
import { HITNOSTI, hitnostLabela } from '../akcije/oznake';
import { statusLabela, tonStatusa } from '../pregledi/hooks';
import { useDashboard, type OdlukaDto, type PregledKratkoDto } from './hooks';
import { Plocica } from './Prikazi';

/** Moj današnji ili započet pregled vodi na popunjavanje, ostali na detalj. */
const linkPregleda = (p: PregledKratkoDto) =>
  p.moj && ['planiran', 'u_radu', 'vracen_na_doradu'].includes(p.status) ? `/pregled/${p.id}/popuni` : `/pregledi/${p.id}`;

function ListaPregleda({ pregledi, prazno, prebacivanje = false }: { pregledi: PregledKratkoDto[]; prazno: string; prebacivanje?: boolean }) {
  if (pregledi.length === 0) return <p className="text-sm text-gray-500">{prazno}</p>;
  return (
    <ul className="divide-y divide-gray-100 text-sm">
      {pregledi.map((p) => (
        <li key={p.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
          <span className="min-w-0">
            <Link className="font-medium text-primary-700 hover:underline" to={linkPregleda(p)}>{p.broj}</Link>
            <span className="text-gray-600"> · {formatirajDatum(p.datum)} · {p.inzenjer}{p.overio && ` · overio ${p.overio}`}</span>
          </span>
          <span className="flex items-center gap-2">
            {prebacivanje && <Link className="text-xs text-primary-700 hover:underline" to="/kalendar">Zatraži prebacivanje</Link>}
            <Badge tone={tonStatusa(p.status)}>{statusLabela(p.status)}</Badge>
          </span>
        </li>
      ))}
    </ul>
  );
}

function ListaOdluka({ odluke, prazno }: { odluke: OdlukaDto[]; prazno: string }) {
  if (odluke.length === 0) return <p className="text-sm text-gray-500">{prazno}</p>;
  return (
    <ul className="divide-y divide-gray-100 text-sm">
      {odluke.map((o, i) => (
        <li key={i} className="flex flex-wrap items-center justify-between gap-2 py-2">
          <Link className="min-w-0 break-words text-primary-700 hover:underline" to={o.link}>{o.opis}</Link>
          <span className="flex items-center gap-2 text-xs text-gray-500">
            {o.rokIstekao && <Badge tone="red">rok istekao</Badge>}
            {o.od && relativnoVreme(o.od)}
          </span>
        </li>
      ))}
    </ul>
  );
}

function Kartica({ naslov, children, className }: { naslov: string; children: ReactNode; className?: string }) {
  return <Card className={className}><CardHeader><CardTitle>{naslov}</CardTitle></CardHeader><CardBody>{children}</CardBody></Card>;
}

/**
 * Početna (R8.10–R8.12): svi vide sve, uz oznake kome je dodeljeno i ko je overio. Ko popunjava ima „Moje aktivnosti“ na vrhu,
 * Supervizor i Direktor „Čeka moju odluku“.
 */
export function DashboardPage() {
  const d = useDashboard();
  if (d.isLoading) return <Ucitavanje />;
  if (!d.data) return <p role="alert" className="text-sm text-red-600">{porukaGreske(d.error, 'Početna nije učitana.')}</p>;
  const { moje, cekaOdluku, kpi } = d.data;

  return (
    <div className="space-y-4">
      <PageHeader title="Početna" />

      {moje && (
        <Card className="border-primary-200">
          <CardHeader><CardTitle>Moje aktivnosti</CardTitle></CardHeader>
          <CardBody className="grid gap-4 lg:grid-cols-2">
            <div><h3 className="mb-1 text-sm font-medium">Danas</h3><ListaPregleda pregledi={moje.danas} prazno="Danas nemate pregled." /></div>
            <div><h3 className="mb-1 text-sm font-medium">Započeti, a nepredati</h3><ListaPregleda pregledi={moje.zapoceti} prazno="Nema započetih pregleda." /></div>
            <div><h3 className="mb-1 text-sm font-medium">Narednih 7 dana</h3><ListaPregleda pregledi={moje.narednih7} prazno="Nema planiranih pregleda." prebacivanje /></div>
            <div><h3 className="mb-1 text-sm font-medium">Moji zahtevi na čekanju</h3><ListaOdluka odluke={moje.zahtevi} prazno="Nema zahteva na čekanju." /></div>
            {moje.akcije.length > 0 && (
              <div className="lg:col-span-2">
                <h3 className="mb-1 text-sm font-medium">Akcije na mojim uređajima</h3>
                <ul className="divide-y divide-gray-100 text-sm">
                  {moje.akcije.map((a) => (
                    <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                      <span className="min-w-0 break-words"><Link className="font-medium text-primary-700 hover:underline" to={`/akcije/${a.id}`}>{a.broj}</Link> · <Link className="hover:underline" to={`/uredjaji/${a.uredjajId}`}>{a.uredjaj}</Link> · {a.aktivnost}</span>
                      <Badge tone={a.hitnost === 'hitno' ? 'red' : 'gray'}>{hitnostLabela(a.hitnost)}</Badge>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </CardBody>
        </Card>
      )}

      {cekaOdluku && (
        <Kartica naslov={`Čeka moju odluku (${cekaOdluku.length})`} className="border-amber-200">
          <ListaOdluka odluke={cekaOdluku} prazno="Ništa ne čeka vašu odluku." />
        </Kartica>
      )}

      <div className="grid gap-2 sm:grid-cols-3 lg:grid-cols-6">
        <Plocica naslov="Plan ovog meseca" vrednost={formatirajProcenat(kpi.procenatNaDan)} opis="na planirani dan" />
        <Plocica naslov="Overeni u roku" vrednost={formatirajProcenat(kpi.procenatURoku)} opis="ovog meseca" />
        <Plocica naslov="Čeka overu" vrednost={String(kpi.cekaOveru)} />
        <Plocica naslov="Otvorene akcije" vrednost={String(kpi.otvorenihAkcija)} />
        <Plocica naslov="Otvoreni kvarovi" vrednost={String(kpi.otvorenihKvarova)} />
        <Plocica naslov="Nije u funkciji" vrednost={String(kpi.vanFunkcije)} />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Kartica naslov="Danas"><ListaPregleda pregledi={d.data.danas} prazno="Danas nema pregleda." /></Kartica>
        <Kartica naslov="Sutra"><ListaPregleda pregledi={d.data.sutra} prazno="Sutra nema pregleda." /></Kartica>
        <Kartica naslov="Čekaju overu"><ListaPregleda pregledi={d.data.cekajuOveru} prazno="Nijedan pregled ne čeka overu." /></Kartica>
        <Kartica naslov="Otvorene akcije">
          <div className="mb-2 flex flex-wrap gap-2">
            {HITNOSTI.map((h) => (
              <Badge key={h.value} tone={h.value === 'hitno' ? 'red' : h.value === '7_dana' ? 'amber' : 'gray'}>{h.label}: {d.data.akcijePoHitnosti[h.value] ?? 0}</Badge>
            ))}
          </div>
          {d.data.akcije.length === 0 ? <p className="text-sm text-gray-500">Nema otvorenih akcija.</p> : (
            <ul className="divide-y divide-gray-100 text-sm">
              {d.data.akcije.map((a) => (
                <li key={a.id} className="py-2">
                  <Link className="font-medium text-primary-700 hover:underline" to={`/akcije/${a.id}`}>{a.broj}</Link>
                  <span className="text-gray-600"> · <Link className="hover:underline" to={`/uredjaji/${a.uredjajId}`}>{a.uredjaj}</Link> · {a.aktivnost} · prijavio {a.prijavio}</span>
                  {a.rok && <span className={cn('text-xs', a.rokIstekao ? 'font-semibold text-red-700' : 'text-gray-500')}> · rok {formatirajDatum(a.rok)}</span>}
                </li>
              ))}
            </ul>
          )}
          <Link className="mt-2 inline-block text-sm text-primary-700 hover:underline" to="/akcije">Sve akcije</Link>
        </Kartica>
        <Kartica naslov="Otvoreni kvarovi">
          {d.data.kvarovi.length === 0 ? <p className="text-sm text-gray-500">Nema otvorenih kvarova.</p> : (
            <ul className="divide-y divide-gray-100 text-sm">
              {d.data.kvarovi.map((k) => (
                <li key={k.id} className="py-2">
                  <Link className="font-medium text-primary-700 hover:underline" to={`/neispravnosti/${k.id}`}>{k.broj}</Link>
                  <span className="text-gray-600"> · <Link className="hover:underline" to={`/uredjaji/${k.uredjajId}`}>{k.uredjaj}</Link> · {k.aktivnost} · od {formatirajDatum(k.otvoren)}</span>
                </li>
              ))}
            </ul>
          )}
          <Link className="mt-2 inline-block text-sm text-primary-700 hover:underline" to="/neispravnosti">Sve neispravnosti</Link>
        </Kartica>
        <Kartica naslov="Uređaji van funkcije">
          {d.data.vanFunkcije.length === 0 ? <p className="text-sm text-gray-500">Svi uređaji su u funkciji.</p> : (
            <ul className="divide-y divide-gray-100 text-sm">
              {d.data.vanFunkcije.map((v) => (
                <li key={v.uredjajId} className="flex justify-between gap-2 py-2">
                  <Link className="min-w-0 break-words text-primary-700 hover:underline" to={`/uredjaji/${v.uredjajId}`}>{v.uredjaj}</Link>
                  <span className="whitespace-nowrap text-xs text-gray-600">od {formatirajDatum(v.od)} ({v.dana} d)</span>
                </li>
              ))}
            </ul>
          )}
        </Kartica>
      </div>
    </div>
  );
}
