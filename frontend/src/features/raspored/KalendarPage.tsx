import { Fragment, useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useToast } from '../../app/providers/ToastProvider';
import { Button } from '../../components/ui/Button';
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
import {
  danasYmd, dodajDane, dodajMesece, formatirajDatum, jeVikend, kratakDan, mesecGodina, ponedeljakNedelje, prviUMesecu,
} from '../../lib/datum';
import { imaPravo } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import { useSablonVerzija } from '../sabloni/hooks';
import {
  useDpKojiSePomera, useInzenjeri, useIzmeniDodelu, useOtkaziZahtevPrebacivanja, usePostaviUcesnika, usePodnesiPrebacivanje, useRaspored,
  useZahteviPrebacivanja, type PregledKalendarDto, type RasporedDto,
} from './hooks';
import { OtkaziZahtev } from './OtkaziZahtev';
import { STATUS_LABELA, bojaPregleda, ivicaPregleda, oznakaPomeranja, oznakaPregleda, stavkeDana } from './kalendar';
import { OtkazivanjeForma, PovlacenjeDijalog, TipDpForma, UpozorenjeDp } from './IzmeneRasporeda';

/**
 * Kalendar (R3.22, R3.23). Računar: mesec; telefon: spisak po danima. Klik na pregled otvara karticu sa brojem,
 * datumom, inženjerom i statusom; za svoj planiran pregled „Zatraži prebacivanje“, za Supervizora/Direktora
 * „Promeni u rasporedu“. Urađeni pregledi se otvaraju na svom ekranu od faze 06.
 */
export function KalendarPage() {
  // „Zatraži prebacivanje“ sa Početne: ?pregled=ID&datum=YYYY-MM-DD otvara mesec tog pregleda i njegovu karticu (zamerka 10).
  const [params, setParams] = useSearchParams();
  const trazeni = Number(params.get('pregled')) || null;
  const [mesec, setMesec] = useState(prviUMesecu(params.get('datum') ?? danasYmd()));
  const od = ponedeljakNedelje(mesec);
  const doDatuma = dodajDane(ponedeljakNedelje(dodajDane(dodajMesece(mesec, 1), -1)), 6);
  const r = useRaspored(od, doDatuma);
  const [izabran, setIzabran] = useState<PregledKalendarDto | null>(null);
  const danas = danasYmd();
  // Pregled iz adrese se prikazuje dok korisnik ne zatvori karticu; zatim se adresa čisti.
  const prikazan = izabran ?? (trazeni ? r.data?.pregledi.find((x) => x.id === trazeni) ?? null : null);
  const zatvori = () => {
    setIzabran(null);
    if (trazeni) setParams({}, { replace: true });
  };
  const dani: string[] = [];
  for (let d = od; d <= doDatuma; d = dodajDane(d, 1)) dani.push(d);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Kalendar"
        actions={
          <div className="flex items-center gap-1">
            <Button variant="secondary" size="sm" onClick={() => setMesec(dodajMesece(mesec, -1))} aria-label="Prethodni mesec">
              <ChevronLeft size={14} aria-hidden />
            </Button>
            <span className="min-w-32 text-center text-sm font-medium capitalize">{mesecGodina(mesec)}</span>
            <Button variant="secondary" size="sm" onClick={() => setMesec(dodajMesece(mesec, 1))} aria-label="Sledeći mesec">
              <ChevronRight size={14} aria-hidden />
            </Button>
          </div>
        }
      />
      <Legenda />
      {r.isLoading ? (
        <Ucitavanje />
      ) : r.isError ? (
        <p role="alert" className="text-sm text-red-600">{porukaGreske(r.error, 'Kalendar nije učitan.')}</p>
      ) : (
        <>
          {/* Računar i tablet: mesečna mreža. */}
          <div className="hidden grid-cols-7 gap-1 md:grid">
            {['pon', 'uto', 'sre', 'čet', 'pet', 'sub', 'ned'].map((d) => (
              <div key={d} className="p-1 text-center text-[11px] text-gray-500">{d}</div>
            ))}
            {dani.map((dan) => (
              <Dan key={dan} dan={dan} r={r.data} danas={danas} uMesecu={dan.slice(0, 7) === mesec.slice(0, 7)} onPregled={setIzabran} />
            ))}
          </div>
          {/* Telefon: spisak dana sa pregledima. */}
          <ul className="space-y-2 md:hidden">
            {dani
              .filter((dan) => dan.slice(0, 7) === mesec.slice(0, 7))
              .filter((dan) => { const s = stavkeDana(r.data, dan); return s.pregledi.length + s.preneti.length > 0 || s.neradni; })
              .map((dan) => (
                <li key={dan}>
                  <Dan dan={dan} r={r.data} danas={danas} uMesecu onPregled={setIzabran} spisak />
                </li>
              ))}
          </ul>
        </>
      )}
      {prikazan && <PregledKartica pregled={prikazan} onClose={zatvori} />}
    </div>
  );
}

