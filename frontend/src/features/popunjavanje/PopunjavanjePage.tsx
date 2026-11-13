import { useEffect, useRef, useState } from 'react';
import { UputstvoDugme } from '../sabloni/UputstvoDijalog';
import { useNavigate, useParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { Camera, Check, ImagePlus, MessageSquare, Wrench } from 'lucide-react';
import { Modal } from '../../components/ui/Modal';
import { Select } from '../../components/ui/Select';
import { HITNOSTI, VRSTE_AKCIJA } from '../akcije/oznake';
import { useToast } from '../../app/providers/ToastProvider';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody } from '../../components/ui/Card';
import type React from 'react';
import { Checkbox } from '../../components/ui/Checkbox';
import { Input } from '../../components/ui/Input';
import { PageHeader } from '../../components/ui/PageHeader';
import { Ucitavanje } from '../../components/ui/Spinner';
import { FormField } from '../../components/ui/FormField';
import { Textarea } from '../../components/ui/Textarea';
import { porukaGreske } from '../../lib/apiError';
import { brojSlika } from '../../lib/broj';
import { cn } from '../../lib/cn';
import { danasYmd, formatirajDatum, formatirajVreme } from '../../lib/datum';
import { useZahteviPrebacivanja, useZatraziRanijiPocetak } from '../raspored/hooks';
import { nedostaje } from '../../offline/lokalno';
import { lokalnaSlika, obrisiSliku, useStanjeSinhronizacije, zatvoriOdbijene } from '../../offline/sinhronizacija';
import { api } from '../../api/client';
import { useAuth } from '../auth/AuthProvider';
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
  const qc = useQueryClient();
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
            {p.planiraniDatum > danasYmd() ? (
              <PreDatuma id={p.id} pz={p.tip.startsWith('PZ')} datum={p.planiraniDatum} />
            ) : (
              <>
                <p className="text-sm text-gray-700">Datum i vreme početka upisuju se automatski i ne mogu da se menjaju.</p>
                <Button className="w-full sm:w-auto" onClick={() => void zapocni.mutateAsync(p.verzijaId).catch((e) => toast.error(porukaGreske(e, 'Pregled nije započet.')))} disabled={zapocni.isPending}>
                  Započni pregled
                </Button>
              </>
            )}
          </CardBody>
        </Card>
      </div>
    );

  if (p.status === 'predato_ceka')
    return (
      <div className="space-y-3">
        <PageHeader title={p.broj} description="Predato – čeka slanje. Pregled se šalje sam čim se veza vrati i do tada se ne menja." />
        <OdbijenePromene pregledId={p.id} razlozi={pregled.odbijene.map((o) => o.razlog)} />
      </div>
    );
  if (p.status !== 'u_radu' && p.status !== 'vracen_na_doradu')
    return <PageHeader title={p.broj} description="Pregled je predat; menja se samo kroz doradu." />;

  async function predaj() {
    const n = nedostaje(p);
    if (n.odgovora > 0 || n.napomena > 0) {
      toast.error(`Pregled još nije potpun: nedostaje ${n.odgovora} odgovora i ${n.napomena} obaveznih napomena.`);
      return;
    }
    if (n.polja.length > 0) {
      toast.error(`Nedostaje ${n.polja.length} ${n.polja.length === 1 ? 'obavezno polje' : 'obaveznih polja'} (${n.polja.slice(0, 5).join('; ')}${n.polja.length > 5 ? '; …' : ''}).`);
      return;
    }
    if (await posalji({ vrsta: 'predaja', podaci: {} }, navigator.onLine ? 'Pregled je predat.' : 'Predato – čeka slanje.')) {
      // Kalendar, Početna i lista još drže stari status (keš 30 s); posle predaje sve se učitava iznova.
      void qc.invalidateQueries();
      navigate('/kalendar');
    }
  }

  const ukupno = napredak(p);
  const aktivna = sekcija ?? p.sekcije.find((s) => s.moja)?.sifra ?? p.sekcije[0]?.sifra;
  const sek = p.sekcije.find((s) => s.sifra === aktivna);

  return (
    <div className="space-y-2.5 pb-16 md:pb-0">
      {/* Zaglavlje pregleda po mockup-u (`.insp-head`): zelena traka sa brojem, statusom i automatskim podacima. */}
      <div className="rounded-[10px] bg-primary-500 px-3.5 py-3 text-white">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-[15px] font-bold">{p.broj} · {NAZIV_TIPA[p.tip] ?? p.tip}</h1>
          <span className="rounded-full bg-white px-2.5 py-0.5 text-xs font-semibold text-primary-500">{p.status === 'vracen_na_doradu' ? 'Dorada' : 'U radu'}</span>
        </div>
        <div className="mt-1 flex flex-wrap gap-x-3.5 gap-y-1 text-xs text-[#d7ecdf]">
          <span>Datum: {formatirajDatum(p.planiraniDatum)} (automatski)</span>
          {p.zapocetAt && <span>Početak: {formatirajVreme(p.zapocetAt)} (automatski)</span>}
          <span>Šablon: {p.tip}</span>
          <span>Popunjeno: {ukupno.gotovo} od {ukupno.ukupno}</span>
        </div>
      </div>

      <StanjeSlanja />
      <OdbijenePromene pregledId={p.id} razlozi={pregled.odbijene.map((o) => o.razlog)} />

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

      {/* Sekcije kao pilule (mockup `.chips`); prelaze u novi red umesto da beže van ekrana (spec §8.1). */}
      <div className="flex flex-wrap gap-1.5" role="tablist" aria-label="Sekcije">
        {p.sekcije.map((s) => {
          const n = napredak(p, s.sifra);
          const aktivan = s.sifra === aktivna;
          return (
            <button key={s.sifra} type="button" role="tab" aria-selected={aktivan} onClick={() => setSekcija(s.sifra)}
              className={cn('min-h-11 whitespace-nowrap rounded-full border px-3 text-xs md:min-h-8',
                aktivan ? 'border-primary-500 bg-primary-500 text-white' : n.ukupno > 0 && n.gotovo === n.ukupno ? 'border-accent-500 bg-white' : 'border-gray-200 bg-white')}>
              Sekcija {s.sifra}{!s.moja && ` · ${s.radi}`} · {n.gotovo}/{n.ukupno}
            </button>
          );
        })}
      </div>

      {sek && (
        <div className="space-y-2.5">
          {!sek.moja && <p className="rounded-lg bg-blue-50 px-3 py-2.5 text-[13px] text-blue-900">Ovu sekciju radi <b>{sek.radi}</b> na svom telefonu. Vidite njen napredak, ali ne upisujete u nju.</p>}
          {sek.moja && (
            <Button variant="secondary" className="w-full sm:w-auto" disabled={operacija.isPending}
              onClick={() => void posalji({ vrsta: 'sve_ispravno', podaci: { sekcija: sek.sifra } }, 'Neoznačene stavke su označene kao ispravne.')}>
              <Check size={16} aria-hidden /> Sve neoznačeno u sekciji je ispravno
            </Button>
          )}
          {sek.uredjaji.map((u) => (
            <UredjajKartica key={u.uredjajId} pregledId={p.id} uredjaj={u} moja={sek.moja} posalji={posalji} />
          ))}
          <NavigacijaSekcija p={p} aktivna={sek.sifra} idi={(sifra) => {
            setSekcija(sifra);
            window.scrollTo({ top: 0, behavior: 'smooth' });
            document.querySelector('main')?.scrollTo({ top: 0, behavior: 'smooth' });
          }} />
        </div>
      )}

      {/* Predaja je uvek pri ruci (mockup `.sticky`); na telefonu iznad donjeg menija. */}
      <div className="fixed inset-x-0 bottom-16 z-30 border-t border-gray-200 bg-gray-50 px-3 py-2.5 md:sticky md:bottom-0 md:z-10 md:px-0">
        <Button className="w-full md:w-auto" disabled={operacija.isPending} onClick={() => void predaj()}>
          Predaj pregled ({ukupno.gotovo}/{ukupno.ukupno})
        </Button>
      </div>
    </div>
  );
}

