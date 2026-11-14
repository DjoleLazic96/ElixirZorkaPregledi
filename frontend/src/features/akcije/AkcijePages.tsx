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
import { FilterTraka, type AktivanFilter } from '../../components/ui/FilterTraka';
import { cn } from '../../lib/cn';
import { formatirajDatum, formatirajVreme } from '../../lib/datum';
import { imaPravo } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import { useSekcije } from '../sabloni/hooks';
import {
  parametriAkcija, parametriKvarova, useAkcija, useAkcije, useKvar, useKvarovi, useOdlukaAkcije,
  type AkcijaDto, type AkcijeFilteri, type KvaroviFilteri,
} from './hooks';
import { HITNOSTI, ZADUZENI_PREDLOZI, hitnostLabela, tonHitnosti, vrstaLabela } from './oznake';
import { NacinZatvaranja, NeposlatiZahtevi, ZahtevNaCekanju, ZatvaranjeForma } from './ZatvaranjeAkcije';


/** Kartica akcije za listu (R7.3, R7.6): broj, pregled, uređaj, hitnost, rok (crveno kad je istekao, R7.8), kvar na stavci. */
function AkcijaRed({ a }: { a: AkcijaDto }) {
  const navigate = useNavigate();
  return (
    // Stavka po mockup-u (`.list .it`); sadrži više linkova (akcija, uređaj, pregled). Dodir bilo gde drugde na kartici otvara
    // akciju (zamerka 65 — na telefonu je broj mala meta); linkovi unutra i dalje vode svaki na svoje.
    // „Stavka sada ispravna“ (R7.15) — zelena ivica levo, da se akcija za odluku odmah uoči (zamerka 50).
    <li onClick={(e) => { if (!(e.target as HTMLElement).closest('a')) navigate(`/akcije/${a.id}`); }}
      className={cn('cursor-pointer rounded-[14px] border border-gray-200 bg-[#f7f8f6] px-3 py-2.5 hover:bg-gray-100', a.stavkaSadaIspravna && 'border-l-4 border-l-green-500')}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Link to={`/akcije/${a.id}`} className="whitespace-nowrap text-[13px] font-bold text-primary-500 hover:underline">{a.broj}<span aria-hidden> ›</span></Link>
        <span className="flex flex-wrap gap-1">
          {a.rokIstekao && <Badge tone="red">rok istekao</Badge>}
          <Badge tone={tonHitnosti(a.hitnost)}>{hitnostLabela(a.hitnost)}</Badge>
          {a.status !== 'otvorena' && <Badge tone="gray">{a.status}</Badge>}
          {a.stavkaSadaIspravna && <Badge tone="green">stavka sada ispravna</Badge>}
          {a.zahtevZatvaranja && <Badge tone="amber">zahtev za zatvaranje</Badge>}
        </span>
      </div>
      <p className="mt-1 break-words text-sm font-semibold text-gray-800"><Link to={`/uredjaji/${a.uredjajId}`} className="text-primary-700 hover:underline">{a.uredjaj}</Link> · {a.aktivnost} · {vrstaLabela(a.vrsta)}{a.opis && ` — ${a.opis}`}</p>
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
  const statusi = [{ value: 'otvorena', label: 'Otvorene' }, { value: 'zatvorena', label: 'Zatvorene' }, { value: 'otkazana', label: 'Otkazane' }];
  const aktivni: AktivanFilter[] = [
    ...(f.status ? [{ kljuc: 'status', labela: statusi.find((x) => x.value === f.status)?.label ?? f.status, ukloni: () => setF((x) => ({ ...x, status: null })) }] : []),
    ...(f.hitnost ? [{ kljuc: 'hitnost', labela: hitnostLabela(f.hitnost), ukloni: () => setF((x) => ({ ...x, hitnost: null })) }] : []),
    ...(f.sekcijaId ? [{ kljuc: 'sekcija', labela: `Sekcija ${sekcije.data?.find((x) => x.id === f.sekcijaId)?.sifra ?? ''}`.trim(), ukloni: () => setF((x) => ({ ...x, sekcijaId: null })) }] : []),
    ...(f.sadaIspravna ? [{ kljuc: 'ispravna', labela: 'Stavka sada ispravna', ukloni: () => setF((x) => ({ ...x, sadaIspravna: false })) }] : []),
  ];
  return (
    <div className="space-y-4">
      <PageHeader title="Potrebne akcije" actions={<IzvozDugme url="/izvoz/akcije.xlsx" params={parametriAkcija(f)} ime="Akcije.xlsx" />} />
      <ZahteviZaZatvaranje />
      <FilterTraka aktivni={aktivni} onObrisiSve={() => setF({ status: null, hitnost: null, sekcijaId: null, sadaIspravna: false })}>
          <Select aria-label="Status" value={f.status} nullable placeholder="Svi statusi" onChange={(v) => setF({ ...f, status: v })} options={statusi} />
          <Select aria-label="Hitnost" value={f.hitnost} nullable placeholder="Sve hitnosti" options={HITNOSTI} onChange={(v) => setF({ ...f, hitnost: v })} />
          <Select aria-label="Sekcija" value={f.sekcijaId} nullable placeholder="Sve sekcije" onChange={(v) => setF({ ...f, sekcijaId: v })}
            options={(sekcije.data ?? []).map((s) => ({ value: s.id, label: `Sekcija ${s.sifra}` }))} />
          <Checkbox label="Stavka sada ispravna" checked={f.sadaIspravna} onChange={(e) => setF({ ...f, sadaIspravna: e.target.checked })} />
      </FilterTraka>
      {akcije.isLoading ? <Ucitavanje /> : !akcije.data?.length ? (
        <Card><CardBody><EmptyState icon={ListChecks} title="Nema akcija za zadate filtere." /></CardBody></Card>
      ) : (
        <ul className="grid gap-2 lg:grid-cols-2">{akcije.data.map((a) => <AkcijaRed key={a.id} a={a} />)}</ul>
      )}
    </div>
  );
}

/** Zahtevi inženjera za zatvaranje koji čekaju odluku (zamerka 49) — samo za one koji odlučuju. */
function ZahteviZaZatvaranje() {
  const { me } = useAuth();
  const odlucuje = imaPravo(me ?? { roles: [], prava: [] }, 'akcije.zatvaranje');
  const zahtevi = useAkcije({ status: 'otvorena', hitnost: null, sekcijaId: null, sadaIspravna: false, saZahtevom: true }, odlucuje);
  if (!odlucuje || !zahtevi.data?.length) return null;
  return (
    <Card>
      <CardHeader><CardTitle>Zahtevi za zatvaranje ({zahtevi.data.length})</CardTitle></CardHeader>
      <CardBody>
        <ul className="divide-y divide-gray-100 text-sm">
          {zahtevi.data.map((a) => (
            <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
              <span className="min-w-0 break-words">
                <Link to={`/akcije/${a.id}`} className="font-semibold text-primary-700 hover:underline">{a.broj}</Link> · {a.uredjaj} · {a.zahtevZatvaranja?.podneo}: {a.zahtevZatvaranja?.uradjeno}
              </span>
              <Link to={`/akcije/${a.id}`} className="text-xs font-medium text-primary-700 hover:underline">Odluči ›</Link>
            </li>
          ))}
        </ul>
      </CardBody>
    </Card>
  );
}

/**
 * Ekran akcije (R7.6, R7.12): podaci, istorija; Supervizor/Direktor zatvaraju (šta, kada, ko — zamerka 49), otkazuju i
 * dodeljuju zaduženog; inženjer šalje zahtev za zatvaranje.
 */
export function AkcijaPage() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const { me } = useAuth();
  const sme = imaPravo(me ?? { roles: [], prava: [] }, 'akcije.zatvaranje');
  const inzenjer = imaPravo(me ?? { roles: [], prava: [] }, 'pregledi.popunjavanje');
  const akcija = useAkcija(id);
  const odluka = useOdlukaAkcije(id);
  const toast = useToast();
  const [dijalog, setDijalog] = useState<null | 'zatvori' | 'otkazi' | 'zahtev'>(null);
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
            {a.stavkaSadaIspravna && <Badge tone="green">stavka sada ispravna</Badge>}
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
          <NacinZatvaranja akcija={a} />
          {a.zahtevZatvaranja && <ZahtevNaCekanju akcija={a} odlucuje={sme} />}
          <NeposlatiZahtevi akcijaId={a.id} />
          {inzenjer && !sme && a.status === 'otvorena' && !a.zahtevZatvaranja && (
            <div className="pt-2"><Button onClick={() => setDijalog('zahtev')}>Zahtev za zatvaranje</Button></div>
          )}
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
              <li key={i} className="flex flex-wrap gap-x-3"><span className="whitespace-nowrap text-gray-500">{formatirajVreme(d.at)}</span><span className="break-words">{d.tekst}{d.korisnik && ` · ${d.korisnik}`}</span></li>
            ))}
          </ul>
        </CardBody>
      </Card>
      {(dijalog === 'zatvori' || dijalog === 'zahtev') && <ZatvaranjeForma akcija={a} zahtev={dijalog === 'zahtev'} onClose={() => setDijalog(null)} />}
      {dijalog === 'otkazi' && (
        <Modal open title="Otkaži akciju" onClose={() => setDijalog(null)}>
          <div className="space-y-4">
            <Textarea aria-label="Razlog" placeholder="Razlog" rows={3} value={razlog} onChange={(e) => setRazlog(e.target.value)} />
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setDijalog(null)}>Odustani</Button>
              <Button disabled={!razlog.trim() || odluka.isPending} onClick={() => void sacuvaj({ putanja: 'otkazi', razlog: razlog.trim() }, 'Akcija je otkazana.')}>
                Otkaži
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
  const aktivni: AktivanFilter[] = [
    ...(f.pretraga.trim() ? [{ kljuc: 'pretraga', labela: `„${f.pretraga.trim()}“`, kucanje: true, ukloni: () => setF((x) => ({ ...x, pretraga: '' })) }] : []),
    ...(f.sekcijaId ? [{ kljuc: 'sekcija', labela: `Sekcija ${sekcije.data?.find((x) => x.id === f.sekcijaId)?.sifra ?? ''}`.trim(), ukloni: () => setF((x) => ({ ...x, sekcijaId: null })) }] : []),
    ...(f.otvoreno ? [{ kljuc: 'otvoreno', labela: 'Trenutno otvoreno', ukloni: () => setF((x) => ({ ...x, otvoreno: null })) }] : []),
  ];
  return (
    <div className="space-y-4">
      <PageHeader title="Neispravnosti" actions={<IzvozDugme url="/izvoz/kvarovi.xlsx" params={parametriKvarova(f)} ime="Neispravnosti.xlsx" />} />
      <FilterTraka aktivni={aktivni} kolone="sm:grid-cols-3" onObrisiSve={() => setF({ otvoreno: null, sekcijaId: null, pretraga: '' })}>
          <Input aria-label="Pretraga" placeholder="Broj kvara, uređaj ili oznaka" value={f.pretraga} onChange={(e) => setF({ ...f, pretraga: e.target.value })} />
          <Select aria-label="Sekcija" value={f.sekcijaId} nullable placeholder="Sve sekcije" onChange={(v) => setF({ ...f, sekcijaId: v })}
            options={(sekcije.data ?? []).map((s) => ({ value: s.id, label: `Sekcija ${s.sifra}` }))} />
          <Checkbox label="Trenutno otvoreno" checked={f.otvoreno === true} onChange={(e) => setF({ ...f, otvoreno: e.target.checked ? true : null })} />
      </FilterTraka>
      {kvarovi.isLoading ? <Ucitavanje /> : !kvarovi.data?.length ? (
        <Card><CardBody><EmptyState icon={ShieldAlert} title="Nema neispravnosti za zadate filtere." /></CardBody></Card>
      ) : (
        <ul className="grid gap-2 lg:grid-cols-2">
          {kvarovi.data.map((k) => (
            <li key={k.id} className="rounded-[14px] border border-gray-200 bg-[#f7f8f6] px-3 py-2.5">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <Link to={`/neispravnosti/${k.id}`} className="whitespace-nowrap text-[13px] font-bold text-primary-500 hover:underline">{k.broj}<span aria-hidden> ›</span></Link>
                <Badge tone={k.zatvoren ? 'gray' : 'red'}>{k.zatvoren ? `zatvoren ${formatirajDatum(k.zatvoren)}` : `otvoren ${k.trajanjeDana} dana`}</Badge>
              </div>
              <p className="mt-1 break-words text-sm font-semibold text-gray-800"><Link to={`/uredjaji/${k.uredjajId}`} className="text-primary-700 hover:underline">{k.uredjaj}</Link> (sekcija {k.sekcija}) · {k.aktivnost}</p>
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