function Legenda() {
  const s = [
    ['bg-white border-gray-200', 'planiran'], ['bg-amber-50 border-transparent', 'u radu'], ['bg-green-50 border-transparent', 'završen'],
    ['bg-blue-50 border-transparent', 'overen'], ['bg-red-50 border-transparent', 'nije započet tog dana'],
  ];
  return (
    <div className="flex flex-wrap gap-3 text-xs text-gray-600">
      {s.map(([k, l]) => (
        <span key={l} className="flex items-center gap-1"><span className={cn('inline-block h-3 w-3 rounded-[3px] border', k)} />{l}</span>
      ))}
    </div>
  );
}

interface DanProps {
  dan: string;
  r: RasporedDto | undefined;
  danas: string;
  uMesecu: boolean;
  onPregled: (p: PregledKalendarDto) => void;
  spisak?: boolean;
}

function Dan({ dan, r, danas, uMesecu, onPregled, spisak }: DanProps) {
  const s = stavkeDana(r, dan);
  return (
    <div
      className={cn(
        'min-h-[84px] min-w-0 rounded-lg border p-[5px] text-[11px]',
        uMesecu ? 'border-gray-200 bg-white' : 'border-gray-200 bg-[#f0efed] text-gray-400',
        (jeVikend(dan) || s.neradni) && 'bg-[#f0efed] text-gray-400',
        dan === danas && 'border-2 border-primary-500',
        spisak && 'min-h-0 p-3',
      )}
    >
      <p className="text-xs font-bold">
        {spisak ? `${kratakDan(dan)}, ${formatirajDatum(dan)}` : Number(dan.slice(8))}
        {s.neradni && <span className="ml-1 font-normal text-gray-500">· {s.neradni.naziv}</span>}
      </p>
      <div className="mt-1 space-y-1">
        {s.pregledi.map((p) => (
          <button
            key={p.id}
            type="button"
            onClick={() => onPregled(p)}
            className={cn('mt-[3px] block min-h-11 w-full rounded-[5px] border px-[5px] py-0.5 text-left text-[11px] font-semibold md:min-h-0', bojaPregleda(p, danas), ivicaPregleda(p))}
          >
            <span className="block whitespace-nowrap font-semibold">{p.broj}</span>
            <span className="block truncate">{p.dodeljen}</span>
            {oznakaPregleda(p) && <span className={cn('block truncate font-normal', oznakaPregleda(p)!.boja)}>{oznakaPregleda(p)!.tekst}</span>}
          </button>
        ))}
        {s.preneti.map((p) => (
          <p key={`p${p.id}`} className={cn('rounded-[5px] px-[5px] py-0.5 text-[11px]', oznakaPomeranja(p).boja)}>
            <span className="whitespace-nowrap font-semibold">{p.broj}</span> {oznakaPomeranja(p).tekst} {formatirajDatum(p.planiraniDatum)}
          </p>
        ))}
        {s.odsutni.length > 0 && <p className="truncate text-[11px] text-gray-500">odsutni: {s.odsutni.map((o) => o.ime).join(', ')}</p>}
      </div>
    </div>
  );
}

