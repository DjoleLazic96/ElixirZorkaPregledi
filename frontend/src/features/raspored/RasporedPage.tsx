import { useState, type FormEvent } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useToast } from '../../app/providers/ToastProvider';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { DatePicker } from '../../components/ui/DatePicker';
import { EmptyState } from '../../components/ui/EmptyState';
import { FormField } from '../../components/ui/FormField';
import { Input } from '../../components/ui/Input';
import { Modal } from '../../components/ui/Modal';
import { PageHeader } from '../../components/ui/PageHeader';
import { Select } from '../../components/ui/Select';
import { Ucitavanje } from '../../components/ui/Spinner';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { danasYmd, dodajDane, formatirajDatum, kratakDan, ponedeljakNedelje } from '../../lib/datum';
import { imaPravo } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import {
  VRSTE_ODSUSTVA, useInzenjeri, useOdluciOdsustvo, useOdluciPrebacivanje, useOdsustva, usePotvrdi, usePredlog,
  usePreglediUOdsustvu, useRaspored, useSledeciPz, useUnesiOdsustvo, useZahteviPrebacivanja, useZakaziPz,
  type OdsustvoDto, type PredlogStavka, type PregledKalendarDto, type ZahtevPrebacivanjaDto,
} from './hooks';
import { PregledKartica } from './KalendarPage';
import { bojaPregleda, stavkeDana } from './kalendar';

/**
 * „Raspored“ (Supervizor, Direktor): nedelja, predlog i potvrda DP-a (R3.10), zakazivanje PZ-a (R3.7, R3.8),
 * zahtevi za prebacivanje (R3.12) i odsustva sa prebacivanjem (R3.18–R3.20). Stranica drži mutacije i toastove.
 */
