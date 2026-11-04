import { useRef, useState } from 'react';
import { UputstvoDugme } from '../sabloni/UputstvoDijalog';
import { useNavigate, useParams } from 'react-router-dom';
import { Camera, Check, ImagePlus, MessageSquare, Wrench } from 'lucide-react';
import { Modal } from '../../components/ui/Modal';
import { Select } from '../../components/ui/Select';
import { HITNOSTI, VRSTE_AKCIJA } from '../akcije/oznake';
import { useToast } from '../../app/providers/ToastProvider';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody } from '../../components/ui/Card';
import { Checkbox } from '../../components/ui/Checkbox';
import { Input } from '../../components/ui/Input';
import { PageHeader } from '../../components/ui/PageHeader';
import { Ucitavanje } from '../../components/ui/Spinner';
import { Textarea } from '../../components/ui/Textarea';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { formatirajDatum, formatirajVreme } from '../../lib/datum';
import { nedostaje } from '../../offline/lokalno';
import { useStanjeSinhronizacije } from '../../offline/sinhronizacija';
import { smanjiSliku } from '../../lib/slika';
import {
  useOperacija, useOtpremiSliku, usePopunjavanje, useZapocni,
  type OdgovorDto, type Operacija, type PopunjavanjeDto, type StavkaPopunjavanjaDto, type UredjajPopunjavanjaDto,
} from './hooks';

/** Koliko je stavki popunjeno i koliko treba (uređaji van funkcije se ne računaju). */
function napredak(p: PopunjavanjeDto, sekcija?: string) {
  let gotovo = 0;
  let ukupno = 0;
  for (const s of p.sekcije.filter((x) => !sekcija || x.sifra === sekcija))
    for (const u of s.uredjaji.filter((x) => !x.vanFunkcije))
      for (const st of u.stavke) {
        ukupno++;
        if (st.odgovor) gotovo++;
      }
  return { gotovo, ukupno };
}

/**
 * Popunjavanje pregleda (R4.1–R4.6). Telefon: jedna sekcija u fokusu, velika dugmad (≥ 44 px). Svaka radnja se odmah šalje
 * kao operacija; posle odgovora server vraća stanje. „Nastavi gde si stao“ skroluje do prve nepopunjene stavke (R4.2).
 */