/** Kartica pregleda (R3.23) sa „Zatraži prebacivanje“ i „Promeni u rasporedu“. */
export function PregledKartica({ pregled, onClose }: { pregled: PregledKalendarDto; onClose: () => void }) {
  const { me } = useAuth();
  const pristup = me ?? { roles: [], prava: [] };
  const [mod, setMod] = useState<'kartica' | 'prebacivanje' | 'izmena' | 'ucesnik' | 'otkazivanje' | 'tip'>('kartica');
  // Posle izmene koja je ostavila dan bez DP-a usred niza nudi se „Povuci raspored“ (zamerka 29).
  const [slobodan, setSlobodan] = useState<string | null>(null);
  const navigate = useNavigate();
  const moj = me?.user.id === pregled.dodeljenId || pregled.ucesnici.some((u) => u.korisnikId === me?.user.id);
  const planiran = pregled.status === 'planiran';
  const smeRaspored = imaPravo(pristup, 'raspored.uredjivanje');
  const posleIzmene = (r?: PregledKalendarDto) => (r?.slobodanDan ? setSlobodan(r.slobodanDan) : onClose());
  const zahtevi = useZahteviPrebacivanja('zahtev', moj && !!pregled.zahtevNaCekanju);
  const mojZahtev = zahtevi.data?.find((z) => z.pregled.id === pregled.id && z.podneoId === me?.user.id);
  const otkaziZahtev = useOtkaziZahtevPrebacivanja();

  if (slobodan) return <PovlacenjeDijalog datum={slobodan} pz={!pregled.tip.startsWith('DP')} onClose={onClose} />;

  return (
    <Modal open title={pregled.broj} onClose={onClose}>
      {mod === 'kartica' && (
        <div className="space-y-3 text-sm">
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
            <dt className="text-gray-500">Datum</dt>
            <dd>{formatirajDatum(pregled.planiraniDatum)}{pregled.prvobitniDatum !== pregled.planiraniDatum && ` (${pregled.propusten ? 'prenet' : 'pomeren'} sa ${formatirajDatum(pregled.prvobitniDatum)})`}</dd>
            <dt className="text-gray-500">Inženjer</dt>
            <dd>{pregled.dodeljen}</dd>
            {pregled.ucesnici.map((u) => (
              <Fragment key={u.korisnikId}>
                <dt className="text-gray-500">Drugi inženjer</dt>
                <dd>{u.ime} (sekcije {u.sekcije.join(', ')})</dd>
              </Fragment>
            ))}
            <dt className="text-gray-500">Status</dt>
            <dd>{STATUS_LABELA[pregled.status]}{oznakaPregleda(pregled) && <span className={cn('ml-2 font-semibold', oznakaPregleda(pregled)!.boja)}>· {oznakaPregleda(pregled)!.tekst}</span>}</dd>
            {pregled.napomena && (<><dt className="text-gray-500">Napomena</dt><dd className="break-words">{pregled.napomena}</dd></>)}
          </dl>
          <div className="flex flex-wrap justify-end gap-2 pt-2">
            {moj && (planiran || pregled.status === 'u_radu' || pregled.status === 'vracen_na_doradu') && imaPravo(pristup, 'pregledi.popunjavanje') && (
              <Button onClick={() => navigate(`/pregled/${pregled.id}/popuni`)}>Popuni pregled</Button>
            )}
            {moj && planiran && imaPravo(pristup, 'pregledi.popunjavanje') && (pregled.zahtevNaCekanju
              ? (
                <span className="flex items-center gap-2 self-center text-sm font-semibold text-amber-800">
                  Zahtev poslat — čeka odluku
                  {mojZahtev && <OtkaziZahtev opis="zahtev za prebacivanje" otkazi={() => otkaziZahtev.mutateAsync(mojZahtev.id)} />}
                </span>
              )
              : <Button variant="secondary" onClick={() => setMod('prebacivanje')}>Zatraži prebacivanje</Button>)}
            {smeRaspored && (planiran || pregled.status === 'u_radu') && (
              <Button variant="secondary" onClick={() => setMod('izmena')}>Promeni u rasporedu</Button>
            )}
            {smeRaspored && pregled.tip.startsWith('PZ') && (planiran || pregled.status === 'u_radu') && (
              <Button variant="secondary" onClick={() => setMod('ucesnik')}>{pregled.ucesnici.length ? 'Drugi inženjer' : 'Dodaj inženjera'}</Button>
            )}
            {smeRaspored && planiran && pregled.tip.startsWith('DP') && (
              <Button variant="secondary" onClick={() => setMod('tip')}>Promeni DP</Button>
            )}
            {smeRaspored && planiran && <Button variant="ghost" onClick={() => setMod('otkazivanje')}>Otkaži pregled</Button>}
            {pregled.status !== 'planiran' && <Button variant="secondary" onClick={() => navigate(`/pregledi/${pregled.id}`)}>Otvori pregled</Button>}
            <Button variant="ghost" onClick={onClose}>Zatvori</Button>
          </div>
        </div>
      )}
      {mod === 'prebacivanje' && <PrebacivanjeForma pregled={pregled} onDone={onClose} onBack={() => setMod('kartica')} />}
      {mod === 'izmena' && <IzmenaForma pregled={pregled} onDone={posleIzmene} onBack={() => setMod('kartica')} />}
      {mod === 'ucesnik' && <UcesnikForma pregled={pregled} onDone={onClose} onBack={() => setMod('kartica')} />}
      {mod === 'otkazivanje' && <OtkazivanjeForma pregled={pregled} onDone={posleIzmene} onBack={() => setMod('kartica')} />}
      {mod === 'tip' && <TipDpForma pregled={pregled} onDone={onClose} onBack={() => setMod('kartica')} />}
    </Modal>
  );
}