export function RasporedPage() {
  const { me } = useAuth();
  const pristup = me ?? { roles: [], prava: [] };
  const [nedelja, setNedelja] = useState(ponedeljakNedelje(danasYmd()));
  const r = useRaspored(nedelja, dodajDane(nedelja, 6));
  const [izabran, setIzabran] = useState<PregledKalendarDto | null>(null);
  const [predlog, setPredlog] = useState<PredlogStavka[] | null>(null);
  const [pzOtvoren, setPzOtvoren] = useState(false);
  const [odsustvoOtvoreno, setOdsustvoOtvoreno] = useState(false);
  const traziPredlog = usePredlog();
  const toast = useToast();
  const danas = danasYmd();
  const smeRaspored = imaPravo(pristup, 'raspored.uredjivanje');
  const smeOdsustva = imaPravo(pristup, 'odsustva.odobravanje');

  async function predlozi() {
    try {
      const p = await traziPredlog.mutateAsync(nedelja);
      if (p.length === 0) toast.success('Svi radni dani ove nedelje već imaju DP.');
      else setPredlog(p);
    } catch (err) {
      toast.error(porukaGreske(err, 'Predlog nije napravljen.'));
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Raspored"
        actions={
          <div className="flex flex-wrap gap-2">
            {smeRaspored && <Button onClick={() => void predlozi()} disabled={traziPredlog.isPending}>Predloži raspored</Button>}
            {smeRaspored && <Button variant="secondary" onClick={() => setPzOtvoren(true)}>Zakaži PZ</Button>}
            {smeOdsustva && <Button variant="secondary" onClick={() => setOdsustvoOtvoreno(true)}>Unesi odsustvo</Button>}
          </div>
        }
      />

      <Card>
        <CardHeader className="flex flex-wrap items-center justify-between gap-2">
          <CardTitle>Nedelja {formatirajDatum(nedelja)} – {formatirajDatum(dodajDane(nedelja, 6))}</CardTitle>
          <div className="flex gap-1">
            <Button variant="secondary" size="sm" onClick={() => setNedelja(dodajDane(nedelja, -7))} aria-label="Prethodna nedelja"><ChevronLeft size={14} aria-hidden /></Button>
            <Button variant="secondary" size="sm" onClick={() => setNedelja(ponedeljakNedelje(danas))}>Ova nedelja</Button>
            <Button variant="secondary" size="sm" onClick={() => setNedelja(dodajDane(nedelja, 7))} aria-label="Sledeća nedelja"><ChevronRight size={14} aria-hidden /></Button>
          </div>
        </CardHeader>
        <CardBody>
          {r.isLoading ? <Ucitavanje /> : (
            <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-7">
              {Array.from({ length: 7 }, (_, i) => dodajDane(nedelja, i)).map((dan) => {
                const s = stavkeDana(r.data, dan);
                return (
                  <li key={dan} className={cn('min-w-0 rounded-md border border-gray-200 p-2', dan === danas && 'ring-2 ring-primary-400')}>
                    <p className="text-xs font-medium text-gray-600">{kratakDan(dan)}, {formatirajDatum(dan)}</p>
                    {s.neradni && <p className="text-xs text-gray-500">{s.neradni.naziv}</p>}
                    <div className="mt-1 space-y-1">
                      {s.pregledi.map((p) => (
                        <button key={p.id} type="button" onClick={() => setIzabran(p)}
                          className={cn('block min-h-10 w-full rounded border px-2 py-1 text-left text-xs', bojaPregleda(p, danas))}>
                          <span className="block whitespace-nowrap font-semibold">{p.broj}</span>
                          <span className="block truncate">{p.dodeljen}</span>
                        </button>
                      ))}
                      {s.preneti.map((p) => (
                        <p key={`p${p.id}`} className="text-xs text-red-700">{p.broj} prenet na {formatirajDatum(p.planiraniDatum)}</p>
                      ))}
                      {s.odsutni.map((o) => <p key={o.korisnikId} className="truncate text-xs text-gray-500">odsutan: {o.ime}</p>)}
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </CardBody>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        {smeRaspored && <ZahteviPrebacivanja />}
        {smeOdsustva && <OdsustvaNaCekanju />}
      </div>

      {predlog && <PredlogModal predlog={predlog} onClose={() => setPredlog(null)} />}
      {pzOtvoren && <PzModal onClose={() => setPzOtvoren(false)} />}
      {odsustvoOtvoreno && <OdsustvoModal onClose={() => setOdsustvoOtvoreno(false)} />}
      {izabran && <PregledKartica pregled={izabran} onClose={() => setIzabran(null)} />}
    </div>
  );
}

// ─── Predlog → potvrda ────────────────────────────────────────────────────

function PredlogModal({ predlog, onClose }: { predlog: PredlogStavka[]; onClose: () => void }) {
  const inzenjeri = useInzenjeri();
  const potvrdi = usePotvrdi();
  const toast = useToast();
  const [stavke, setStavke] = useState(predlog);
  const [greska, setGreska] = useState<string | null>(null);
  const opcije = (inzenjeri.data ?? []).map((i) => ({ value: i.id, label: i.ime }));

  async function sacuvaj() {
    setGreska(null);
    try {
      const novi = await potvrdi.mutateAsync(stavke.map((s) => ({ datum: s.datum, tip: s.tip, inzenjerId: s.inzenjerId })));
      toast.success(`Raspored je potvrđen: ${novi.map((n) => n.broj).join(', ')}.`);
      onClose();
    } catch (err) {
      setGreska(porukaGreske(err, 'Raspored nije potvrđen.'));
    }
  }

  return (
    <Modal open title="Predlog rasporeda" onClose={onClose} widthClass="max-w-xl">
      <p className="mb-3 text-sm text-gray-600">DP se dodeljuje redom aktivnim inženjerima, a odsutni se preskaču. Inženjera možete promeniti pre potvrde.</p>
      <ul className="space-y-2">
        {stavke.map((s, i) => (
          <li key={s.datum} className="flex flex-wrap items-center gap-2">
            <span className="w-32 shrink-0 text-sm">{kratakDan(s.datum)}, {formatirajDatum(s.datum)}</span>
            <span className="w-10 text-sm font-semibold">{s.tip}</span>
            <Select className="min-w-0 flex-1" aria-label={`Inženjer za ${formatirajDatum(s.datum)}`} value={s.inzenjerId} options={opcije}
              onChange={(v) => setStavke(stavke.map((x, j) => (j === i ? { ...x, inzenjerId: v } : x)))} />
          </li>
        ))}
      </ul>
      {greska && <p role="alert" className="mt-3 text-sm text-red-600">{greska}</p>}
      <div className="flex justify-end gap-2 pt-4">
        <Button variant="ghost" onClick={onClose} disabled={potvrdi.isPending}>Odustani</Button>
        <Button onClick={() => void sacuvaj()} disabled={potvrdi.isPending}>{potvrdi.isPending ? 'Čuvanje…' : 'Potvrdi raspored'}</Button>
      </div>
    </Modal>
  );
}

// ─── PZ ───────────────────────────────────────────────────────────────────

function PzModal({ onClose }: { onClose: () => void }) {
  const sledeci = useSledeciPz(true);
  const inzenjeri = useInzenjeri();
  const zakazi = useZakaziPz();
  const toast = useToast();
  const [datum, setDatum] = useState(danasYmd());
  const [tip, setTip] = useState<string | null>(null);
  const [inz, setInz] = useState<string | null>(null);
  const [napomena, setNapomena] = useState('');
  const [greska, setGreska] = useState<string | null>(null);
  const izabraniTip = tip ?? sledeci.data ?? null;
  const promena = sledeci.data !== undefined && izabraniTip !== sledeci.data;

  async function potvrdi(e: FormEvent) {
    e.preventDefault();
    if (!inz) { setGreska('Izaberite inženjera.'); return; }
    try {
      const p = await zakazi.mutateAsync({ datum, tip: promena ? izabraniTip : null, inzenjerId: inz, napomena: napomena.trim() || null });
      toast.success(`Zakazan ${p.broj} za ${formatirajDatum(p.planiraniDatum)}.`);
      onClose();
    } catch (err) {
      setGreska(porukaGreske(err, 'PZ nije zakazan.'));
    }
  }

  return (
    <Modal open title="Zakaži PZ" onClose={onClose}>
      <form onSubmit={potvrdi} className="space-y-4" noValidate>
        <FormField label="Datum" htmlFor="pz-datum"><DatePicker id="pz-datum" value={datum} onChange={setDatum} clearable={false} /></FormField>
        <FormField label="PZ" htmlFor="pz-tip" hint={sledeci.data ? `Sledeći po redu: ${sledeci.data}. Drugi izbor je „Promeni PZ“.` : undefined}>
          <Select id="pz-tip" value={izabraniTip} onChange={setTip} options={['PZ1', 'PZ2', 'PZ3', 'PZ4'].map((t) => ({ value: t, label: t }))} />
        </FormField>
        <FormField label="Inženjer" htmlFor="pz-inz">
          <Select id="pz-inz" value={inz} onChange={setInz} placeholder="Izaberite inženjera" options={(inzenjeri.data ?? []).map((i) => ({ value: i.id, label: i.ime }))} />
        </FormField>
        {promena && (
          <FormField label="Napomena (nije obavezno)" htmlFor="pz-nap">
            <Input id="pz-nap" value={napomena} onChange={(e) => setNapomena(e.target.value)} />
          </FormField>
        )}
        {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>Odustani</Button>
          <Button type="submit" disabled={zakazi.isPending}>{promena ? 'Promeni PZ i zakaži' : 'Zakaži'}</Button>
        </div>
      </form>
    </Modal>
  );
}

// ─── Zahtevi za prebacivanje ──────────────────────────────────────────────

function ZahteviPrebacivanja() {
  const zahtevi = useZahteviPrebacivanja('zahtev');
  const [odluka, setOdluka] = useState<ZahtevPrebacivanjaDto | null>(null);
  return (
    <Card>
      <CardHeader><CardTitle>Zahtevi inženjera</CardTitle></CardHeader>
      <CardBody>
        {zahtevi.isLoading ? <Ucitavanje /> : !zahtevi.data?.length ? <EmptyState title="Nema zahteva na čekanju." /> : (
          <ul className="divide-y divide-gray-100">
            {zahtevi.data.map((z) => (
              <li key={z.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                <span className="min-w-0">
                  <span className="whitespace-nowrap font-semibold">{z.pregled.broj}</span> · {formatirajDatum(z.pregled.planiraniDatum)} · {z.podneo}
                  {' '}<Badge tone={z.vrsta === 'raniji_pocetak' ? 'blue' : 'gray'}>{z.vrsta === 'raniji_pocetak' ? 'raniji početak' : 'prebacivanje'}</Badge>
                  <span className="block break-words text-xs text-gray-500">{z.razlog}{z.predlozenaZamena && ` · predlog: ${z.predlozenaZamena}`}</span>
                </span>
                <Button size="sm" variant="secondary" onClick={() => setOdluka(z)}>Odluči</Button>
              </li>
            ))}
          </ul>
        )}
      </CardBody>
      {odluka && <OdlukaPrebacivanja zahtev={odluka} onClose={() => setOdluka(null)} />}
    </Card>
  );
}

function OdlukaPrebacivanja({ zahtev, onClose }: { zahtev: ZahtevPrebacivanjaDto; onClose: () => void }) {
  const inzenjeri = useInzenjeri();
  const odluci = useOdluciPrebacivanje();
  const toast = useToast();
  const [zamena, setZamena] = useState<string | null>(zahtev.predlozenaZamenaId);
  const [greska, setGreska] = useState<string | null>(null);

  const raniji = zahtev.vrsta === 'raniji_pocetak';

  async function posalji(odobreno: boolean) {
    try {
      await odluci.mutateAsync({ id: zahtev.id, odobreno, zamenaId: odobreno && !raniji ? zamena : null });
      toast.success(!odobreno ? 'Zahtev je odbijen.' : raniji ? 'Pregled je pomeren na danas.' : 'Pregled je prebačen.');
      onClose();
    } catch (err) {
      setGreska(porukaGreske(err, 'Odluka nije sačuvana.'));
    }
  }

  return (
    <Modal open title={`${raniji ? 'Raniji početak' : 'Prebacivanje'} — ${zahtev.pregled.broj}`} onClose={onClose}>
      <p className="mb-3 break-words text-sm text-gray-600">{zahtev.podneo}: {zahtev.razlog}</p>
      {raniji ? (
        <p className="text-sm text-gray-700">
          Zakazan za {formatirajDatum(zahtev.pregled.planiraniDatum)}. Ako odobrite, pregled se pomera na danas i {zahtev.podneo} može odmah da ga započne.
        </p>
      ) : (
        <FormField label="Zamena" htmlFor="odl-zamena">
          <Select id="odl-zamena" value={zamena} onChange={setZamena} placeholder="Izaberite zamenu"
            options={(inzenjeri.data ?? []).filter((i) => i.id !== zahtev.podneoId).map((i) => ({ value: i.id, label: i.ime }))} />
        </FormField>
      )}
      {greska && <p role="alert" className="mt-3 text-sm text-red-600">{greska}</p>}
      <div className="flex justify-end gap-2 pt-4">
        <Button variant="ghost" onClick={() => void posalji(false)} disabled={odluci.isPending}>Odbij</Button>
        <Button onClick={() => void posalji(true)} disabled={odluci.isPending || (!raniji && !zamena)}>Odobri</Button>
      </div>
    </Modal>
  );
}

// ─── Odsustva ─────────────────────────────────────────────────────────────

function OdsustvaNaCekanju() {
  const zahtevi = useOdsustva('zahtev');
  const [odluka, setOdluka] = useState<OdsustvoDto | null>(null);
  return (
    <Card>
      <CardHeader><CardTitle>Zahtevi za odsustvo</CardTitle></CardHeader>
      <CardBody>
        {zahtevi.isLoading ? <Ucitavanje /> : !zahtevi.data?.length ? <EmptyState title="Nema zahteva na čekanju." /> : (
          <ul className="divide-y divide-gray-100">
            {zahtevi.data.map((o) => (
              <li key={o.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                <span>{o.ime} · {formatirajDatum(o.od)} – {formatirajDatum(o.do)} · {VRSTE_ODSUSTVA.find((v) => v.value === o.vrsta)?.label}</span>
                <Button size="sm" variant="secondary" onClick={() => setOdluka(o)}>Odluči</Button>
              </li>
            ))}
          </ul>
        )}
      </CardBody>
      {odluka && <OdsustvoModal zahtev={odluka} onClose={() => setOdluka(null)} />}
    </Card>
  );
}

/**
 * Unos odsustva ili odluka o zahtevu (R3.18–R3.20): pokazuje planirane preglede inženjera u periodu, sa predlogom
 * zamene po rotaciji; „Prebaci sve na“ postavlja istu zamenu svima. Započet pregled se ne prebacuje automatski.
 */
function OdsustvoModal({ zahtev, onClose }: { zahtev?: OdsustvoDto; onClose: () => void }) {
  const inzenjeri = useInzenjeri();
  const unesi = useUnesiOdsustvo();
  const odluci = useOdluciOdsustvo();
  const toast = useToast();
  const [korisnik, setKorisnik] = useState<string | null>(zahtev?.korisnikId ?? null);
  const [od, setOd] = useState(zahtev?.od ?? danasYmd());
  const [doDatuma, setDo] = useState(zahtev?.do ?? danasYmd());
  const [vrsta, setVrsta] = useState<string | null>(zahtev?.vrsta ?? 'godisnji');
  const [napomena, setNapomena] = useState(zahtev?.napomena ?? '');
  const [zamene, setZamene] = useState<Record<number, string | null>>({});
  const [greska, setGreska] = useState<string | null>(null);
  const pregledi = usePreglediUOdsustvu(korisnik, od, doDatuma);
  const opcije = (inzenjeri.data ?? []).filter((i) => i.id !== korisnik).map((i) => ({ value: i.id, label: i.ime }));
  const zamenaZa = (id: number, predlog: string | null) => (id in zamene ? zamene[id] : predlog);

  const prebacivanja = (pregledi.data ?? [])
    .filter((x) => x.pregled.status === 'planiran')
    .map((x) => ({ pregledId: x.pregled.id, zamenaId: zamenaZa(x.pregled.id, x.predlogZameneId) }))
    .filter((x): x is { pregledId: number; zamenaId: string } => x.zamenaId !== null);

  async function sacuvaj(odobreno: boolean) {
    setGreska(null);
    try {
      if (zahtev) await odluci.mutateAsync({ id: zahtev.id, odobreno, razlog: null, prebacivanja: odobreno ? prebacivanja : [] });
      else {
        if (!korisnik || !vrsta) { setGreska('Izaberite inženjera i vrstu odsustva.'); return; }
        await unesi.mutateAsync({ korisnikId: korisnik, od, do: doDatuma, vrsta, napomena: napomena.trim() || null, prebacivanja });
      }
      toast.success(!zahtev ? 'Odsustvo je uneto.' : odobreno ? 'Odsustvo je odobreno.' : 'Zahtev je odbijen.');
      onClose();
    } catch (err) {
      setGreska(porukaGreske(err, 'Odsustvo nije sačuvano.'));
    }
  }

  const radi = unesi.isPending || odluci.isPending;
  return (
    <Modal open title={zahtev ? `Zahtev za odsustvo — ${zahtev.ime}` : 'Unesi odsustvo'} onClose={onClose} widthClass="max-w-xl">
      <div className="space-y-4">
        {!zahtev && (
          <>
            <FormField label="Inženjer" htmlFor="ods-inz">
              <Select id="ods-inz" value={korisnik} onChange={setKorisnik} placeholder="Izaberite inženjera"
                options={(inzenjeri.data ?? []).map((i) => ({ value: i.id, label: i.ime }))} />
            </FormField>
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField label="Od" htmlFor="ods-od"><DatePicker id="ods-od" value={od} onChange={setOd} clearable={false} /></FormField>
              <FormField label="Do" htmlFor="ods-do"><DatePicker id="ods-do" value={doDatuma} onChange={setDo} min={od} clearable={false} /></FormField>
            </div>
            <FormField label="Vrsta" htmlFor="ods-vrsta"><Select id="ods-vrsta" value={vrsta} onChange={setVrsta} options={VRSTE_ODSUSTVA} /></FormField>
            <FormField label="Napomena (nije obavezno)" htmlFor="ods-nap"><Input id="ods-nap" value={napomena} onChange={(e) => setNapomena(e.target.value)} /></FormField>
          </>
        )}
        {zahtev && (
          <p className="text-sm text-gray-600">
            {formatirajDatum(zahtev.od)} – {formatirajDatum(zahtev.do)} · {VRSTE_ODSUSTVA.find((v) => v.value === zahtev.vrsta)?.label}
            {zahtev.napomena && ` · ${zahtev.napomena}`}
          </p>
        )}
        {korisnik && (pregledi.data?.length ?? 0) > 0 && (
          <div className="space-y-2">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="text-sm font-medium">Planirani pregledi u tom periodu</p>
              <Select className="w-48" aria-label="Prebaci sve na" value={null} placeholder="Prebaci sve na…" options={opcije}
                onChange={(v) => setZamene(Object.fromEntries((pregledi.data ?? []).map((x) => [x.pregled.id, v])))} />
            </div>
            <ul className="space-y-1">
              {pregledi.data!.map((x) => (
                <li key={x.pregled.id} className="flex flex-wrap items-center gap-2 text-sm">
                  <span className="w-40 whitespace-nowrap">{x.pregled.broj} · {formatirajDatum(x.pregled.planiraniDatum)}</span>
                  {x.pregled.status === 'planiran' ? (
                    <Select className="min-w-0 flex-1" aria-label={`Zamena za ${x.pregled.broj}`} value={zamenaZa(x.pregled.id, x.predlogZameneId)}
                      nullable placeholder="Ne prebacuj" options={opcije} onChange={(v) => setZamene({ ...zamene, [x.pregled.id]: v })} />
                  ) : (
                    <span className="text-xs text-amber-700">započet — ne prebacuje se automatski</span>
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}
        {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
        <div className="flex justify-end gap-2">
          {zahtev ? (
            <>
              <Button variant="ghost" onClick={() => void sacuvaj(false)} disabled={radi}>Odbij</Button>
              <Button onClick={() => void sacuvaj(true)} disabled={radi}>Odobri i prebaci</Button>
            </>
          ) : (
            <>
              <Button variant="ghost" onClick={onClose} disabled={radi}>Odustani</Button>
              <Button onClick={() => void sacuvaj(true)} disabled={radi}>Sačuvaj i prebaci</Button>
            </>
          )}
        </div>
      </div>
    </Modal>
  );
}
