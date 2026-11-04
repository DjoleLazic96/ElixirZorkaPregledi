import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, QrCode } from 'lucide-react';
import type { Smer } from '../../api/types';
import { IzvozDugme } from '../../components/IzvozDugme';
import { Slike } from '../../components/Slike';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { Select } from '../../components/ui/Select';
import { SortableTh } from '../../components/ui/SortableTh';
import { Ucitavanje } from '../../components/ui/Spinner';
import { Tabs } from '../../components/ui/Tabs';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { formatirajDatum } from '../../lib/datum';
import { mnozina } from '../../lib/broj';
import { hitnostLabela } from '../akcije/oznake';
import { statusLabela, tonStatusa } from '../pregledi/hooks';
import { useUredjajEkran, type PregledUredjajaDto, type UredjajEkranDto } from './hooks';
import { STANJA, STATUS_UREDJAJA } from './oznake';
import { Plocica, QrDijalog, VremenskaLinija } from './Prikazi';

const dani = (n: number) => `${n} ${mnozina(n, { jednina: 'dan', paukal: 'dana', mnozina: 'dana' })}`;

/**
 * Ekran uređaja (R8.4–R8.6): status, poslednji pregled, istorija u bojama sa klizačem perioda (30–365 dana), analitika,
 * „Pregledi uređaja“ (Svi pregledi / Dosije), kvarovi i QR nalepnica. Klik na pregled otvara pregled sa ovim uređajem na vrhu.
 */
export function UredjajEkranPage() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const [klizac, setKlizac] = useState(90);
  const [dana, setDana] = useState(90);
  const [qr, setQr] = useState(false);
  const ekran = useUredjajEkran(id, dana);

  if (ekran.isLoading) return <Ucitavanje />;
  if (!ekran.data) return <p role="alert" className="text-sm text-red-600">{porukaGreske(ekran.error, 'Uređaj nije učitan.')}</p>;
  const e = ekran.data;
  const st = STATUS_UREDJAJA[e.status];
  const a = e.analitika;
  const primeni = () => setDana(klizac);

  return (
    <div className="space-y-4">
      <Button variant="ghost" onClick={() => navigate(-1)}><ArrowLeft size={14} aria-hidden /> Nazad</Button>
      <Card>
        <CardBody className="space-y-2 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="break-words text-xl font-semibold">{e.prikaz}</h1>
              <Badge tone={st.ton}>{st.labela}{e.status === 'nije_pregledan' && e.danaOdPregleda !== null && ` ${dani(e.danaOdPregleda)}`}</Badge>
              {e.statusRegistra !== 'aktivan' && <Badge tone="gray">van upotrebe</Badge>}
            </div>
            <Button variant="secondary" onClick={() => setQr(true)}><QrCode size={14} aria-hidden /> QR nalepnica</Button>
          </div>
          <p className="text-gray-600">Sekcija {e.sekcija} · {e.vrsta || 'bez vrste'} · šabloni: {e.sabloni.length ? e.sabloni.join(', ') : 'nije u objavljenom šablonu'}</p>
          <p className="text-gray-600">
            Poslednji pregled:{' '}
            {e.poslednji ? (
              <>
                <Link className="text-primary-700 hover:underline" to={`/pregledi/${e.poslednji.pregledId}?uredjaj=${e.id}`}>{e.poslednji.broj}</Link>
                {' '}({formatirajDatum(e.poslednji.datum)}, {e.poslednji.inzenjer}) — {STANJA[e.poslednji.stanje].labela.toLowerCase()}
              </>
            ) : 'nije bilo predatih pregleda'}
          </p>
        </CardBody>
      </Card>

      <Card>
        <CardHeader className="flex flex-wrap items-center justify-between gap-3">
          <CardTitle>Istorija</CardTitle>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            Period
            <input type="range" min={30} max={365} step={5} value={klizac} aria-label="Period istorije u danima"
              onChange={(ev) => setKlizac(Number(ev.target.value))} onPointerUp={primeni} onKeyUp={primeni} onBlur={primeni} className="w-40 accent-primary-600" />
            <span className="w-16 tabular-nums">{dani(klizac)}</span>
          </label>
        </CardHeader>
        <CardBody className={cn('space-y-4', ekran.isFetching && 'opacity-60')}>
          <VremenskaLinija dani={e.istorija} />
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            <Plocica naslov="Dana bez kvara" vrednost={String(a.danaBezKvaraSada)} opis={`najduže ${dani(a.najduzeBezKvara)}`} />
            <Plocica naslov="Od poslednjeg kvara" vrednost={a.danaOdPoslednjegKvara === null ? '—' : dani(a.danaOdPoslednjegKvara)} />
            <Plocica naslov="Kvarova u periodu" vrednost={String(a.brojKvarova)} opis={a.trajanja.length ? `trajanje: ${a.trajanja.join(', ')} d` : undefined} />
            <Plocica naslov="Prosečno trajanje kvara" vrednost={a.brojKvarova ? dani(Math.round(a.prosekTrajanja)) : '—'} opis={e.najcescaStavka ? `najčešće: ${e.najcescaStavka}` : undefined} />
          </div>
        </CardBody>
      </Card>

      <PreglediUredjaja e={e} />

      <Card>
        <CardHeader><CardTitle>Neispravnosti ({e.kvarovi.length})</CardTitle></CardHeader>
        <CardBody>
          {e.kvarovi.length === 0 ? <p className="text-sm text-gray-500">Uređaj nije imao kvarove.</p> : (
            <ul className="divide-y divide-gray-100 text-sm">
              {e.kvarovi.map((k) => (
                <li key={k.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                  <span className="min-w-0 break-words"><Link className="font-medium text-primary-700 hover:underline" to={`/neispravnosti/${k.id}`}>{k.broj}</Link> · {k.aktivnost}</span>
                  <span className="text-xs text-gray-600">{formatirajDatum(k.otvoren)} – {k.zatvoren ? formatirajDatum(k.zatvoren) : <Badge tone="red">otvoren</Badge>} · {dani(k.trajanjeDana)}</span>
                </li>
              ))}
            </ul>
          )}
        </CardBody>
      </Card>

      {qr && <QrDijalog token={e.qrToken} naziv={e.prikaz} onClose={() => setQr(false)} />}
    </div>
  );
}

