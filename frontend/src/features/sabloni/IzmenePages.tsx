import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowDown, ArrowLeft, ArrowUp, Pencil, Trash2 } from 'lucide-react';
import type { SablonVerzijaDto, StavkaDto } from '../../api/types';
import { useConfirm } from '../../app/providers/ConfirmProvider';
import { useToast } from '../../app/providers/ToastProvider';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { Checkbox } from '../../components/ui/Checkbox';
import { DatePicker } from '../../components/ui/DatePicker';
import { FormField } from '../../components/ui/FormField';
import { Modal } from '../../components/ui/Modal';
import { PageHeader } from '../../components/ui/PageHeader';
import { Select } from '../../components/ui/Select';
import { Ucitavanje } from '../../components/ui/Spinner';
import { Textarea } from '../../components/ui/Textarea';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { danasYmd, formatirajDatum, formatirajVreme } from '../../lib/datum';
import { canEditOprema, imaPravo } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import { useAktivnosti, useDodatnaPolja, useSekcije, useUredjaji } from './hooks';
import {
  VRSTE_DOGADJAJA, VRSTE_PREDLOGA, useIstorijat, useIzmenaNacrta, useNacrt, useObjavi, useOdlukaPredloga, usePodnesiPredlog, usePredlozi,
  type IstorijatFilteri, type PredlogDto, type RazlikaDto,
} from './izmene';

const TIPOVI = ['DP1', 'DP2', 'PZ1', 'PZ2', 'PZ3', 'PZ4'];

const RAZLIKA: Record<RazlikaDto['vrsta'], { labela: string; ton: 'primary' | 'red' | 'amber' | 'gray' }> = {
  dodato: { labela: 'Dodato', ton: 'primary' },
  izbaceno: { labela: 'Izbačeno', ton: 'red' },
  izmenjeno: { labela: 'Izmenjeno', ton: 'amber' },
  pomereno: { labela: 'Pomereno', ton: 'gray' },
};

/**
 * Nacrt šablona (R2.6): Direktor ili Admin dodaju uređaje iz registra i aktivnosti iz kataloga, izbacuju, menjaju i pomeraju stavke,
 * vide razlike i odobrene predloge; Direktor objavljuje sa datumom od kog važi. Započeti pregledi se završavaju po staroj verziji.
 */
