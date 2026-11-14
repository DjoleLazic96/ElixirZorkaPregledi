import { useRef, useState, type FormEvent } from 'react';
import { ClipboardList, Upload } from 'lucide-react';
import type { AktivnostDto, ApiProblem, SablonPregledDto } from '../../api/types';
import { useToast } from '../../app/providers/ToastProvider';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { EmptyState } from '../../components/ui/EmptyState';
import { FormField } from '../../components/ui/FormField';
import { Input } from '../../components/ui/Input';
import { Modal } from '../../components/ui/Modal';
import { PageHeader } from '../../components/ui/PageHeader';
import { Ucitavanje } from '../../components/ui/Spinner';
import { Tabs } from '../../components/ui/Tabs';
import { porukaGreske } from '../../lib/apiError';
import { formatirajDatum } from '../../lib/datum';
import { canEditOprema, canUvoziSablone } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import { Link } from 'react-router-dom';
import { Select } from '../../components/ui/Select';
import { useAktivnosti, useDodatnaPolja, useSabloni, useSacuvajAktivnost, useUvoziSablone } from './hooks';
import { IstorijatTab, PredlogDijalog, PredloziTab } from './IzmenePages';
import { STATUS_VERZIJE, useVerzija, useVerzije } from './izmene';
import { UputstvoDugme } from './UputstvoDijalog';
import { mnozina } from '../../lib/broj';

const UREDJAJ = { jednina: 'uređaj', paukal: 'uređaja', mnozina: 'uređaja' };
const STAVKA = { jednina: 'stavka', paukal: 'stavke', mnozina: 'stavki' };

const TABOVI = [
  { key: 'sabloni', label: 'Šabloni' },
  { key: 'aktivnosti', label: 'Katalog aktivnosti' },
  { key: 'polja', label: 'Dodatna polja' },
  { key: 'predlozi', label: 'Predlozi izmena' },
  { key: 'istorijat', label: 'Istorijat' },
] as const;

type Tab = (typeof TABOVI)[number]['key'];

/**
 * „Šabloni“ (R2.1): šest šablona i važeća verzija kao na obrascu, katalog aktivnosti i dodatna polja.
 * Svi čitaju i predlažu izmene; katalog, uputstva i nacrt menja ko ima „sabloni.nacrt“, objavljuje Direktor
 * („sabloni.objava“); prvi uvoz iz Excela radi Admin.
 */
export function SabloniPage({ pocetniTab = 'sabloni' }: { pocetniTab?: Tab }) {
  const { me } = useAuth();
  const pristup = me ?? { roles: [], prava: [] };
  const [tab, setTab] = useState<Tab>(pocetniTab);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Šabloni"
        description="Šabloni pregleda kao na papirnim obrascima: sekcije, uređaji i aktivnosti sa dva odgovora."
        actions={canUvoziSablone(pristup) ? <UvozDugme /> : undefined}
      />
      <Tabs items={TABOVI} value={tab} onChange={setTab} aria-label="Šabloni" />
      {tab === 'sabloni' && <SabloniTab smeDaMenja={canEditOprema(pristup)} />}
      {tab === 'aktivnosti' && <AktivnostiTab smeDaMenja={canEditOprema(pristup)} />}
      {tab === 'polja' && <PoljaTab />}
      {tab === 'predlozi' && <PredloziTab />}
      {tab === 'istorijat' && <IstorijatTab />}
    </div>
  );
}

// ─── Uvoz ─────────────────────────────────────────────────────────────────

