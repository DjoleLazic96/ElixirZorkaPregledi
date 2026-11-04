import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, ListChecks, ShieldAlert } from 'lucide-react';
import { useToast } from '../../app/providers/ToastProvider';
import { IzvozDugme } from '../../components/IzvozDugme';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { Checkbox } from '../../components/ui/Checkbox';
import { EmptyState } from '../../components/ui/EmptyState';
import { Input } from '../../components/ui/Input';
import { Modal } from '../../components/ui/Modal';
import { PageHeader } from '../../components/ui/PageHeader';
import { Select } from '../../components/ui/Select';
import { Ucitavanje } from '../../components/ui/Spinner';
import { Textarea } from '../../components/ui/Textarea';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { formatirajDatum, formatirajVreme } from '../../lib/datum';
import { imaPravo } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import { useSekcije } from '../sabloni/hooks';
import {
  parametriAkcija, parametriKvarova, useAkcija, useAkcije, useKvar, useKvarovi, useOdlukaAkcije,
  type AkcijaDto, type AkcijeFilteri, type KvaroviFilteri,
} from './hooks';
import { HITNOSTI, ZADUZENI_PREDLOZI, hitnostLabela, vrstaLabela } from './oznake';

const tonHitnosti = (h: string) => (h === 'hitno' ? 'red' : h === '7_dana' ? 'amber' : 'gray') as 'red' | 'amber' | 'gray';

/** Kartica akcije za listu (R7.3, R7.6): broj, pregled, uređaj, hitnost, rok (crveno kad je istekao, R7.8), kvar na stavci. */
function AkcijaRed({ a }: { a: AkcijaDto }) {
  return (
    <li className={cn('rounded-md border bg-white p-3', a.rokIstekao ? 'border-red-300' : 'border-gray-200')}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Link to={`/akcije/${a.id}`} className="whitespace-nowrap font-semibold text-primary-700 hover:underline">{a.broj}</Link>
        <span className="flex flex-wrap gap-1">
          <Badge tone={tonHitnosti(a.hitnost)}>{hitnostLabela(a.hitnost)}</Badge>
          {a.status !== 'otvorena' && <Badge tone="gray">{a.status}</Badge>}
          {a.stavkaSadaIspravna && <Badge tone="primary">stavka sada ispravna</Badge>}
        </span>
      </div>
      <p className="mt-1 break-words text-sm"><Link to={`/uredjaji/${a.uredjajId}`} className="text-primary-700 hover:underline">{a.uredjaj}</Link> · {a.aktivnost} · {vrstaLabela(a.vrsta)}{a.opis && ` — ${a.opis}`}</p>
      <p className="mt-1 text-xs text-gray-600">
        Pregled <Link to={`/pregledi/${a.pregledId}`} className="whitespace-nowrap text-primary-700 hover:underline">{a.pregled}</Link>
        {a.rok && <> · rok <span className={cn(a.rokIstekao && 'font-semibold text-red-700')}>{formatirajDatum(a.rok)}</span></>}
        {a.zaduzen && ` · zadužen: ${a.zaduzen}`}
        {a.kvarBroj && ` · kvar ${a.kvarBroj} ${a.kvarOtvoren ? 'otvoren' : 'zatvoren'}`}
      </p>
    </li>
  );
}