export function NacrtPage() {
  const tip = (useParams().tip ?? '').toUpperCase();
  const { me } = useAuth();
  const sme = imaPravo(me ?? { roles: [], prava: [] }, 'sabloni.objava');
  const nacrt = useNacrt(tip);
  const izmena = useIzmenaNacrta(tip);
  const toast = useToast();
  const confirm = useConfirm();
  const [dodaj, setDodaj] = useState(false);
  const [menja, setMenja] = useState<StavkaDto | null>(null);
  const [objava, setObjava] = useState(false);

  async function posalji(v: Parameters<typeof izmena.mutateAsync>[0], uspeh?: string) {
    try {
      await izmena.mutateAsync(v);
      if (uspeh) toast.success(uspeh);
      return true;
    } catch (e) {
      toast.error(porukaGreske(e, 'Izmena nacrta nije uspela.'));
      return false;
    }
  }

  const nazad = <Link to="/sabloni" className="inline-flex items-center gap-1 text-sm text-primary-700 hover:underline"><ArrowLeft size={14} aria-hidden /> Šabloni</Link>;
  if (nacrt.isLoading) return <Ucitavanje />;
  if (nacrt.isError) return <p role="alert" className="text-sm text-red-600">{porukaGreske(nacrt.error, 'Nacrt nije učitan.')}</p>;
  if (!nacrt.data)
    return (
      <div className="space-y-4">
        {nazad}
        <PageHeader title={`Nacrt ${tip}`} />
        <Card><CardBody className="space-y-3 text-sm">
          <p>Nacrt je kopija poslednje verzije u kojoj pripremate izmene. Dok se ne objavi, pregledi rade po važećoj verziji.</p>
          <Button disabled={izmena.isPending} onClick={() => void posalji({ metod: 'post' }, 'Nacrt je otvoren.')}>Otvori nacrt</Button>
        </CardBody></Card>
      </div>
    );

  const n = nacrt.data;
  return (
    <div className="space-y-4">
      {nazad}
      <PageHeader title={`Nacrt ${tip} · verzija ${n.verzija.broj}`} description={n.osnovnaVerzija ? `Nastao od verzije ${n.osnovnaVerzija}.` : undefined}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button variant="ghost" onClick={() => void confirm({ title: 'Odbaci nacrt', message: 'Sve izmene u nacrtu se brišu. Važeća verzija ostaje.', confirmLabel: 'Odbaci', tone: 'danger' })
              .then((da) => da && posalji({ metod: 'delete' }, 'Nacrt je odbačen.'))}>Odbaci nacrt</Button>
            <Button variant="secondary" onClick={() => setDodaj(true)}>Dodaj stavku</Button>
            {sme && <Button onClick={() => setObjava(true)} disabled={n.razlike.length === 0}>Objavi</Button>}
          </div>
        } />
      {n.zakazana && <p className="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-900">Verzija {n.zakazana.broj} je zakazana i važi od {formatirajDatum(n.zakazana.vaziOd)}; ovaj nacrt se objavljuje posle toga.</p>}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Razlike ({n.razlike.length})</CardTitle></CardHeader>
          <CardBody>
            {n.razlike.length === 0 ? <p className="text-sm text-gray-500">Nacrt je isti kao važeća verzija.</p> : (
              <ul className="space-y-1.5 text-sm">
                {n.razlike.map((r, i) => (
                  <li key={i} className="flex flex-wrap items-baseline gap-2">
                    <Badge tone={RAZLIKA[r.vrsta].ton}>{RAZLIKA[r.vrsta].labela}</Badge>
                    <span className="min-w-0 break-words">Sekcija {r.sekcija} · {r.uredjaj} · {r.bilo ? <><s className="text-gray-500">{r.bilo}</s> → {r.aktivnost}</> : r.aktivnost}</span>
                  </li>
                ))}
              </ul>
            )}
          </CardBody>
        </Card>
        <Card>
          <CardHeader><CardTitle>Odobreni predlozi ({n.predlozi.length})</CardTitle></CardHeader>
          <CardBody>
            {n.predlozi.length === 0 ? <p className="text-sm text-gray-500">Nema odobrenih predloga koji čekaju.</p> : (
              <ul className="space-y-2 text-sm">
                {n.predlozi.map((p) => <li key={p.id} className="break-words"><Badge>{VRSTE_PREDLOGA.find((v) => v.value === p.vrsta)?.label}</Badge> {p.opis}{p.stavka && <span className="text-gray-500"> ({p.stavka})</span>}</li>)}
              </ul>
            )}
          </CardBody>
        </Card>
      </div>

      <StavkeNacrta v={n.verzija} zauzeto={izmena.isPending}
        onPomeri={(s, gore) => void posalji({ metod: 'post', putanja: `/stavke/${s.stavkaId}/pomeri`, telo: { gore } })}
        onIzbaci={(s) => void posalji({ metod: 'delete', putanja: `/stavke/${s.stavkaId}` }, 'Stavka je izbačena iz nacrta.')}
        onIzmeni={setMenja} />

      {dodaj && <DodajStavkuDijalog onClose={() => setDodaj(false)} onSubmit={async (telo) => (await posalji({ metod: 'post', putanja: '/stavke', telo }, 'Stavka je dodata.')) && setDodaj(false)} />}
      {menja && <IzmeniStavkuDijalog s={menja} onClose={() => setMenja(null)}
        onSubmit={async (telo) => (await posalji({ metod: 'put', putanja: `/stavke/${menja.stavkaId}`, telo }, 'Stavka je izmenjena.')) && setMenja(null)} />}
      {objava && <ObjavaDijalog tip={tip} broj={n.verzija.broj} izmena={n.razlike.length} onClose={() => setObjava(false)} />}
    </div>
  );
}