function UvozDugme() {
  const uvoz = useUvoziSablone();
  const toast = useToast();
  const ulaz = useRef<HTMLInputElement>(null);
  const [greske, setGreske] = useState<string[] | null>(null);

  async function izaberi(fajl: File | undefined) {
    if (!fajl) return;
    try {
      const r = await uvoz.mutateAsync(fajl);
      const stavki = r.sabloni.reduce((s, x) => s + x.stavki, 0);
      toast.success(`Uvezeno ${r.sabloni.length} šablona, ${stavki} stavki.`);
    } catch (err) {
      const lista = (err as { response?: { data?: ApiProblem } }).response?.data?.greske;
      setGreske(lista && lista.length > 0 ? lista : [porukaGreske(err, 'Uvoz nije uspeo.')]);
    } finally {
      if (ulaz.current) ulaz.current.value = '';
    }
  }

  return (
    <>
      <input
        ref={ulaz}
        type="file"
        accept=".xlsx"
        className="hidden"
        aria-label="Fajl za uvoz šablona"
        onChange={(e) => void izaberi(e.target.files?.[0])}
      />
      <Button onClick={() => ulaz.current?.click()} disabled={uvoz.isPending}>
        <Upload size={14} aria-hidden />
        {uvoz.isPending ? 'Uvoz…' : 'Uvezi šablone'}
      </Button>
      {greske && (
        <Modal open title="Uvoz nije urađen" onClose={() => setGreske(null)} widthClass="max-w-2xl">
          <p className="mb-3 text-sm text-gray-600">Ništa nije upisano. Ispravite fajl i pokušajte ponovo:</p>
          <ul className="max-h-80 list-disc space-y-1 overflow-y-auto pl-5 text-sm text-red-700">
            {greske.map((g) => (
              <li key={g} className="break-words">
                {g}
              </li>
            ))}
          </ul>
          <div className="flex justify-end pt-4">
            <Button variant="ghost" onClick={() => setGreske(null)}>
              Zatvori
            </Button>
          </div>
        </Modal>
      )}
    </>
  );
}

// ─── Šabloni ──────────────────────────────────────────────────────────────

function SabloniTab({ smeDaMenja }: { smeDaMenja: boolean }) {
  const sabloni = useSabloni();
  const [izabran, setIzabran] = useState<string | null>(null);
  const prvi = sabloni.data?.find((s) => s.vazecaVerzija !== null)?.tip ?? null;
  const tip = izabran ?? prvi;

  if (sabloni.isLoading) return <Ucitavanje />;
  if (sabloni.isError || !sabloni.data)
    return <p role="alert" className="text-sm text-red-600">{porukaGreske(sabloni.error, 'Šabloni nisu učitani.')}</p>;
  if (prvi === null)
    return (
      <Card>
        <CardBody>
          <EmptyState icon={ClipboardList} title="Šabloni još nisu uvezeni." description="Administrator ih uvozi iz Excela dugmetom „Uvezi šablone“." />
        </CardBody>
      </Card>
    );

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2" role="group" aria-label="Izbor šablona">
        {sabloni.data.map((s) => (
          <SablonDugme key={s.tip} s={s} aktivan={s.tip === tip} onClick={() => setIzabran(s.tip)} />
        ))}
      </div>
      {tip && <VerzijaPrikaz key={tip} tip={tip} smeDaMenja={smeDaMenja} />}
    </div>
  );
}

function SablonDugme({ s, aktivan, onClick }: { s: SablonPregledDto; aktivan: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={s.vazecaVerzija === null}
      aria-pressed={aktivan}
      className={
        'min-h-11 rounded-md border px-3 py-2 text-left text-sm transition-colors disabled:opacity-50 ' +
        (aktivan ? 'border-primary-500 bg-primary-50 text-primary-800' : 'border-gray-200 bg-white hover:bg-gray-50')
      }
    >
      <span className="block font-semibold">{s.tip}</span>
      <span className="block text-xs text-gray-500">
        {s.vazecaVerzija === null ? 'nije uvezen' : `v${s.vazecaVerzija} · ${s.brojUredjaja} ${mnozina(s.brojUredjaja, UREDJAJ)} · ${s.brojStavki} ${mnozina(s.brojStavki, STAVKA)}`}
      </span>
    </button>
  );
}