type Kolona = 'datum' | 'broj' | 'tip' | 'inzenjer';

/** Kartica „Pregledi uređaja“ (R8.5): Svi pregledi (sortiranje, filteri, izvoz) i Dosije (napomene, slike, akcije). */
function PreglediUredjaja({ e }: { e: UredjajEkranDto }) {
  const [tab, setTab] = useState<'svi' | 'dosije'>('svi');
  const [sort, setSort] = useState<{ k: Kolona; s: Smer }>({ k: 'datum', s: 'desc' });
  const [stanje, setStanje] = useState<string | null>(null);
  const [tip, setTip] = useState<string | null>(null);
  const lista = useMemo(() => {
    const f = e.pregledi.filter((p) => (!stanje || p.stanje === stanje) && (!tip || p.tip === tip));
    const kljuc = (p: PregledUredjajaDto) => (sort.k === 'datum' ? p.datum : sort.k === 'broj' ? p.broj : sort.k === 'tip' ? p.tip : p.inzenjer);
    return [...f].sort((x, y) => kljuc(x).localeCompare(kljuc(y)) * (sort.s === 'asc' ? 1 : -1));
  }, [e.pregledi, stanje, tip, sort]);
  const tipovi = [...new Set(e.pregledi.map((p) => p.tip))].sort();
  const th = (k: Kolona, t: string) => <SortableTh column={k} current={sort.k} direction={sort.s} onSort={(c, s) => setSort({ k: c, s })}>{t}</SortableTh>;

  return (
    <Card>
      <CardHeader className="flex-col items-stretch gap-2">
        <CardTitle>Pregledi uređaja</CardTitle>
        <Tabs aria-label="Pregledi uređaja" value={tab} onChange={setTab} items={[{ key: 'svi', label: `Svi pregledi (${e.pregledi.length})` }, { key: 'dosije', label: 'Dosije' }]} />
      </CardHeader>
      <CardBody>
        {tab === 'svi' ? (
          <div className="space-y-3">
            <div className="grid gap-2 sm:grid-cols-3">
              <Select aria-label="Stanje uređaja" value={stanje} nullable placeholder="Sva stanja" onChange={setStanje}
                options={Object.entries(STANJA).map(([value, s]) => ({ value, label: s.labela }))} />
              <Select aria-label="Tip pregleda" value={tip} nullable placeholder="Svi tipovi" onChange={setTip} options={tipovi.map((t) => ({ value: t, label: t }))} />
              <div className="sm:justify-self-end"><IzvozDugme url={`/uredjaji/${e.id}/pregledi.xlsx`} ime={`Pregledi ${e.prikaz}.xlsx`} /></div>
            </div>
            {lista.length === 0 ? <p className="text-sm text-gray-500">Nema pregleda.</p> : (
              <>
              {/* Na telefonu kartice (§8.1 t. 4), redosled je po datumu. */}
              <ul className="divide-y divide-gray-100 text-sm md:hidden">
                {lista.map((p) => (
                  <li key={p.pregledId} className="space-y-1 py-2">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <Link className="whitespace-nowrap font-medium text-primary-700 hover:underline" to={`/pregledi/${p.pregledId}?uredjaj=${e.id}`}>{p.broj}</Link>
                      <Badge tone={tonStatusa(p.status)}>{statusLabela(p.status)}</Badge>
                    </div>
                    <p className="text-gray-600">{formatirajDatum(p.datum)} · {p.inzenjer}</p>
                    <p className="inline-flex items-center gap-1.5"><span className={`inline-block h-2.5 w-2.5 rounded-sm ${STANJA[p.stanje].boja}`} aria-hidden />{STANJA[p.stanje].labela}{p.neispravnih > 1 && ` (${p.neispravnih})`}</p>
                  </li>
                ))}
              </ul>
              <div className="hidden overflow-x-auto md:block">
                <table className="w-full text-sm">
                  <thead className="text-xs text-gray-500"><tr>{th('broj', 'Broj')}{th('datum', 'Datum')}{th('tip', 'Tip')}{th('inzenjer', 'Inženjer')}<th className="px-3 py-2 text-left">Stanje uređaja</th><th className="px-3 py-2 text-left">Status</th></tr></thead>
                  <tbody className="divide-y divide-gray-100">
                    {lista.map((p) => (
                      <tr key={p.pregledId}>
                        <td className="whitespace-nowrap px-3 py-2"><Link className="text-primary-700 hover:underline" to={`/pregledi/${p.pregledId}?uredjaj=${e.id}`}>{p.broj}</Link></td>
                        <td className="whitespace-nowrap px-3 py-2">{formatirajDatum(p.datum)}</td>
                        <td className="px-3 py-2">{p.tip}</td>
                        <td className="px-3 py-2">{p.inzenjer}</td>
                        <td className="whitespace-nowrap px-3 py-2">
                          <span className="inline-flex items-center gap-1.5"><span className={`inline-block h-2.5 w-2.5 rounded-sm ${STANJA[p.stanje].boja}`} aria-hidden />{STANJA[p.stanje].labela}{p.neispravnih > 1 && ` (${p.neispravnih})`}</span>
                        </td>
                        <td className="px-3 py-2"><Badge tone={tonStatusa(p.status)}>{statusLabela(p.status)}</Badge></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              </>
            )}
          </div>
        ) : (
          <div className="space-y-4 text-sm">
            {e.dosije.length === 0 && <p className="text-gray-500">Nema napomena ni slika.</p>}
            <ul className="space-y-2">
              {e.dosije.map((d) => (
                <li key={`${d.pregledId}-${d.aktivnost}`} className={cn('rounded-md border p-2', d.neispravan ? 'border-red-200 bg-red-50' : 'border-gray-200')}>
                  <p className="text-xs text-gray-600">
                    <Link className="text-primary-700 hover:underline" to={`/pregledi/${d.pregledId}?uredjaj=${e.id}`}>{d.broj}</Link> · {formatirajDatum(d.datum)} · {d.aktivnost}
                  </p>
                  {d.napomena && <p className="mt-1 break-words">{d.napomena}</p>}
                  <Slike ids={d.fotografije} />
                </li>
              ))}
            </ul>
            {e.akcije.length > 0 && (
              <div>
                <h3 className="mb-1 font-medium">Akcije</h3>
                <ul className="divide-y divide-gray-100">
                  {e.akcije.map((x) => (
                    <li key={x.id} className="flex flex-wrap items-center justify-between gap-2 py-1.5">
                      <span className="min-w-0 break-words"><Link className="text-primary-700 hover:underline" to={`/akcije/${x.id}`}>{x.broj}</Link> · {x.aktivnost}{x.opis && ` — ${x.opis}`}</span>
                      <span className="flex gap-1"><Badge tone={x.status === 'otvorena' ? 'amber' : 'gray'}>{x.status}</Badge>{x.status === 'otvorena' && <Badge>{hitnostLabela(x.hitnost)}</Badge>}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )}
      </CardBody>
    </Card>
  );
}