export function PopunjavanjePage() {
  const id = Number(useParams().id);
  const navigate = useNavigate();
  const toast = useToast();
  const pregled = usePopunjavanje(id);
  const zapocni = useZapocni(id);
  const operacija = useOperacija(id);
  const [sekcija, setSekcija] = useState<string | null>(null);
  const [nastavak, setNastavak] = useState<'pitaj' | 'gotovo'>('pitaj');

  async function posalji(op: Operacija, uspeh?: string) {
    try {
      await operacija.mutateAsync(op);
      if (uspeh) toast.success(uspeh);
      return true;
    } catch (err) {
      toast.error(err instanceof Error && !('response' in err) ? err.message : porukaGreske(err, 'Promena nije sačuvana.'));
      return false;
    }
  }

  if (pregled.isLoading) return <Ucitavanje />;
  if (pregled.isError || !pregled.data)
    return <p role="alert" className="text-sm text-red-600">{porukaGreske(pregled.error, 'Pregled nije učitan.')}</p>;
  const p = pregled.data;

  if (p.status === 'planiran')
    return (
      <div className="space-y-4">
        <PageHeader title={p.broj} description={`Planiran za ${formatirajDatum(p.planiraniDatum)}.`} />
        <Card>
          <CardBody className="space-y-3">
            <p className="text-sm text-gray-700">Datum i vreme početka upisuju se automatski i ne mogu da se menjaju.</p>
            <Button className="min-h-11 w-full sm:w-auto" onClick={() => void zapocni.mutateAsync(p.verzijaId).catch((e) => toast.error(porukaGreske(e, 'Pregled nije započet.')))} disabled={zapocni.isPending}>
              Započni pregled
            </Button>
          </CardBody>
        </Card>
      </div>
    );

  if (p.status === 'predato_ceka')
    return <PageHeader title={p.broj} description="Predato – čeka slanje. Pregled se šalje sam čim se veza vrati i do tada se ne menja." />;
  if (p.status !== 'u_radu' && p.status !== 'vracen_na_doradu')
    return <PageHeader title={p.broj} description="Pregled je predat; menja se samo kroz doradu." />;

  async function predaj() {
    const n = nedostaje(p);
    if (n.odgovora > 0 || n.napomena > 0) {
      toast.error(`Pregled još nije potpun: nedostaje ${n.odgovora} odgovora i ${n.napomena} obaveznih napomena.`);
      return;
    }
    if (await posalji({ vrsta: 'predaja', podaci: {} }, navigator.onLine ? 'Pregled je predat.' : 'Predato – čeka slanje.')) navigate('/kalendar');
  }

  const ukupno = napredak(p);
  const aktivna = sekcija ?? p.sekcije.find((s) => s.moja)?.sifra ?? p.sekcije[0]?.sifra;
  const sek = p.sekcije.find((s) => s.sifra === aktivna);

  return (
    <div className="space-y-4">
      <PageHeader
        title={p.broj}
        description={`Popunjeno ${ukupno.gotovo} od ${ukupno.ukupno} stavki`}
        actions={
          <Button className="min-h-11" disabled={operacija.isPending} onClick={() => void predaj()}>
            Predaj pregled
          </Button>
        }
      />

      <StanjeSlanja />
      {pregled.odbijene.length > 0 && (
        <div role="alert" className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800">
          Server nije prihvatio neke promene: {[...new Set(pregled.odbijene.map((o) => o.razlog))].join(' ')}
        </div>
      )}

      {nastavak === 'pitaj' && ukupno.gotovo > 0 && ukupno.gotovo < ukupno.ukupno && (
        <Card>
          <CardBody className="flex flex-wrap items-center justify-between gap-2">
            <span className="text-sm">Pregled je započet ranije.</span>
            <div className="flex gap-2">
              <Button variant="secondary" onClick={() => { setSekcija(prvaNepopunjena(p)?.sekcija ?? null); setNastavak('gotovo'); setTimeout(() => skrolujDoPrve(), 50); }}>
                Nastavi gde si stao
              </Button>
              <Button variant="ghost" onClick={() => { setSekcija(p.sekcije[0]?.sifra ?? null); setNastavak('gotovo'); }}>Kreni od početka</Button>
            </div>
          </CardBody>
        </Card>
      )}

      {/* Trake sekcija prelaze u novi red umesto da beže van ekrana (spec §8.1). */}
      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Sekcije">
        {p.sekcije.map((s) => {
          const n = napredak(p, s.sifra);
          return (
            <button key={s.sifra} type="button" role="tab" aria-selected={s.sifra === aktivna} onClick={() => setSekcija(s.sifra)}
              className={cn('min-h-11 rounded-md border px-3 text-sm', s.sifra === aktivna ? 'border-primary-500 bg-primary-50 text-primary-800' : 'border-gray-200 bg-white',
                n.gotovo === n.ukupno && 'border-green-300')}>
              {s.sifra} <span className="text-xs text-gray-500">{n.gotovo}/{n.ukupno}</span>
            </button>
          );
        })}
      </div>

      {sek && (
        <div className="space-y-3">
          {!sek.moja && <p className="text-sm text-gray-600">Ovu sekciju radi {sek.radi}; vidite njen napredak, ali ne upisujete u nju.</p>}
          {sek.moja && (
            <Button variant="secondary" className="min-h-11 w-full sm:w-auto" disabled={operacija.isPending}
              onClick={() => void posalji({ vrsta: 'sve_ispravno', podaci: { sekcija: sek.sifra } }, 'Neoznačene stavke su označene kao ispravne.')}>
              <Check size={16} aria-hidden /> Sve neoznačeno u sekciji je ispravno
            </Button>
          )}
          {sek.uredjaji.map((u) => (
            <UredjajKartica key={u.uredjajId} pregledId={p.id} uredjaj={u} moja={sek.moja} posalji={posalji} />
          ))}
        </div>
      )}
    </div>
  );
}

/** „Poslednja sinhronizacija: HH:MM“ i šta čeka slanje (R10.3, R10.4). */
function StanjeSlanja() {
  const st = useStanjeSinhronizacije();
  return (
    <p className="text-xs text-gray-500">
      {st.poslednja ? `Poslednja sinhronizacija: ${formatirajVreme(st.poslednja)}` : 'Još nije sinhronizovano.'}
      {(st.promena > 0 || st.slika > 0) && ` · čeka slanje: ${st.promena} promena, ${st.slika} slika`}
    </p>
  );
}