function PrebacivanjeForma({ pregled, onDone, onBack }: { pregled: PregledKalendarDto; onDone: () => void; onBack: () => void }) {
  const inzenjeri = useInzenjeri();
  const podnesi = usePodnesiPrebacivanje();
  const toast = useToast();
  const [razlog, setRazlog] = useState('');
  const [zamena, setZamena] = useState<string | null>(null);
  const [greska, setGreska] = useState<string | null>(null);

  async function potvrdi(e: FormEvent) {
    e.preventDefault();
    try {
      await podnesi.mutateAsync({ pregledId: pregled.id, razlog: razlog.trim(), predlozenaZamenaId: zamena });
      toast.success('Zahtev za prebacivanje je poslat.');
      onDone();
    } catch (err) {
      setGreska(porukaGreske(err, 'Zahtev nije poslat.'));
    }
  }

  return (
    <form onSubmit={potvrdi} className="space-y-4" noValidate>
      <FormField label="Razlog" htmlFor="preb-razlog">
        <Textarea id="preb-razlog" value={razlog} onChange={(e) => setRazlog(e.target.value)} rows={3} />
      </FormField>
      <FormField label="Predlog zamene (nije obavezno)" htmlFor="preb-zamena">
        <Select
          id="preb-zamena" value={zamena} nullable placeholder="Bez predloga" onChange={setZamena}
          options={(inzenjeri.data ?? []).filter((i) => i.id !== pregled.dodeljenId).map((i) => ({ value: i.id, label: i.ime }))}
        />
      </FormField>
      {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onBack}>Nazad</Button>
        <Button type="submit" disabled={podnesi.isPending}>{podnesi.isPending ? 'Slanje…' : 'Pošalji zahtev'}</Button>
      </div>
    </form>
  );
}

export function IzmenaForma({ pregled, onDone, onBack }: { pregled: PregledKalendarDto; onDone: (r?: PregledKalendarDto) => void; onBack: () => void }) {
  const inzenjeri = useInzenjeri();
  const izmeni = useIzmeniDodelu();
  const toast = useToast();
  const [inz, setInz] = useState<string | null>(pregled.dodeljenId);
  const [datum, setDatum] = useState(pregled.planiraniDatum);
  const [greska, setGreska] = useState<string | null>(null);
  const dp = useDpKojiSePomera(!pregled.tip.startsWith('DP') && datum !== pregled.planiraniDatum ? datum : null);

  async function potvrdi(e: FormEvent) {
    e.preventDefault();
    try {
      const r = await izmeni.mutateAsync({
        id: pregled.id,
        inzenjerId: inz !== pregled.dodeljenId ? inz : null,
        datum: datum !== pregled.planiraniDatum ? datum : null,
      });
      toast.success('Raspored je izmenjen.');
      onDone(r);
    } catch (err) {
      setGreska(porukaGreske(err, 'Izmena nije sačuvana.'));
    }
  }

  return (
    <form onSubmit={potvrdi} className="space-y-4" noValidate>
      <FormField label="Inženjer" htmlFor="izm-inz">
        <Select id="izm-inz" value={inz} onChange={setInz} options={(inzenjeri.data ?? []).map((i) => ({ value: i.id, label: i.ime }))} />
      </FormField>
      <FormField label="Datum" htmlFor="izm-datum" hint={pregled.status === 'u_radu' ? 'Započetom pregledu se menja samo inženjer.' : undefined}>
        <DatePicker id="izm-datum" value={datum} onChange={setDatum} min={danasYmd()} clearable={false} disabled={pregled.status !== 'planiran'} />
      </FormField>
      {dp && <UpozorenjeDp dp={dp} radnja="pomerite" />}
      {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onBack}>Nazad</Button>
        <Button type="submit" disabled={izmeni.isPending}>{izmeni.isPending ? 'Čuvanje…' : dp ? 'Sačuvaj i pomeri DP-ove' : 'Sačuvaj'}</Button>
      </div>
    </form>
  );
}