/**
 * Prethodna / sledeća sekcija na dnu sekcije (zamerka 7): na telefonu inženjer ne mora da skroluje do pilula na vrhu.
 * Kad je sekcija gotova, „Sledeća“ je istaknuta.
 */
export function NavigacijaSekcija({ p, aktivna, idi }: { p: PopunjavanjeDto; aktivna: string; idi: (sifra: string) => void }) {
  const i = p.sekcije.findIndex((x) => x.sifra === aktivna);
  const preth = p.sekcije[i - 1];
  const sled = p.sekcije[i + 1];
  if (!preth && !sled) return null;
  const n = napredak(p, aktivna);
  const gotova = n.ukupno > 0 && n.gotovo === n.ukupno;
  return (
    <div className="grid grid-cols-2 gap-2 pt-1">
      {preth ? <Button variant="ghost" onClick={() => idi(preth.sifra)}>← Sekcija {preth.sifra}</Button> : <span />}
      {sled ? <Button variant={gotova ? 'primary' : 'secondary'} onClick={() => idi(sled.sifra)}>Sekcija {sled.sifra} →</Button> : <span />}
    </div>
  );
}

/** Promene koje server nije prihvatio (zamerka 8): razlog je uvek vidljiv; lokalna kopija je već vraćena na stanje sa servera. */
function OdbijenePromene({ pregledId, razlozi }: { pregledId: number; razlozi: string[] }) {
  if (razlozi.length === 0) return null;
  const drugiUredjaj = razlozi.some((r) => r.includes('drugom telefonu'));
  return (
    <div role="alert" className="space-y-2 rounded-md border border-red-300 bg-red-50 p-3 text-sm text-red-800">
      <p className="font-semibold">Neke promene nisu sačuvane na serveru.</p>
      <ul className="list-disc space-y-0.5 pl-5">{[...new Set(razlozi)].map((r) => <li key={r}>{r}</li>)}</ul>
      <p>
        Pregled je vraćen na stanje sa servera.{' '}
        {drugiUredjaj
          ? 'Nastavite na uređaju na kome je pregled započet, ili zamolite Supervizora da ga otključa za ovaj uređaj, pa ponovo unesite odbijeno.'
          : 'Ispravite ono što je navedeno i pokušajte ponovo.'}
      </p>
      <div className="flex justify-end"><Button size="sm" variant="secondary" onClick={() => void zatvoriOdbijene(pregledId)}>U redu</Button></div>
    </div>
  );
}