/** „Potrebne akcije“ (R7.3): otvorene akcije sa filterima po hitnosti, sekciji i „stavka sada ispravna“ (R7.15). */
export function AkcijePage() {
  const [f, setF] = useState<AkcijeFilteri>({ status: 'otvorena', hitnost: null, sekcijaId: null, sadaIspravna: false });
  const akcije = useAkcije(f);
  const sekcije = useSekcije();
  return (
    <div className="space-y-4">
      <PageHeader title="Potrebne akcije" actions={<IzvozDugme url="/izvoz/akcije.xlsx" params={parametriAkcija(f)} ime="Akcije.xlsx" />} />
      <Card>
        <CardBody className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Select aria-label="Status" value={f.status} nullable placeholder="Svi statusi" onChange={(v) => setF({ ...f, status: v })}
            options={[{ value: 'otvorena', label: 'Otvorene' }, { value: 'zatvorena', label: 'Zatvorene' }, { value: 'otkazana', label: 'Otkazane' }]} />
          <Select aria-label="Hitnost" value={f.hitnost} nullable placeholder="Sve hitnosti" options={HITNOSTI} onChange={(v) => setF({ ...f, hitnost: v })} />
          <Select aria-label="Sekcija" value={f.sekcijaId} nullable placeholder="Sve sekcije" onChange={(v) => setF({ ...f, sekcijaId: v })}
            options={(sekcije.data ?? []).map((s) => ({ value: s.id, label: `Sekcija ${s.sifra}` }))} />
          <Checkbox label="Stavka sada ispravna" checked={f.sadaIspravna} onChange={(e) => setF({ ...f, sadaIspravna: e.target.checked })} />
        </CardBody>
      </Card>
      {akcije.isLoading ? <Ucitavanje /> : !akcije.data?.length ? (
        <Card><CardBody><EmptyState icon={ListChecks} title="Nema akcija za zadate filtere." /></CardBody></Card>
      ) : (
        <ul className="grid gap-2 lg:grid-cols-2">{akcije.data.map((a) => <AkcijaRed key={a.id} a={a} />)}</ul>
      )}
    </div>
  );
}