/**
 * Drugi inženjer na PZ-u (R3.14): bira se inženjer i njegove sekcije („Označi sve sekcije“); ostale sekcije radi dodeljeni.
 * Svako upisuje samo u svoje sekcije, a predati može bilo ko od njih kad su sve gotove (R3.15).
 */
export function UcesnikForma({ pregled, onDone, onBack }: { pregled: PregledKalendarDto; onDone: () => void; onBack: () => void }) {
  const inzenjeri = useInzenjeri();
  const sablon = useSablonVerzija(pregled.tip);
  const postavi = usePostaviUcesnika();
  const toast = useToast();
  const postojeci = pregled.ucesnici[0];
  const [inz, setInz] = useState<string | null>(postojeci?.korisnikId ?? null);
  const [sekcije, setSekcije] = useState<string[]>(postojeci?.sekcije ?? []);
  const [greska, setGreska] = useState<string | null>(null);
  const sve = (sablon.data?.sekcije ?? []).map((s) => s.sifra);

  function prekidac(s: string) {
    setSekcije((x) => (x.includes(s) ? x.filter((y) => y !== s) : [...x, s]));
  }

  async function sacuvaj(korisnikId: string, izabrane: string[], poruka: string) {
    try {
      await postavi.mutateAsync({ id: pregled.id, korisnikId, sekcije: izabrane });
      toast.success(poruka);
      onDone();
    } catch (err) {
      setGreska(porukaGreske(err, 'Izmena nije sačuvana.'));
    }
  }

  function potvrdi(e: FormEvent) {
    e.preventDefault();
    if (!inz) return setGreska('Izaberite inženjera.');
    if (sekcije.length === 0) return setGreska('Označite bar jednu sekciju.');
    if (sekcije.length === sve.length) return setGreska('Ostavite bar jednu sekciju inženjeru kome je PZ dodeljen.');
    void sacuvaj(inz, sve.filter((s) => sekcije.includes(s)), postojeci ? 'Sekcije su izmenjene.' : 'Inženjer je dodat.');
  }

  return (
    <form onSubmit={potvrdi} className="space-y-4" noValidate>
      <p className="text-sm text-gray-600">{pregled.dodeljen} radi sekcije koje ovde ne označite. Svako upisuje samo u svoje sekcije.</p>
      <FormField label="Drugi inženjer" htmlFor="uc-inz">
        <Select id="uc-inz" value={inz} onChange={setInz} placeholder="Izaberite inženjera" disabled={!!postojeci}
          options={(inzenjeri.data ?? []).filter((i) => i.id !== pregled.dodeljenId).map((i) => ({ value: i.id, label: i.ime }))} />
      </FormField>
      <fieldset className="space-y-2">
        <legend className="mb-1 text-sm font-medium text-gray-800">Njegove sekcije</legend>
        {sablon.isLoading ? <Ucitavanje /> : (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            {sve.map((s) => <Checkbox key={s} label={`Sekcija ${s}`} checked={sekcije.includes(s)} onChange={() => prekidac(s)} />)}
          </div>
        )}
        <Button type="button" size="sm" variant="ghost" onClick={() => setSekcije(sve)}>
          Označi sve sekcije
        </Button>
      </fieldset>
      {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
      <div className="flex flex-wrap justify-end gap-2">
        {postojeci && (
          <Button type="button" variant="danger" disabled={postavi.isPending}
            onClick={() => void sacuvaj(postojeci.korisnikId, [], `${postojeci.ime} više ne radi ovaj PZ.`)}>Ukloni inženjera</Button>
        )}
        <Button type="button" variant="ghost" onClick={onBack}>Nazad</Button>
        <Button type="submit" disabled={postavi.isPending}>{postavi.isPending ? 'Čuvanje…' : 'Sačuvaj'}</Button>
      </div>
    </form>
  );
}