function VerzijaPrikaz({ tip, smeDaMenja }: { tip: string; smeDaMenja: boolean }) {
  const [broj, setBroj] = useState<number | null>(null);
  const v = useVerzija(tip, broj);
  const verzije = useVerzije(tip);
  const [predlog, setPredlog] = useState(false);
  if (v.isLoading) return <Ucitavanje />;
  if (v.isError || !v.data) return <p role="alert" className="text-sm text-red-600">{porukaGreske(v.error, 'Šablon nije učitan.')}</p>;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2 text-sm text-gray-600">
          <span>{v.data.tip}, verzija {v.data.broj} ({STATUS_VERZIJE[v.data.status] ?? v.data.status})</span>
          {v.data.vaziOd && <span>· važi od {formatirajDatum(v.data.vaziOd)}</span>}
          {(verzije.data?.length ?? 0) > 1 && (
            <div className="w-56">
              <Select aria-label="Verzija" value={broj} nullable placeholder="Važeća verzija" onChange={setBroj}
                options={(verzije.data ?? []).filter((x) => x.status !== 'nacrt').map((x) => ({ value: x.broj, label: `Verzija ${x.broj} (${STATUS_VERZIJE[x.status] ?? x.status})` }))} />
            </div>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" onClick={() => setPredlog(true)}>Predloži izmenu</Button>
          {smeDaMenja && <Link to={`/sabloni/${tip}/nacrt`}><Button>Nacrt nove verzije</Button></Link>}
        </div>
      </div>
      {predlog && <PredlogDijalog v={v.data} onClose={() => setPredlog(false)} />}
      {v.data.sekcije.map((sek) => (
        <Card key={sek.sifra}>
          <CardHeader>
            <CardTitle>Sekcija {sek.sifra}</CardTitle>
          </CardHeader>
          <CardBody className="space-y-3">
            {sek.uredjaji.map((u) => (
              <div key={u.uredjajId} className="rounded-md border border-gray-100 p-3">
                <p className="break-words font-medium text-gray-900">
                  {u.naziv} {u.oznaka && <span className="whitespace-nowrap text-gray-600">{u.oznaka}</span>}
                </p>
                <ul className="mt-2 space-y-1">
                  {u.stavke.map((s) => (
                    <li key={s.stavkaId} className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 text-sm">
                      <span className="flex min-w-0 items-center gap-1 break-words text-gray-800">
                        <UputstvoDugme aktivnostId={s.aktivnostId} aktivnost={s.aktivnost} smeDaMenja={smeDaMenja} />
                        {s.aktivnost}
                        {s.polja.map((p) => (
                          <Badge key={p} tone="primary" className="ml-2">
                            {p}
                          </Badge>
                        ))}
                      </span>
                      <span className="whitespace-nowrap text-xs text-gray-500">
                        {s.odgovorIspravan} / {s.odgovorNeispravan}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </CardBody>
        </Card>
      ))}
    </div>
  );
}

// ─── Katalog aktivnosti ───────────────────────────────────────────────────

function AktivnostiTab({ smeDaMenja }: { smeDaMenja: boolean }) {
  const aktivnosti = useAktivnosti();
  const sacuvaj = useSacuvajAktivnost();
  const toast = useToast();
  const [forma, setForma] = useState<AktivnostDto | 'nova' | null>(null);
  const [greska, setGreska] = useState<string | null>(null);

  async function potvrdi(id: number | null, input: { naziv: string; odgovorIspravan: string; odgovorNeispravan: string }) {
    setGreska(null);
    try {
      await sacuvaj.mutateAsync({ id, input });
      toast.success(id === null ? 'Aktivnost je dodata.' : 'Aktivnost je izmenjena.');
      setForma(null);
    } catch (err) {
      setGreska(porukaGreske(err, 'Čuvanje aktivnosti nije uspelo.'));
    }
  }

  return (
    <Card>
      <CardHeader className="flex flex-wrap items-center justify-between gap-2">
        <CardTitle>Katalog aktivnosti</CardTitle>
        {smeDaMenja && (
          <Button
            size="sm"
            onClick={() => {
              setGreska(null);
              setForma('nova');
            }}
          >
            Nova aktivnost
          </Button>
        )}
      </CardHeader>
      <CardBody>
        {aktivnosti.isLoading ? (
          <Ucitavanje />
        ) : !aktivnosti.data || aktivnosti.data.length === 0 ? (
          <EmptyState title="Katalog je prazan." description="Aktivnosti nastaju uvozom šablona ili ručno." />
        ) : (
          <ul className="divide-y divide-gray-100">
            {aktivnosti.data.map((a) => (
              <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                <span className="flex min-w-0 items-center gap-1 break-words text-gray-900">
                  <UputstvoDugme aktivnostId={a.id} aktivnost={a.naziv} smeDaMenja={smeDaMenja} />
                  {a.naziv}
                  {!a.imaUputstvo && <Badge className="ml-1">bez uputstva</Badge>}
                </span>
                <span className="flex items-center gap-3 text-xs text-gray-500">
                  <span className="whitespace-nowrap">
                    {a.odgovorIspravan} / {a.odgovorNeispravan}
                  </span>
                  <span className="whitespace-nowrap">{a.brojStavki} stavki</span>
                  {smeDaMenja && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setGreska(null);
                        setForma(a);
                      }}
                    >
                      Izmeni
                    </Button>
                  )}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardBody>
      {forma && (
        <AktivnostForma
          key={forma === 'nova' ? 'nova' : forma.id}
          aktivnost={forma === 'nova' ? null : forma}
          saving={sacuvaj.isPending}
          greska={greska}
          onSubmit={(input) => void potvrdi(forma === 'nova' ? null : forma.id, input)}
          onClose={() => setForma(null)}
        />
      )}
    </Card>
  );
}

interface AktivnostFormaProps {
  aktivnost: AktivnostDto | null;
  saving: boolean;
  greska: string | null;
  onSubmit: (input: { naziv: string; odgovorIspravan: string; odgovorNeispravan: string }) => void;
  onClose: () => void;
}

export function AktivnostForma({ aktivnost, saving, greska, onSubmit, onClose }: AktivnostFormaProps) {
  const [naziv, setNaziv] = useState(aktivnost?.naziv ?? '');
  const [ok, setOk] = useState(aktivnost?.odgovorIspravan ?? 'Ispravno');
  const [nok, setNok] = useState(aktivnost?.odgovorNeispravan ?? 'Neispravno');

  const potvrdi = (e: FormEvent) => {
    e.preventDefault();
    onSubmit({ naziv: naziv.trim(), odgovorIspravan: ok.trim(), odgovorNeispravan: nok.trim() });
  };

  return (
    <Modal open title={aktivnost ? 'Izmena aktivnosti' : 'Nova aktivnost'} onClose={onClose}>
      <form onSubmit={potvrdi} className="space-y-4" noValidate>
        <FormField label="Naziv" htmlFor="akt-naziv">
          <Input id="akt-naziv" value={naziv} onChange={(e) => setNaziv(e.target.value)} />
        </FormField>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label="Odgovor „u redu“" htmlFor="akt-ok">
            <Input id="akt-ok" value={ok} onChange={(e) => setOk(e.target.value)} />
          </FormField>
          <FormField label="Odgovor „kvar“" htmlFor="akt-nok" hint="Uz ovaj odgovor je napomena obavezna.">
            <Input id="akt-nok" value={nok} onChange={(e) => setNok(e.target.value)} />
          </FormField>
        </div>
        {greska && (
          <p role="alert" className="text-sm text-red-600">
            {greska}
          </p>
        )}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={saving}>
            Odustani
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Čuvanje…' : 'Sačuvaj'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

// ─── Dodatna polja ────────────────────────────────────────────────────────

const TIP_LABELA: Record<string, string> = { broj: 'broj', merenje: 'merenje', dve_vrednosti: 'dve vrednosti', da_ne: 'da / ne' };

function PoljaTab() {
  const polja = useDodatnaPolja();
  if (polja.isLoading) return <Ucitavanje />;
  return (
    <Card>
      <CardBody>
        <ul className="divide-y divide-gray-100">
          {(polja.data ?? []).map((p) => (
            <li key={p.id} className="py-2 text-sm">
              <p className="font-medium text-gray-900">{p.naziv}</p>
              <p className="text-xs text-gray-500">
                {TIP_LABELA[p.tip]}
                {p.jedinica && ` · ${p.jedinica}`}
                {p.oznake.length > 0 && ` · ${p.oznake.join(', ')}`}
                {' · '}
                {p.prikaz === 'uz_neispravan' ? 'prikazuje se uz neispravan odgovor' : 'uvek'}
                {p.obavezno ? ' · obavezno' : ' · opciono'}
              </p>
            </li>
          ))}
        </ul>
      </CardBody>
    </Card>
  );
}