function StavkeNacrta({ v, zauzeto, onPomeri, onIzbaci, onIzmeni }: {
  v: SablonVerzijaDto; zauzeto: boolean; onPomeri: (s: StavkaDto, gore: boolean) => void; onIzbaci: (s: StavkaDto) => void; onIzmeni: (s: StavkaDto) => void;
}) {
  return (
    <>
      {v.sekcije.map((sek) => (
        <Card key={sek.sifra}>
          <CardHeader><CardTitle>Sekcija {sek.sifra}</CardTitle></CardHeader>
          <CardBody className="space-y-3">
            {sek.uredjaji.map((u) => (
              <div key={u.uredjajId} className="rounded-md border border-gray-100 p-3">
                <p className="break-words font-medium">{u.naziv} {u.oznaka && <span className="text-gray-600">{u.oznaka}</span>}</p>
                <ul className="mt-2 divide-y divide-gray-50">
                  {u.stavke.map((s) => (
                    <li key={s.stavkaId} className="flex flex-wrap items-center justify-between gap-2 py-1 text-sm">
                      <span className="min-w-0 break-words">{s.aktivnost} <span className="text-xs text-gray-500">({s.odgovorIspravan} / {s.odgovorNeispravan})</span>
                        {s.polja.map((p) => <Badge key={p} tone="primary" className="ml-1">{p}</Badge>)}</span>
                      <span className="flex gap-1">
                        <Button size="sm" variant="ghost" disabled={zauzeto} aria-label={`Gore — ${s.aktivnost}`} onClick={() => onPomeri(s, true)}><ArrowUp size={14} /></Button>
                        <Button size="sm" variant="ghost" disabled={zauzeto} aria-label={`Dole — ${s.aktivnost}`} onClick={() => onPomeri(s, false)}><ArrowDown size={14} /></Button>
                        <Button size="sm" variant="ghost" disabled={zauzeto} aria-label={`Izmeni — ${s.aktivnost}`} onClick={() => onIzmeni(s)}><Pencil size={14} /></Button>
                        <Button size="sm" variant="ghost" disabled={zauzeto} aria-label={`Izbaci — ${s.aktivnost}`} onClick={() => onIzbaci(s)}><Trash2 size={14} /></Button>
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </CardBody>
        </Card>
      ))}
    </>
  );
}

function PoljaIzbor({ izabrana, onChange }: { izabrana: number[]; onChange: (v: number[]) => void }) {
  const polja = useDodatnaPolja();
  return (
    <fieldset className="space-y-1">
      <legend className="text-sm font-medium text-gray-700">Dodatna polja</legend>
      {(polja.data ?? []).map((p) => (
        <Checkbox key={p.id} label={p.naziv} checked={izabrana.includes(p.id)} onChange={(e) => onChange(e.target.checked ? [...izabrana, p.id] : izabrana.filter((x) => x !== p.id))} />
      ))}
    </fieldset>
  );
}

function DodajStavkuDijalog({ onClose, onSubmit }: { onClose: () => void; onSubmit: (telo: { sekcijaId: number; uredjajId: number; aktivnostId: number; poljaIds: number[] }) => Promise<unknown> }) {
  const sekcije = useSekcije();
  const [sekcijaId, setSekcijaId] = useState<number | null>(null);
  const uredjaji = useUredjaji({ sekcijaId, status: 'aktivan', pretraga: '', bezSablona: false });
  const aktivnosti = useAktivnosti();
  const [uredjajId, setUredjajId] = useState<number | null>(null);
  const [aktivnostId, setAktivnostId] = useState<number | null>(null);
  const [polja, setPolja] = useState<number[]>([]);
  return (
    <Modal open title="Dodaj stavku" onClose={onClose}>
      <div className="space-y-3">
        <FormField label="Sekcija" htmlFor="ds-sek">
          <Select id="ds-sek" value={sekcijaId} placeholder="Izaberite sekciju" onChange={(v) => { setSekcijaId(v); setUredjajId(null); }}
            options={(sekcije.data ?? []).map((s) => ({ value: s.id, label: `Sekcija ${s.sifra}` }))} />
        </FormField>
        <FormField label="Uređaj iz registra" htmlFor="ds-ur" hint="Novi uređaj se prvo dodaje u registar (Uređaji).">
          <Select id="ds-ur" value={uredjajId} placeholder="Izaberite uređaj" disabled={sekcijaId === null} onChange={setUredjajId}
            options={(uredjaji.data ?? []).map((u) => ({ value: u.id, label: `${u.naziv} ${u.oznaka}`.trim() }))} />
        </FormField>
        <FormField label="Aktivnost iz kataloga" htmlFor="ds-akt">
          <Select id="ds-akt" value={aktivnostId} placeholder="Izaberite aktivnost" onChange={setAktivnostId}
            options={(aktivnosti.data ?? []).map((a) => ({ value: a.id, label: a.naziv, hint: `${a.odgovorIspravan} / ${a.odgovorNeispravan}` }))} />
        </FormField>
        <PoljaIzbor izabrana={polja} onChange={setPolja} />
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="ghost" onClick={onClose}>Odustani</Button>
          <Button disabled={!sekcijaId || !uredjajId || !aktivnostId}
            onClick={() => void onSubmit({ sekcijaId: sekcijaId!, uredjajId: uredjajId!, aktivnostId: aktivnostId!, poljaIds: polja })}>Dodaj</Button>
        </div>
      </div>
    </Modal>
  );
}

function IzmeniStavkuDijalog({ s, onClose, onSubmit }: { s: StavkaDto; onClose: () => void; onSubmit: (telo: { aktivnostId: number; poljaIds: number[] }) => Promise<unknown> }) {
  const aktivnosti = useAktivnosti();
  const polja = useDodatnaPolja();
  const [aktivnostId, setAktivnostId] = useState<number | null>(s.aktivnostId);
  const [izabrana, setIzabrana] = useState<number[] | null>(null);
  const pocetna = (polja.data ?? []).filter((p) => s.polja.includes(p.naziv)).map((p) => p.id);
  const trenutna = izabrana ?? pocetna;
  return (
    <Modal open title="Izmeni stavku" onClose={onClose}>
      <div className="space-y-3">
        <p className="text-sm text-gray-600">Ispravka teksta je izbor druge aktivnosti iz kataloga; nova aktivnost se prvo dodaje u katalog.</p>
        <FormField label="Aktivnost" htmlFor="is-akt">
          <Select id="is-akt" value={aktivnostId} onChange={setAktivnostId}
            options={(aktivnosti.data ?? []).map((a) => ({ value: a.id, label: a.naziv, hint: `${a.odgovorIspravan} / ${a.odgovorNeispravan}` }))} />
        </FormField>
        <PoljaIzbor izabrana={trenutna} onChange={setIzabrana} />
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="ghost" onClick={onClose}>Odustani</Button>
          <Button disabled={!aktivnostId} onClick={() => void onSubmit({ aktivnostId: aktivnostId!, poljaIds: trenutna })}>Sačuvaj</Button>
        </div>
      </div>
    </Modal>
  );
}

function ObjavaDijalog({ tip, broj, izmena, onClose }: { tip: string; broj: number; izmena: number; onClose: () => void }) {
  const objavi = useObjavi(tip);
  const toast = useToast();
  const [vaziOd, setVaziOd] = useState(danasYmd);
  return (
    <Modal open title={`Objavi ${tip} verziju ${broj}`} onClose={onClose}>
      <div className="space-y-3 text-sm">
        <p>{izmena} izmena. Pregledi započeti pre tog dana završavaju se po staroj verziji.</p>
        <FormField label="Važi od" htmlFor="ob-od">
          <DatePicker id="ob-od" value={vaziOd} min={danasYmd()} clearable={false} onChange={setVaziOd} />
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="ghost" onClick={onClose}>Odustani</Button>
          <Button disabled={objavi.isPending || !vaziOd} onClick={() => void objavi.mutateAsync(vaziOd)
            .then((v) => { toast.success(v.status === 'objavljena' ? `Verzija ${v.broj} važi od danas.` : `Verzija ${v.broj} je zakazana od ${formatirajDatum(v.vaziOd!)}.`); onClose(); })
            .catch((e) => toast.error(porukaGreske(e, 'Objava nije uspela.')))}>Objavi</Button>
        </div>
      </div>
    </Modal>
  );
}

// ─── Predlozi ─────────────────────────────────────────────────────────────

/** „Predloži izmenu“ (R2.8): vrsta, stavka (za izbacivanje i ispravku), opis i obavezan razlog. */
export function PredlogDijalog({ v, onClose }: { v: SablonVerzijaDto; onClose: () => void }) {
  const podnesi = usePodnesiPredlog();
  const toast = useToast();
  const [vrsta, setVrsta] = useState<string>('ispravka');
  const [stavkaId, setStavkaId] = useState<number | null>(null);
  const [opis, setOpis] = useState('');
  const [razlog, setRazlog] = useState('');
  const stavke = v.sekcije.flatMap((s) => s.uredjaji.flatMap((u) => u.stavke.map((x) => ({ value: x.stavkaId, label: `${s.sifra} · ${u.naziv} ${u.oznaka} · ${x.aktivnost}` }))));
  return (
    <Modal open title={`Predlog izmene ${v.tip}`} onClose={onClose} widthClass="max-w-lg">
      <div className="space-y-3">
        <FormField label="Vrsta" htmlFor="pr-vrsta">
          <Select id="pr-vrsta" value={vrsta} onChange={(x) => setVrsta(x ?? 'ispravka')} options={VRSTE_PREDLOGA.map((x) => ({ value: x.value, label: x.label }))} />
        </FormField>
        {vrsta !== 'dodavanje' && (
          <FormField label="Stavka" htmlFor="pr-st">
            <Select id="pr-st" value={stavkaId} nullable={vrsta === 'ispravka'} placeholder="Izaberite stavku" onChange={setStavkaId} options={stavke} />
          </FormField>
        )}
        <FormField label="Šta treba promeniti" htmlFor="pr-opis"><Textarea id="pr-opis" rows={2} value={opis} onChange={(e) => setOpis(e.target.value)} /></FormField>
        <FormField label="Razlog" htmlFor="pr-razlog"><Textarea id="pr-razlog" rows={2} value={razlog} onChange={(e) => setRazlog(e.target.value)} /></FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="ghost" onClick={onClose}>Odustani</Button>
          <Button disabled={podnesi.isPending || !opis.trim() || !razlog.trim() || (vrsta === 'izbacivanje' && !stavkaId)}
            onClick={() => void podnesi.mutateAsync({ tip: v.tip, vrsta, stavkaId: vrsta === 'dodavanje' ? null : stavkaId, opis, razlog })
              .then(() => { toast.success('Predlog je poslat.'); onClose(); })
              .catch((e) => toast.error(porukaGreske(e, 'Predlog nije poslat.')))}>Pošalji predlog</Button>
        </div>
      </div>
    </Modal>
  );
}

const STATUS_PREDLOGA: Record<PredlogDto['status'], { labela: string; ton: 'amber' | 'primary' | 'gray' }> = {
  zahtev: { labela: 'čeka odluku', ton: 'amber' }, odobren: { labela: 'odobren', ton: 'primary' }, odbijen: { labela: 'odbijen', ton: 'gray' },
};

/** Lista predloga; Direktor i Admin odobravaju ili odbijaju. */
export function PredloziTab() {
  const { me } = useAuth();
  const odlucuje = canEditOprema(me ?? { roles: [], prava: [] });
  const [status, setStatus] = useState<string | null>('zahtev');
  const predlozi = usePredlozi(status);
  const odluka = useOdlukaPredloga();
  const toast = useToast();
  const [odbija, setOdbija] = useState<PredlogDto | null>(null);
  const [odgovor, setOdgovor] = useState('');

  function odluci(id: number, odobren: boolean, odg: string | null) {
    void odluka.mutateAsync({ id, odobren, odgovor: odg })
      .then(() => { toast.success(odobren ? 'Predlog je odobren i čeka nacrt sledeće verzije.' : 'Predlog je odbijen.'); setOdbija(null); setOdgovor(''); })
      .catch((e) => toast.error(porukaGreske(e, 'Odluka nije sačuvana.')));
  }

  return (
    <Card>
      <CardHeader className="flex flex-wrap items-center justify-between gap-2">
        <CardTitle>Predlozi izmena</CardTitle>
        <div className="w-48"><Select aria-label="Status" value={status} nullable placeholder="Svi" onChange={setStatus}
          options={[{ value: 'zahtev', label: 'Čekaju odluku' }, { value: 'odobren', label: 'Odobreni' }, { value: 'odbijen', label: 'Odbijeni' }]} /></div>
      </CardHeader>
      <CardBody>
        {predlozi.isLoading ? <Ucitavanje /> : !predlozi.data?.length ? <p className="text-sm text-gray-500">Nema predloga.</p> : (
          <ul className="divide-y divide-gray-100 text-sm">
            {predlozi.data.map((p) => (
              <li key={p.id} className="space-y-1 py-3">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-semibold">{p.sablon}</span>
                  <Badge>{VRSTE_PREDLOGA.find((v) => v.value === p.vrsta)?.label}</Badge>
                  <Badge tone={STATUS_PREDLOGA[p.status].ton}>{STATUS_PREDLOGA[p.status].labela}</Badge>
                  {p.verzija && <Badge tone="primary">u verziji {p.verzija}</Badge>}
                </div>
                <p className="break-words">{p.opis}{p.stavka && <span className="text-gray-500"> · {p.stavka}</span>}</p>
                <p className="break-words text-gray-600">Razlog: {p.razlog}</p>
                <p className="text-xs text-gray-500">{p.podneo}, {formatirajVreme(p.kreiranoAt)}{p.odlucio && ` · odlučio ${p.odlucio}`}{p.odgovor && `: ${p.odgovor}`}</p>
                {odlucuje && p.status === 'zahtev' && (
                  <div className="flex gap-2 pt-1">
                    <Button size="sm" disabled={odluka.isPending} onClick={() => odluci(p.id, true, null)}>Odobri</Button>
                    <Button size="sm" variant="ghost" disabled={odluka.isPending} onClick={() => setOdbija(p)}>Odbij</Button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardBody>
      {odbija && (
        <Modal open title="Odbij predlog" onClose={() => setOdbija(null)}>
          <div className="space-y-3">
            <FormField label="Obrazloženje (nije obavezno)" htmlFor="od-odg"><Textarea id="od-odg" rows={2} value={odgovor} onChange={(e) => setOdgovor(e.target.value)} /></FormField>
            <div className="flex justify-end gap-2"><Button variant="ghost" onClick={() => setOdbija(null)}>Odustani</Button>
              <Button onClick={() => odluci(odbija.id, false, odgovor.trim() || null)}>Odbij</Button></div>
          </div>
        </Modal>
      )}
    </Card>
  );
}

// ─── Istorijat ────────────────────────────────────────────────────────────

/** „Istorijat šablona“ (R2.9): datum, šablon, verzija, opis, ko; filteri po šablonu, vrsti i periodu. */
export function IstorijatTab() {
  const [f, setF] = useState<IstorijatFilteri>({ tip: null, vrsta: null, od: '', do: '' });
  const ist = useIstorijat(f);
  return (
    <Card>
      <CardBody className="space-y-3">
        <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
          <Select aria-label="Šablon" value={f.tip} nullable placeholder="Svi šabloni" onChange={(v) => setF({ ...f, tip: v })} options={TIPOVI.map((t) => ({ value: t, label: t }))} />
          <Select aria-label="Vrsta događaja" value={f.vrsta} nullable placeholder="Sve vrste" onChange={(v) => setF({ ...f, vrsta: v })} options={VRSTE_DOGADJAJA} />
          <DatePicker aria-label="Od" value={f.od} placeholder="Od" onChange={(v) => setF({ ...f, od: v })} />
          <DatePicker aria-label="Do" value={f.do} placeholder="Do" onChange={(v) => setF({ ...f, do: v })} />
        </div>
        {ist.isLoading ? <Ucitavanje /> : !ist.data?.length ? <p className="text-sm text-gray-500">Nema događaja za zadate filtere.</p> : (
          <div className="overflow-x-auto">
            <table className={cn('w-full text-sm', ist.isFetching && 'opacity-60')}>
              <thead className="text-xs text-gray-500"><tr><th className="px-2 py-2 text-left">Vreme</th><th className="px-2 py-2 text-left">Šablon</th><th className="px-2 py-2 text-left">Verzija</th><th className="px-2 py-2 text-left">Opis</th><th className="px-2 py-2 text-left">Ko</th></tr></thead>
              <tbody className="divide-y divide-gray-100">
                {ist.data.map((d) => (
                  <tr key={d.id}>
                    <td className="whitespace-nowrap px-2 py-1.5">{formatirajVreme(d.at)}</td>
                    <td className="px-2 py-1.5">{d.sablon}</td>
                    <td className="px-2 py-1.5">{d.verzija ?? '—'}</td>
                    <td className="break-words px-2 py-1.5">{d.opis}</td>
                    <td className="px-2 py-1.5">{d.korisnik}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </CardBody>
    </Card>
  );
}