function prvaNepopunjena(p: PopunjavanjeDto) {
  for (const s of p.sekcije.filter((x) => x.moja))
    for (const u of s.uredjaji.filter((x) => !x.vanFunkcije))
      for (const st of u.stavke) if (!st.odgovor) return { sekcija: s.sifra, stavkaId: st.stavkaId };
  return null;
}

function skrolujDoPrve() {
  document.querySelector('[data-nepopunjena="true"]')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

type Posalji = (op: Operacija, uspeh?: string) => Promise<boolean>;

function UredjajKartica({ pregledId, uredjaj, moja, posalji }: { pregledId: number; uredjaj: UredjajPopunjavanjaDto; moja: boolean; posalji: Posalji }) {
  const van = (v: boolean) => void posalji({ vrsta: 'van_funkcije', podaci: { uredjajId: uredjaj.uredjajId, vanFunkcije: v } });
  return (
    <Card>
      <CardBody className="space-y-3">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <p className="min-w-0 break-words font-medium text-gray-900">
            {uredjaj.naziv} {uredjaj.oznaka && <span className="whitespace-nowrap text-gray-600">{uredjaj.oznaka}</span>}
          </p>
          <Checkbox label="Nije u funkciji" checked={uredjaj.vanFunkcije} disabled={!moja} onChange={(e) => van(e.target.checked)} />
        </div>
        {moja && uredjaj.otvoreneAkcije.map((a) => (
          <PopUpAkcije key={a.id} akcija={a} uredjaj={uredjaj} posalji={posalji} />
        ))}
        {uredjaj.prosliPutVanFunkcije && !uredjaj.vanFunkcije && (
          <div className="flex flex-wrap items-center gap-2 rounded-md bg-amber-50 p-2 text-sm text-amber-900">
            Prošli put: nije u funkciji
            {moja && <Button size="sm" variant="secondary" onClick={() => van(true)}>I dalje nije u funkciji</Button>}
          </div>
        )}
        {uredjaj.vanFunkcije ? (
          <p className="text-sm text-gray-500">Uređaj nije u funkciji na ovom pregledu; aktivnosti se ne popunjavaju.</p>
        ) : (
          <ul className="space-y-3">
            {uredjaj.stavke.map((s) => (
              // Ključ uključuje stanje sa servera: posle „sve ispravno“ ili tuđe izmene lokalni nacrt kreće od nove vrednosti.
              <StavkaRed key={`${s.stavkaId}-${s.odgovor?.vrednost}-${s.odgovor?.napomena}-${JSON.stringify(s.odgovor?.polja)}`}
                pregledId={pregledId} stavka={s} moja={moja} posalji={posalji} />
            ))}
          </ul>
        )}
      </CardBody>
    </Card>
  );
}

/** Jedna aktivnost: dva odgovora, napomena (obavezna uz neispravan, R4.4), dodatna polja (R2.11) i slike (R4.5). */
export function StavkaRed({ pregledId, stavka, moja, posalji }: { pregledId: number; stavka: StavkaPopunjavanjaDto; moja: boolean; posalji: Posalji }) {
  const o = stavka.odgovor;
  const [napomena, setNapomena] = useState(o?.napomena ?? '');
  const [napomenaOtvorena, setNapomenaOtvorena] = useState(false);
  const [akcijaOtvorena, setAkcijaOtvorena] = useState(false);
  const [polja, setPolja] = useState<OdgovorDto['polja']>(o?.polja ?? {});
  const otpremi = useOtpremiSliku(pregledId);
  const toast = useToast();
  const kamera = useRef<HTMLInputElement>(null);
  const galerija = useRef<HTMLInputElement>(null);

  const odgovori = (vrednost: 0 | 1, izmene?: { napomena?: string; polja?: OdgovorDto['polja'] }) =>
    void posalji({
      vrsta: 'odgovor',
      podaci: { stavkaId: stavka.stavkaId, vrednost, napomena: (izmene?.napomena ?? napomena).trim() || null, polja: izmene?.polja ?? polja },
    });

  const neispravan = o?.vrednost === 1;
  const vidljivaPolja = stavka.polja.filter((p) => p.prikaz === 'uvek' || neispravan);

  async function slika(fajl: File | undefined, izvor: 'kamera' | 'galerija') {
    if (!fajl) return;
    try {
      await otpremi.mutateAsync({ stavkaId: stavka.stavkaId, slika: await smanjiSliku(fajl), izvor });
      toast.success('Slika je dodata.');
    } catch (err) {
      toast.error(porukaGreske(err, 'Slika nije sačuvana.'));
    }
  }

  return (
    <li className="space-y-2 border-t border-gray-100 pt-3 first:border-t-0 first:pt-0" data-nepopunjena={!o}>
      <p className="flex items-start gap-1 break-words text-sm text-gray-900">
        <span className="min-w-0 flex-1">{stavka.aktivnost}</span>
        <UputstvoDugme aktivnostId={stavka.aktivnostId} aktivnost={stavka.aktivnost} smeDaMenja={false} offlineTekst={stavka.uputstvo} />
      </p>
      <div className="grid grid-cols-2 gap-2">
        {([0, 1] as const).map((v) => (
          <button key={v} type="button" disabled={!moja} onClick={() => odgovori(v)} aria-pressed={o?.vrednost === v}
            className={cn('min-h-11 rounded-md border px-2 text-sm font-medium disabled:opacity-60',
              o?.vrednost === v ? (v === 0 ? 'border-green-500 bg-green-50 text-green-800' : 'border-red-500 bg-red-50 text-red-800') : 'border-gray-300 bg-white text-gray-800')}>
            {v === 0 ? stavka.odgovorIspravan : stavka.odgovorNeispravan}
          </button>
        ))}
      </div>

      {vidljivaPolja.map((p) => (
        <PoljeUnos key={p.id} polje={p} vrednost={polja[String(p.id)]} disabled={!moja}
          onChange={(v) => setPolja({ ...polja, [String(p.id)]: v })}
          onBlur={() => o && odgovori(o.vrednost)} />
      ))}

      {(neispravan || napomenaOtvorena || napomena) && (
        <Textarea aria-label={`Napomena — ${stavka.aktivnost}`} rows={2} value={napomena} disabled={!moja}
          placeholder={neispravan ? 'Napomena je obavezna uz neispravan odgovor.' : 'Napomena'}
          className={cn(neispravan && !napomena.trim() && 'border-red-400')}
          onChange={(e) => setNapomena(e.target.value)} onBlur={() => o && napomena !== (o.napomena ?? '') && odgovori(o.vrednost)} />
      )}

      {moja && (
        <div className="flex flex-wrap items-center gap-1">
          {!neispravan && !napomenaOtvorena && !napomena && (
            <Button size="sm" variant="ghost" onClick={() => setNapomenaOtvorena(true)}><MessageSquare size={14} aria-hidden /> Napomena</Button>
          )}
          <Button size="sm" variant="ghost" onClick={() => setAkcijaOtvorena(true)}><Wrench size={14} aria-hidden /> Akcija</Button>
          <Button size="sm" variant="ghost" onClick={() => kamera.current?.click()} disabled={otpremi.isPending}><Camera size={14} aria-hidden /> Fotografiši</Button>
          <Button size="sm" variant="ghost" onClick={() => galerija.current?.click()} disabled={otpremi.isPending}><ImagePlus size={14} aria-hidden /> Iz galerije</Button>
          {stavka.fotografije.length > 0 && <Badge tone="primary">{stavka.fotografije.length} {stavka.fotografije.length === 1 ? 'slika' : 'slike'}</Badge>}
          <input ref={kamera} type="file" accept="image/*" capture="environment" className="hidden" aria-label="Fotografiši"
            onChange={(e) => { void slika(e.target.files?.[0], 'kamera'); e.target.value = ''; }} />
          <input ref={galerija} type="file" accept="image/*" className="hidden" aria-label="Iz galerije"
            onChange={(e) => { void slika(e.target.files?.[0], 'galerija'); e.target.value = ''; }} />
        </div>
      )}
      {akcijaOtvorena && <NovaAkcija stavka={stavka} posalji={posalji} onClose={() => setAkcijaOtvorena(false)} />}
    </li>
  );
}

/** Nova potrebna akcija na stavci (R7.1, R7.2): vrsta, hitnost, kratak opis (opciono). */
function NovaAkcija({ stavka, posalji, onClose }: { stavka: StavkaPopunjavanjaDto; posalji: Posalji; onClose: () => void }) {
  const [vrsta, setVrsta] = useState<string | null>('popraviti');
  const [hitnost, setHitnost] = useState<string | null>('7_dana');
  const [opis, setOpis] = useState('');
  return (
    <Modal open title="Nova akcija" onClose={onClose}>
      <div className="space-y-4">
        <p className="break-words text-sm text-gray-600">{stavka.aktivnost}</p>
        <Select aria-label="Vrsta" value={vrsta} onChange={setVrsta} options={VRSTE_AKCIJA} />
        <Select aria-label="Hitnost" value={hitnost} onChange={setHitnost} options={HITNOSTI} />
        <Textarea aria-label="Opis (nije obavezno)" placeholder="Opis (nije obavezno)" rows={2} value={opis} onChange={(e) => setOpis(e.target.value)} />
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>Odustani</Button>
          <Button onClick={() => void posalji({ vrsta: 'akcija', podaci: { stavkaId: stavka.stavkaId, vrsta: vrsta ?? 'ostalo', hitnost: hitnost ?? 'pratiti', opis: opis.trim() || null } },
            'Akcija je otvorena.').then((ok) => ok && onClose())}>Otvori akciju</Button>
        </div>
      </div>
    </Modal>
  );
}

/**
 * Pop-up otvorene akcije na uređaju (R4.7, R7.4): da li je urađena. Ako je urađena, a odgovor na toj stavci je neispravan,
 * upozorava i nudi novu akciju (R7.14) — kvar ostaje otvoren.
 */
function PopUpAkcije({ akcija, uredjaj, posalji }: { akcija: UredjajPopunjavanjaDto['otvoreneAkcije'][number]; uredjaj: UredjajPopunjavanjaDto; posalji: Posalji }) {
  const stavka = uredjaj.stavke.find((s) => s.stavkaId === akcija.stavkaId);
  const neispravna = stavka?.odgovor?.vrednost === 1;
  return (
    <div role="alertdialog" aria-label={`Akcija ${akcija.broj}`} className="space-y-2 rounded-md border border-blue-200 bg-blue-50 p-3 text-sm">
      <p className="break-words">
        <span className="whitespace-nowrap font-semibold">{akcija.broj}</span> · {akcija.aktivnost} · {VRSTE_AKCIJA.find((v) => v.value === akcija.vrsta)?.label}
        {akcija.opis && ` — ${akcija.opis}`}
      </p>
      <p className="font-medium">Da li je akcija urađena?</p>
      {neispravna && <p className="text-amber-800">Stavka je označena kao neispravna; ako je akcija urađena, otvorite novu akciju za preostali kvar.</p>}
      <div className="flex gap-2">
        <Button size="sm" className="min-h-11" onClick={() => void posalji({ vrsta: 'akcija_odgovor', podaci: { akcijaId: akcija.id, uradjeno: true } }, 'Akcija je zatvorena.')}>Da</Button>
        <Button size="sm" variant="secondary" className="min-h-11" onClick={() => void posalji({ vrsta: 'akcija_odgovor', podaci: { akcijaId: akcija.id, uradjeno: false } }, 'Akcija ostaje otvorena.')}>Ne</Button>
      </div>
    </div>
  );
}

function PoljeUnos({ polje, vrednost, disabled, onChange, onBlur }: {
  polje: StavkaPopunjavanjaDto['polja'][number]; vrednost: number | number[] | boolean | undefined; disabled: boolean;
  onChange: (v: number | number[] | boolean) => void; onBlur: () => void;
}) {
  const oznaka = `${polje.naziv}${polje.jedinica ? ` (${polje.jedinica})` : ''}${polje.obavezno ? '' : ' — nije obavezno'}`;
  if (polje.tip === 'da_ne')
    return <Checkbox label={oznaka} checked={vrednost === true} disabled={disabled} onChange={(e) => { onChange(e.target.checked); onBlur(); }} />;
  if (polje.tip === 'dve_vrednosti') {
    const niz = Array.isArray(vrednost) ? vrednost : [];
    return (
      <fieldset className="space-y-1">
        <legend className="text-xs text-gray-600">{oznaka}</legend>
        <div className="grid gap-2 sm:grid-cols-2">
          {polje.oznake.map((o, i) => (
            <Input key={o} type="number" inputMode="decimal" aria-label={`${polje.naziv} — ${o}`} placeholder={o} disabled={disabled}
              value={niz[i] ?? ''} onBlur={onBlur}
              onChange={(e) => { const n = [...niz]; n[i] = Number(e.target.value); onChange(n); }} />
          ))}
        </div>
      </fieldset>
    );
  }
  return (
    <label className="block text-xs text-gray-600">
      {oznaka}
      <Input type="number" inputMode="numeric" min={0} disabled={disabled} value={typeof vrednost === 'number' ? vrednost : ''}
        onChange={(e) => onChange(Number(e.target.value))} onBlur={onBlur} />
    </label>
  );
}