/** Ekran akcije (R7.6, R7.12): podaci, istorija; Supervizor/Direktor zatvaraju, otkazuju i dodeljuju zaduženog. */
export function AkcijaPage() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const { me } = useAuth();
  const sme = imaPravo(me ?? { roles: [], prava: [] }, 'akcije.zatvaranje');
  const akcija = useAkcija(id);
  const odluka = useOdlukaAkcije(id);
  const toast = useToast();
  const [dijalog, setDijalog] = useState<null | 'zatvori' | 'otkazi'>(null);
  const [razlog, setRazlog] = useState('');
  const [zaduzen, setZaduzen] = useState<string | null>(null);

  if (akcija.isLoading) return <Ucitavanje />;
  if (!akcija.data) return <p role="alert" className="text-sm text-red-600">{porukaGreske(akcija.error, 'Akcija nije učitana.')}</p>;
  const a = akcija.data.akcija;

  async function sacuvaj(v: Parameters<typeof odluka.mutateAsync>[0], uspeh: string) {
    try {
      await odluka.mutateAsync(v);
      toast.success(uspeh);
      setDijalog(null);
      setRazlog('');
    } catch (err) {
      toast.error(porukaGreske(err, 'Promena nije sačuvana.'));
    }
  }

  return (
    <div className="space-y-4">
      <Button variant="ghost" onClick={() => navigate(-1)}><ArrowLeft size={14} aria-hidden /> Nazad</Button>
      <Card>
        <CardBody className="space-y-2 text-sm">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="whitespace-nowrap text-xl font-semibold">{a.broj}</h1>
            <Badge tone={tonHitnosti(a.hitnost)}>{hitnostLabela(a.hitnost)}</Badge>
            <Badge tone={a.status === 'otvorena' ? 'amber' : 'gray'}>{a.status}</Badge>
            {a.stavkaSadaIspravna && <Badge tone="primary">stavka sada ispravna</Badge>}
          </div>
          <p className="break-words"><Link to={`/uredjaji/${a.uredjajId}`} className="text-primary-700 hover:underline">{a.uredjaj}</Link> (sekcija {a.sekcija}) · {a.aktivnost}</p>
          <p>{vrstaLabela(a.vrsta)}{a.opis && ` — ${a.opis}`}</p>
          <p className="text-gray-600">
            Prijavio {a.prijavio} · pregled <Link className="text-primary-700 hover:underline" to={`/pregledi/${a.pregledId}`}>{a.pregled}</Link>
            {a.rok && <> · rok <span className={cn(a.rokIstekao && 'font-semibold text-red-700')}>{formatirajDatum(a.rok)}</span></>}
          </p>
          <p className="text-gray-600">Kvar na stavci: {a.kvarBroj ? `${a.kvarBroj} — ${a.kvarOtvoren ? 'otvoren' : 'zatvoren'}` : 'nema'}</p>
          <p className="text-gray-600">Zadužen: {a.zaduzen ?? '—'}</p>
          {a.razlog && <p className="text-gray-600">Razlog: {a.razlog}</p>}
          {sme && a.status === 'otvorena' && (
            <div className="flex flex-wrap items-center gap-2 pt-2">
              <Select className="w-56" aria-label="Zadužen" value={zaduzen ?? a.zaduzen} nullable placeholder="Bez zaduženog"
                options={[...new Set([...ZADUZENI_PREDLOZI, ...(a.zaduzen ? [a.zaduzen] : [])])].map((z) => ({ value: z, label: z }))}
                onChange={(v) => { setZaduzen(v); void sacuvaj({ putanja: 'zaduzen', zaduzen: v }, 'Zaduženi je sačuvan.'); }} />
              <Button onClick={() => setDijalog('zatvori')}>Zatvori</Button>
              <Button variant="ghost" onClick={() => setDijalog('otkazi')}>Otkaži</Button>
            </div>
          )}
        </CardBody>
      </Card>
      <Card>
        <CardHeader><CardTitle>Istorija</CardTitle></CardHeader>
        <CardBody>
          <ul className="space-y-1 text-sm">
            {akcija.data.istorija.map((d, i) => (
              <li key={i} className="flex flex-wrap gap-x-3"><span className="whitespace-nowrap text-gray-500">{formatirajDatum(d.at)} {formatirajVreme(d.at)}</span><span className="break-words">{d.tekst}{d.korisnik && ` · ${d.korisnik}`}</span></li>
            ))}
          </ul>
        </CardBody>
      </Card>
      {dijalog && (
        <Modal open title={dijalog === 'zatvori' ? 'Zatvori akciju' : 'Otkaži akciju'} onClose={() => setDijalog(null)}>
          <div className="space-y-4">
            <Textarea aria-label="Razlog" placeholder="Razlog" rows={3} value={razlog} onChange={(e) => setRazlog(e.target.value)} />
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setDijalog(null)}>Odustani</Button>
              <Button disabled={!razlog.trim() || odluka.isPending} onClick={() => void sacuvaj({ putanja: dijalog, razlog: razlog.trim() }, dijalog === 'zatvori' ? 'Akcija je zatvorena.' : 'Akcija je otkazana.')}>
                {dijalog === 'zatvori' ? 'Zatvori' : 'Otkaži'}
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}

/** „Neispravnosti“ (R8.1, R8.16): kvarovi sa filterom „trenutno otvoreno“, sekcijom i pretragom; trajanje kvara. */
export function NeispravnostiPage() {
  const [f, setF] = useState<KvaroviFilteri>({ otvoreno: true, sekcijaId: null, pretraga: '' });
  const kvarovi = useKvarovi(f);
  const sekcije = useSekcije();
  return (
    <div className="space-y-4">
      <PageHeader title="Neispravnosti" actions={<IzvozDugme url="/izvoz/kvarovi.xlsx" params={parametriKvarova(f)} ime="Neispravnosti.xlsx" />} />
      <Card>
        <CardBody className="grid gap-3 sm:grid-cols-3">
          <Input aria-label="Pretraga" placeholder="Broj kvara, uređaj ili oznaka" value={f.pretraga} onChange={(e) => setF({ ...f, pretraga: e.target.value })} />
          <Select aria-label="Sekcija" value={f.sekcijaId} nullable placeholder="Sve sekcije" onChange={(v) => setF({ ...f, sekcijaId: v })}
            options={(sekcije.data ?? []).map((s) => ({ value: s.id, label: `Sekcija ${s.sifra}` }))} />
          <Checkbox label="Trenutno otvoreno" checked={f.otvoreno === true} onChange={(e) => setF({ ...f, otvoreno: e.target.checked ? true : null })} />
        </CardBody>
      </Card>
      {kvarovi.isLoading ? <Ucitavanje /> : !kvarovi.data?.length ? (
        <Card><CardBody><EmptyState icon={ShieldAlert} title="Nema neispravnosti za zadate filtere." /></CardBody></Card>
      ) : (
        <ul className="grid gap-2 lg:grid-cols-2">
          {kvarovi.data.map((k) => (
            <li key={k.id} className="rounded-md border border-gray-200 bg-white p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <Link to={`/neispravnosti/${k.id}`} className="whitespace-nowrap font-semibold text-primary-700 hover:underline">{k.broj}</Link>
                <Badge tone={k.zatvoren ? 'gray' : 'red'}>{k.zatvoren ? `zatvoren ${formatirajDatum(k.zatvoren)}` : `otvoren ${k.trajanjeDana} dana`}</Badge>
              </div>
              <p className="mt-1 break-words text-sm"><Link to={`/uredjaji/${k.uredjajId}`} className="text-primary-700 hover:underline">{k.uredjaj}</Link> (sekcija {k.sekcija}) · {k.aktivnost}</p>
              <p className="mt-1 text-xs text-gray-600">
                Pregled <Link to={`/pregledi/${k.otvorioPregledId}`} className="whitespace-nowrap text-primary-700 hover:underline">{k.otvorioPregled}</Link>
                {` · prijava ${k.prijava}`}{k.otvorenihAkcija > 0 && ` · otvorenih akcija ${k.otvorenihAkcija}`}{k.poslednjaNapomena && ` · ${k.poslednjaNapomena}`}
              </p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** Ekran kvara (R8.16): podaci, svi datumi prijave, akcije na stavci. */
export function KvarPage() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const kvar = useKvar(id);
  if (kvar.isLoading) return <Ucitavanje />;
  if (!kvar.data) return <p role="alert" className="text-sm text-red-600">{porukaGreske(kvar.error, 'Kvar nije učitan.')}</p>;
  const k = kvar.data.kvar;
  return (
    <div className="space-y-4">
      <Button variant="ghost" onClick={() => navigate(-1)}><ArrowLeft size={14} aria-hidden /> Nazad</Button>
      <Card>
        <CardBody className="space-y-1 text-sm">
          <div className="flex flex-wrap items-center gap-2"><h1 className="whitespace-nowrap text-xl font-semibold">{k.broj}</h1>
            <Badge tone={k.zatvoren ? 'gray' : 'red'}>{k.zatvoren ? 'zatvoren' : 'otvoren'}</Badge></div>
          <p className="break-words"><Link to={`/uredjaji/${k.uredjajId}`} className="text-primary-700 hover:underline">{k.uredjaj}</Link> (sekcija {k.sekcija}) · {k.aktivnost}</p>
          <p className="text-gray-600">Otvoren {formatirajDatum(k.otvoren)}{k.zatvoren && `, zatvoren ${formatirajDatum(k.zatvoren)}`} · trajanje {k.trajanjeDana} dana · prijava {k.prijava}</p>
        </CardBody>
      </Card>
      <Card>
        <CardHeader><CardTitle>Prijave</CardTitle></CardHeader>
        <CardBody>
          <ul className="space-y-1 text-sm">
            {kvar.data.prijave.map((p) => (
              <li key={p.pregledId} className="flex flex-wrap gap-x-3">
                <span className="whitespace-nowrap text-gray-500">{formatirajDatum(p.datum)}</span>
                <Link to={`/pregledi/${p.pregledId}`} className="whitespace-nowrap text-primary-700 hover:underline">{p.broj}</Link>
                {p.napomena && <span className="break-words">{p.napomena}</span>}
              </li>
            ))}
          </ul>
        </CardBody>
      </Card>
      <Card>
        <CardHeader><CardTitle>Akcije na stavci</CardTitle></CardHeader>
        <CardBody>{kvar.data.akcije.length === 0 ? <p className="text-sm text-gray-500">Nema akcija.</p> : <ul className="space-y-2">{kvar.data.akcije.map((a) => <AkcijaRed key={a.id} a={a} />)}</ul>}</CardBody>
      </Card>
    </div>
  );
}