const NAZIV_TIPA: Record<string, string> = {
  DP1: 'Dnevni pregled 1', DP2: 'Dnevni pregled 2', PZ1: 'Planski zastoj 1', PZ2: 'Planski zastoj 2', PZ3: 'Planski zastoj 3', PZ4: 'Planski zastoj 4',
};

/** „Poslednja sinhronizacija: HH:MM“ i šta čeka slanje (R10.3, R10.4). */
function StanjeSlanja() {
  const st = useStanjeSinhronizacije(useAuth().me?.user.id);
  return (
    <p className="text-xs text-gray-500">
      {st.promena > 0 || st.slika > 0 ? `⏳ Čeka slanje: ${st.promena} promena, ${st.slika} slika` : '✓ Sve je poslato'}
      {' · '}
      {st.poslednja ? `poslednja sinhronizacija: ${formatirajVreme(st.poslednja)}` : 'još nije sinhronizovano'}
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
  const gotovo = uredjaj.stavke.filter((s) => s.odgovor).length;
  return (
    // Kartica uređaja po mockup-u (`.dev`): zaglavlje sa nazivom i stanjem, aktivnosti ispod tanke linije.
    <section className={cn('overflow-hidden rounded-[10px] border border-gray-200 bg-white', uredjaj.vanFunkcije && 'opacity-75')}>
      <div className="flex flex-wrap items-center gap-2 px-3 py-2.5">
        <h3 className="min-w-0 flex-1 break-words font-bold text-gray-800">
          {uredjaj.naziv} {uredjaj.oznaka && <span className="whitespace-nowrap font-semibold text-gray-500">{uredjaj.oznaka}</span>}
        </h3>
        {uredjaj.vanFunkcije ? <Badge tone="gray">Nije u funkciji</Badge>
          : gotovo === uredjaj.stavke.length ? <Badge tone="green">✓</Badge>
          : <span className="text-xs text-gray-500">{gotovo}/{uredjaj.stavke.length}</span>}
        <Checkbox label="Nije u funkciji" checked={uredjaj.vanFunkcije} disabled={!moja} onChange={(e) => van(e.target.checked)} />
      </div>
      <div className="space-y-2 border-t border-gray-100 px-3 pb-1 pt-2">
        {moja && uredjaj.otvoreneAkcije.map((a) => (
          <PopUpAkcije key={a.id} akcija={a} uredjaj={uredjaj} posalji={posalji} />
        ))}
        {uredjaj.prosliPutVanFunkcije && !uredjaj.vanFunkcije && (
          <div className="flex flex-wrap items-center gap-2 rounded-lg bg-amber-50 px-3 py-2.5 text-[13px] text-amber-900">
            Prošli put: nije u funkciji
            {moja && <Button size="sm" variant="secondary" onClick={() => van(true)}>I dalje nije u funkciji</Button>}
          </div>
        )}
        {uredjaj.vanFunkcije ? (
          <p className="pb-2 text-[13px] text-gray-500">Uređaj nije u funkciji na ovom pregledu; aktivnosti se ne popunjavaju.</p>
        ) : (
          <ul>
            {uredjaj.stavke.map((s) => (
              // Ključ uključuje stanje sa servera: posle „sve ispravno“ ili tuđe izmene lokalni nacrt kreće od nove vrednosti.
              <StavkaRed key={`${s.stavkaId}-${s.odgovor?.vrednost}-${s.odgovor?.napomena}-${JSON.stringify(s.odgovor?.polja)}`}
                pregledId={pregledId} stavka={s} moja={moja} posalji={posalji} />
            ))}
          </ul>
        )}
      </div>
    </section>
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
    // Aktivnost po mockup-u (`.act`): tačkasta linija između aktivnosti, odgovori kao dva velika tastera (`.ans`), alatke ispod (`.tools`).
    <li className="space-y-2 border-b border-dashed border-[#e4e2df] py-2.5 last:border-b-0" data-nepopunjena={!o}>
      <p className="flex items-start gap-1 break-words text-sm text-gray-800">
        <span className="min-w-0 flex-1">{stavka.aktivnost}</span>
        <UputstvoDugme aktivnostId={stavka.aktivnostId} aktivnost={stavka.aktivnost} smeDaMenja={false} offlineTekst={stavka.uputstvo} />
      </p>
      <div className="grid grid-cols-2 gap-2">
        {([0, 1] as const).map((v) => (
          <button key={v} type="button" disabled={!moja} onClick={() => odgovori(v)} aria-pressed={o?.vrednost === v}
            className={cn('min-h-11 rounded-[9px] border-2 px-1.5 py-2 text-sm font-semibold transition-colors disabled:opacity-60',
              o?.vrednost === v ? (v === 0 ? 'border-green-500 bg-green-50 text-green-700' : 'border-red-500 bg-red-50 text-red-700') : 'border-gray-200 bg-white text-gray-800 hover:bg-gray-50')}>
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
        <div className="flex gap-1.5">
          <Alatka ima={!!napomena.trim()} treba={neispravan && !napomena.trim()} onClick={() => setNapomenaOtvorena((x) => !x)}>
            <MessageSquare size={14} aria-hidden /> Napomena
          </Alatka>
          <Alatka onClick={() => setAkcijaOtvorena(true)}><Wrench size={14} aria-hidden /> Akcija</Alatka>
          <Alatka ima={stavka.fotografije.length > 0} onClick={() => kamera.current?.click()} disabled={otpremi.isPending}>
            <Camera size={14} aria-hidden />
            <span className="flex flex-col leading-tight">
              <span>Fotografiši</span>
              {stavka.fotografije.length > 0 && <span className="text-[11px] font-normal">{brojSlika(stavka.fotografije.length)}</span>}
            </span>
          </Alatka>
          <Alatka onClick={() => galerija.current?.click()} disabled={otpremi.isPending}><ImagePlus size={14} aria-hidden /> Galerija</Alatka>
          <input ref={kamera} type="file" accept="image/*" capture="environment" className="hidden" aria-label="Fotografiši"
            onChange={(e) => { void slika(e.target.files?.[0], 'kamera'); e.target.value = ''; }} />
          <input ref={galerija} type="file" accept="image/*" className="hidden" aria-label="Iz galerije"
            onChange={(e) => { void slika(e.target.files?.[0], 'galerija'); e.target.value = ''; }} />
        </div>
      )}
      {stavka.fotografije.length > 0 && (
        <SlikeStavke pregledId={pregledId} stavkaId={stavka.stavkaId} ids={stavka.fotografije} stare={stavka.stareFotografije ?? []} moja={moja} />
      )}
      {akcijaOtvorena && <NovaAkcija stavka={stavka} posalji={posalji} onClose={() => setAkcijaOtvorena(false)} />}
    </li>
  );
}

/**
 * Sličice dodatih slika (zamerka 5). Dodir otvara sliku, a iz nje se briše (uz potvrdu) dok pregled nije predat.
 * Neposlata slika se čita sa telefona, poslata sa servera.
 */
function SlikeStavke({ pregledId, stavkaId, ids, stare, moja }: { pregledId: number; stavkaId: number; ids: number[]; stare: number[]; moja: boolean }) {
  const [otvorena, setOtvorena] = useState<{ id: number; src: string } | null>(null);
  const toast = useToast();
  function obrisi(id: number) {
    void obrisiSliku(pregledId, stavkaId, id)
      .then(() => { toast.success('Slika je obrisana.'); setOtvorena(null); })
      .catch((e) => toast.error(porukaGreske(e, 'Slika nije obrisana.')));
  }
  return (
    <div className="flex flex-wrap gap-2">
      {ids.map((id, i) => <Slicica key={id} id={id} redni={i + 1} onOtvori={(src) => setOtvorena({ id, src })} />)}
      {otvorena && (
        <Modal open title="Slika" onClose={() => setOtvorena(null)}>
          <img src={otvorena.src} alt="Slika uz stavku" className="mx-auto max-h-[60vh] rounded-lg object-contain" />
          <div className="flex justify-end gap-2 pt-4">
            {moja && !stare.includes(otvorena.id) && <Button variant="danger" onClick={() => obrisi(otvorena.id)}>Obriši sliku</Button>}
            {stare.includes(otvorena.id) && <span className="self-center text-xs text-gray-500">Slika iz predate verzije se ne briše.</span>}
            <Button variant="ghost" onClick={() => setOtvorena(null)}>Zatvori</Button>
          </div>
        </Modal>
      )}
    </div>
  );
}

/** Jedna sličica: neposlata (negativan ID) iz IndexedDB-a, poslata kroz API sa tokenom (img ne nosi Authorization). */
function Slicica({ id, redni, onOtvori }: { id: number; redni: number; onOtvori: (src: string) => void }) {
  const [src, setSrc] = useState<string | null>(null);
  useEffect(() => {
    let ziv = true;
    let url: string | null = null;
    const ucitaj: Promise<Blob | undefined> = id < 0
      ? lokalnaSlika(id)
      : api.get<Blob>(`/fotografije/${id}`, { responseType: 'blob' }).then((r) => r.data).catch(() => undefined);
    void ucitaj.then((b) => {
      if (b && ziv) {
        url = URL.createObjectURL(b);
        setSrc(url);
      }
    });
    return () => {
      ziv = false;
      if (url) URL.revokeObjectURL(url);
    };
  }, [id]);
  return (
    <button type="button" onClick={() => src && onOtvori(src)} aria-label={`Slika ${redni}`}
      className="h-16 w-16 overflow-hidden rounded-lg border border-gray-200 bg-gray-100 text-[11px] text-gray-500">
      {src ? <img src={src} alt="" className="h-full w-full object-cover" /> : 'slika'}
    </button>
  );
}

/** Alatka ispod aktivnosti (mockup `.tools button`): iste širine u redu; zelena kad je popunjena, crvena kad nešto nedostaje. */
function Alatka({ ima, treba, disabled, onClick, children }: { ima?: boolean; treba?: boolean; disabled?: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button type="button" disabled={disabled} onClick={onClick}
      className={cn('flex min-h-11 min-w-0 flex-1 items-center justify-center gap-1 whitespace-nowrap rounded-lg border px-1 text-xs transition-colors disabled:opacity-50 md:min-h-8 [&>svg]:shrink-0',
        treba ? 'border-red-500 text-red-700' : ima ? 'border-primary-500 font-semibold text-primary-500' : 'border-gray-200 bg-[#fafaf9] text-gray-800 hover:bg-gray-100')}>
      {children}
    </button>
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
    <div role="alertdialog" aria-label={`Akcija ${akcija.broj}`} className="space-y-2 rounded-lg bg-blue-50 px-3 py-2.5 text-[13px] text-blue-900">
      <p className="break-words">
        <span className="whitespace-nowrap font-semibold">{akcija.broj}</span> · {akcija.aktivnost} · {VRSTE_AKCIJA.find((v) => v.value === akcija.vrsta)?.label}
        {akcija.opis && ` — ${akcija.opis}`}
      </p>
      <p className="font-medium">Da li je akcija urađena?</p>
      {neispravna && <p className="text-amber-800">Stavka je označena kao neispravna; ako je akcija urađena, otvorite novu akciju za preostali kvar.</p>}
      <div className="flex gap-2">
        <Button size="sm" onClick={() => void posalji({ vrsta: 'akcija_odgovor', podaci: { akcijaId: akcija.id, uradjeno: true } }, 'Akcija je zatvorena.')}>Da</Button>
        <Button size="sm" variant="secondary" onClick={() => void posalji({ vrsta: 'akcija_odgovor', podaci: { akcijaId: akcija.id, uradjeno: false } }, 'Akcija ostaje otvorena.')}>Ne</Button>
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

/**
 * Pregled pre planiranog dana (odluka 2026-11-10): ne može da se započne. DP se radi na svoj dan (ako inženjer tada nije tu,
 * traži prebacivanje), a za PZ može da zatraži raniji početak — odobrava Supervizor ili Direktor i PZ se pomera na danas.
 */
export function PreDatuma({ id, pz, datum }: { id: number; pz: boolean; datum: string }) {
  const zatrazi = useZatraziRanijiPocetak();
  const zahtevi = useZahteviPrebacivanja('zahtev');
  const poslat = (zahtevi.data ?? []).some((z) => z.pregled.id === id && z.vrsta === 'raniji_pocetak');
  const toast = useToast();
  const [otvoren, setOtvoren] = useState(false);
  const [razlog, setRazlog] = useState('');
  const [greska, setGreska] = useState<string | null>(null);

  async function posalji() {
    if (!razlog.trim()) {
      setGreska('Razlog je obavezan.');
      return;
    }
    try {
      await zatrazi.mutateAsync({ pregledId: id, razlog: razlog.trim() });
      toast.success('Zahtev je poslat. Javićemo vam kad Supervizor ili Direktor odluči.');
      setOtvoren(false);
    } catch (err) {
      setGreska(porukaGreske(err, 'Zahtev nije poslat.'));
    }
  }

  return (
    <>
      <p className="text-sm font-semibold text-gray-800">Pregled može da se započne od {formatirajDatum(datum)}.</p>
      {pz ? (
        <>
          <p className="text-sm text-gray-600">Ako zastoj počinje ranije, zatražite raniji početak; odobrava Supervizor ili Direktor.</p>
          {poslat
            ? <p className="text-sm font-semibold text-amber-800">Zahtev poslat — čeka odluku.</p>
            : <Button variant="secondary" className="w-full sm:w-auto" onClick={() => { setGreska(null); setOtvoren(true); }}>Zatraži raniji početak</Button>}
        </>
      ) : (
        <p className="text-sm text-gray-600">Dnevni pregled se radi na svoj dan. Ako tog dana niste tu, zatražite prebacivanje na Početnoj.</p>
      )}
      <Modal open={otvoren} title="Raniji početak" onClose={() => setOtvoren(false)}>
        <FormField label="Razlog" htmlFor="raniji-razlog">
          <Textarea id="raniji-razlog" rows={3} value={razlog} onChange={(e) => setRazlog(e.target.value)} placeholder="Npr. zastoj počinje danas" />
        </FormField>
        {greska && <p role="alert" className="mt-3 text-sm text-red-600">{greska}</p>}
        <div className="flex justify-end gap-2 pt-4">
          <Button variant="ghost" onClick={() => setOtvoren(false)}>Odustani</Button>
          <Button onClick={() => void posalji()} disabled={zatrazi.isPending}>Pošalji zahtev</Button>
        </div>
      </Modal>
    </>
  );
}
