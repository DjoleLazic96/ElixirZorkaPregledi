import { useState, type FormEvent } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { AlertTriangle, ArrowLeft, FileText } from 'lucide-react';
import { useToast } from '../../app/providers/ToastProvider';
import { Slike } from '../../components/Slike';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { FormField } from '../../components/ui/FormField';
import { Modal } from '../../components/ui/Modal';
import { Select } from '../../components/ui/Select';
import { Ucitavanje } from '../../components/ui/Spinner';
import { Textarea } from '../../components/ui/Textarea';
import { porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { formatirajDatum, formatirajVreme } from '../../lib/datum';
import { preuzmiFajl } from '../../lib/download';
import { imaPravo } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import { useInzenjeri } from '../raspored/hooks';
import { statusLabela, tonStatusa, useOdluka, usePregled, type PregledDetaljDto } from './hooks';

type Dijalog = null | 'vrati' | 'ponisti' | 'zatrazi';

/**
 * Detalj pregleda (R6.x, R5.3–R5.4): vreme i upozorenja za kontrolora, rezime problematičnih stavki, ceo pregled sa
 * slikama, istorija, sporni unosi i zahtevi za doradu. Akcije po pravu: Overio, Vrati na doradu, Poništi (pravo „overa“),
 * Zatraži doradu (inženjer koji je radio). „Nazad“ vraća na ekran sa kog je otvoren (R7.7).
 */
export function PregledDetaljPage() {
  const id = Number(useParams().id);
  const izdvojenId = Number(useSearchParams()[0].get('uredjaj'));
  const navigate = useNavigate();
  const { me } = useAuth();
  const pristup = me ?? { roles: [], prava: [] };
  const pregled = usePregled(id);
  const odluka = useOdluka(id);
  const toast = useToast();
  const [dijalog, setDijalog] = useState<Dijalog>(null);

  async function posalji(putanja: string, telo: object | undefined, uspeh: string) {
    try {
      await odluka.mutateAsync({ putanja, telo });
      toast.success(uspeh);
      setDijalog(null);
      return null;
    } catch (err) {
      return porukaGreske(err, 'Odluka nije sačuvana.');
    }
  }

  if (pregled.isLoading) return <Ucitavanje />;
  if (pregled.isError || !pregled.data) return <p role="alert" className="text-sm text-red-600">{porukaGreske(pregled.error, 'Pregled nije učitan.')}</p>;
  const p = pregled.data;
  const overa = imaPravo(pristup, 'overa');
  const moj = me?.user.id === p.dodeljenId;
  const problemi = p.sekcije.flatMap((s) => s.uredjaji.flatMap((u) => u.stavke.filter((x) => x.neispravan).map((x) => ({ s: s.sifra, u, x }))));
  const van = p.sekcije.flatMap((s) => s.uredjaji.filter((u) => u.vanFunkcije));
  const zahtev = p.zahteviDorade.find((z) => z.status === 'zahtev');
  const izdvojen = p.sekcije.flatMap((s) => s.uredjaji.map((u) => ({ s: s.sifra, u }))).find((x) => x.u.uredjajId === izdvojenId);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Button variant="ghost" onClick={() => navigate(-1)}><ArrowLeft size={14} aria-hidden /> Nazad</Button>
        <div className="flex flex-wrap gap-2">
          <Button variant="secondary" onClick={() => void preuzmiFajl(`/pregledi/${p.id}/izvoz`, { format: 'pdf' }, `${p.broj}.pdf`).catch((e) => toast.error(porukaGreske(e, 'Izvoz nije uspeo.')))}>
            <FileText size={14} aria-hidden /> PDF
          </Button>
          <Button variant="secondary" onClick={() => void preuzmiFajl(`/pregledi/${p.id}/izvoz`, { format: 'docx' }, `${p.broj}.docx`).catch((e) => toast.error(porukaGreske(e, 'Izvoz nije uspeo.')))}>
            <FileText size={14} aria-hidden /> Word
          </Button>
        </div>
      </div>

      <Card>
        <CardBody className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="whitespace-nowrap text-xl font-semibold">{p.broj}</h1>
            {p.dorada > 0 && <Badge tone="amber">dorada {p.dorada}</Badge>}
            <Badge tone={tonStatusa(p.status)}>{statusLabela(p.status)}</Badge>
          </div>
          <dl className="grid gap-x-6 gap-y-1 text-sm sm:grid-cols-2 lg:grid-cols-3">
            <Polje l="Šablon" v={`${p.tip}, verzija ${p.verzijaBroj ?? '—'}`} />
            <Polje l="Planirani datum" v={`${formatirajDatum(p.planiraniDatum)}${p.prvobitniDatum !== p.planiraniDatum ? ` (prvobitno ${formatirajDatum(p.prvobitniDatum)})` : ''}`} />
            <Polje l="Inženjer" v={p.inzenjer} />
            <Polje l="Početak" v={p.zapocetAt ? formatirajVreme(p.zapocetAt) : '—'} />
            <Polje l="Predaja" v={p.predatAt ? formatirajVreme(p.predatAt) : '—'} />
            <Polje l="Aktivno vreme" v={`${Math.round(p.aktivnoSekundi / 60)} min · ${p.delova} ${p.delova === 1 ? 'deo' : 'dela'}`} />
            <Polje l="Overio" v={p.overio ? `${p.overio}${p.overenoAt ? `, ${formatirajDatum(p.overenoAt)}` : ''}` : '—'} />
            {p.ponistenRazlog && <Polje l="Razlog poništavanja" v={p.ponistenRazlog} />}
          </dl>
          {overa && p.upozorenja.length > 0 && (
            <ul className="space-y-1 rounded-md bg-amber-50 p-2 text-sm text-amber-900">
              {p.upozorenja.map((u) => <li key={u} className="flex gap-2"><AlertTriangle size={14} className="mt-0.5 shrink-0" aria-hidden />{u}</li>)}
            </ul>
          )}
          <div className="flex flex-wrap gap-2">
            {overa && p.status === 'ceka_overu' && <Button disabled={odluka.isPending} onClick={() => void posalji('overi', undefined, 'Pregled je overen.')}>Overio</Button>}
            {overa && (p.status === 'ceka_overu' || p.status === 'overen') && <Button variant="secondary" onClick={() => setDijalog('vrati')}>Vrati na doradu</Button>}
            {overa && ['u_radu', 'ceka_overu', 'overen', 'vracen_na_doradu'].includes(p.status) && <Button variant="ghost" onClick={() => setDijalog('ponisti')}>Poništi pregled</Button>}
            {moj && !zahtev && (p.status === 'ceka_overu' || p.status === 'overen') && imaPravo(pristup, 'pregledi.popunjavanje') && (
              <Button variant="secondary" onClick={() => setDijalog('zatrazi')}>Zatraži doradu</Button>
            )}
            {moj && (p.status === 'vracen_na_doradu' || p.status === 'u_radu') && <Button onClick={() => navigate(`/pregled/${p.id}/popuni`)}>Popuni pregled</Button>}
          </div>
          {zahtev && (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-amber-200 bg-amber-50 p-2 text-sm">
              <span className="break-words">Zahtev za doradu ({zahtev.podneo}): {zahtev.razlog}</span>
              {overa && (
                <span className="flex gap-2">
                  <Button size="sm" variant="ghost" onClick={() => void posalji('zahtev-za-doradu/odluka', { odobreno: false }, 'Zahtev je odbijen.').then((g) => g && toast.error(g))}>Odbij</Button>
                  <Button size="sm" onClick={() => void posalji('zahtev-za-doradu/odluka', { odobreno: true }, 'Dorada je odobrena.').then((g) => g && toast.error(g))}>Odobri</Button>
                </span>
              )}
            </div>
          )}
        </CardBody>
      </Card>

      {p.sporniUnosi.some((s) => s.odluka === null) && overa && (
        <Card>
          <CardHeader><CardTitle>Sporni unosi</CardTitle></CardHeader>
          <CardBody>
            <ul className="divide-y divide-gray-100">
              {p.sporniUnosi.filter((s) => s.odluka === null).map((s) => (
                <li key={s.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                  <span className="min-w-0 break-words">{s.korisnik}: {s.opis}<span className="block text-xs text-gray-500">{s.razlog}</span></span>
                  <span className="flex gap-2">
                    <Button size="sm" variant="ghost" onClick={() => void posalji(`sporan-unos/${s.id}/odluka`, { primeni: false }, 'Unos je odbačen.').then((g) => g && toast.error(g))}>Odbaci</Button>
                    <Button size="sm" onClick={() => void posalji(`sporan-unos/${s.id}/odluka`, { primeni: true }, 'Unos je primenjen.').then((g) => g && toast.error(g))}>Primeni</Button>
                  </span>
                </li>
              ))}
            </ul>
          </CardBody>
        </Card>
      )}

      {izdvojen && (
        <Card className="border-primary-300">
          <CardHeader><CardTitle>Ovaj uređaj · sekcija {izdvojen.s}</CardTitle></CardHeader>
          <CardBody><UredjajStavke u={izdvojen.u} /></CardBody>
        </Card>
      )}

      <Card>
        <CardHeader><CardTitle>Problematične stavke ({problemi.length})</CardTitle></CardHeader>
        <CardBody className="space-y-2 text-sm">
          {problemi.length === 0 && <p className="text-gray-500">Nema neispravnih stavki.</p>}
          {problemi.map(({ s, u, x }) => (
            <div key={x.stavkaId} className="rounded-md border border-red-200 bg-red-50 p-2">
              <p className="break-words font-medium text-red-900">Sekcija {s} · {u.naziv} · {x.aktivnost}: {x.odgovor}</p>
              {(x.napomena || x.polja.length > 0) && <p className="break-words text-red-800">{[x.napomena, ...x.polja].filter(Boolean).join(' · ')}</p>}
              <Slike ids={x.fotografije} />
            </div>
          ))}
          {van.length > 0 && <p className="text-amber-800">Nije u funkciji: {van.map((u) => u.naziv).join(', ')}</p>}
        </CardBody>
      </Card>

      {p.sekcije.map((s) => (
        <Card key={s.sifra}>
          <CardHeader><CardTitle>Sekcija {s.sifra}</CardTitle></CardHeader>
          <CardBody className="space-y-3">
            {s.uredjaji.map((u) => <UredjajStavke key={u.uredjajId} u={u} />)}
          </CardBody>
        </Card>
      ))}

      <Card>
        <CardHeader><CardTitle>Istorija</CardTitle></CardHeader>
        <CardBody>
          <ul className="space-y-1 text-sm">
            {p.istorija.map((d, i) => (
              <li key={i} className="flex flex-wrap gap-x-3">
                <span className="whitespace-nowrap text-gray-500">{formatirajVreme(d.at)}</span>
                <span className="min-w-0 break-words">{d.opis}{d.korisnik && <span className="text-gray-500"> · {d.korisnik}</span>}</span>
              </li>
            ))}
          </ul>
        </CardBody>
      </Card>

      {dijalog === 'vrati' && <VratiDijalog p={p} onClose={() => setDijalog(null)} posalji={posalji} />}
      {dijalog === 'ponisti' && (
        <RazlogDijalog naslov="Poništi pregled" dugme="Poništi" opis="Pregled ostaje u arhivi kao „Poništen“, ne ulazi u analitiku, a broj se ne koristi ponovo."
          onClose={() => setDijalog(null)} onSubmit={(razlog) => posalji('ponisti', { razlog }, 'Pregled je poništen.')} />
      )}
      {dijalog === 'zatrazi' && (
        <RazlogDijalog naslov="Zatraži doradu" dugme="Pošalji zahtev" opis="Bez odobrenja predat pregled se ne menja."
          onClose={() => setDijalog(null)} onSubmit={(razlog) => posalji('zahtev-za-doradu', { razlog }, 'Zahtev za doradu je poslat.')} />
      )}
    </div>
  );
}

/** Stavke jednog uređaja u pregledu. */
function UredjajStavke({ u }: { u: PregledDetaljDto['sekcije'][number]['uredjaji'][number] }) {
  return (
    <div>
      <p className="break-words font-medium">{u.naziv}{u.vanFunkcije && <Badge tone="amber" className="ml-2">nije u funkciji</Badge>}</p>
      <ul className="mt-1 space-y-1 text-sm">
        {u.stavke.map((x) => (
          <li key={x.stavkaId} className="flex flex-wrap justify-between gap-x-3">
            <span className="min-w-0 break-words">{x.aktivnost}{x.napomena && <span className="text-gray-500"> — {x.napomena}</span>}</span>
            <span className={cn('whitespace-nowrap', x.neispravan ? 'font-semibold text-red-700' : 'text-gray-600')}>{u.vanFunkcije ? '—' : x.odgovor ?? 'bez odgovora'}</span>
            {x.fotografije.length > 0 && <Slike ids={x.fotografije} />}
          </li>
        ))}
      </ul>
    </div>
  );
}

function Polje({ l, v }: { l: string; v: string }) {
  return <div className="min-w-0"><dt className="inline text-gray-500">{l}: </dt><dd className="inline break-words">{v}</dd></div>;
}

function VratiDijalog({ p, onClose, posalji }: { p: PregledDetaljDto; onClose: () => void; posalji: (putanja: string, telo: object, uspeh: string) => Promise<string | null> }) {
  const inzenjeri = useInzenjeri();
  const [inz, setInz] = useState<string | null>(p.dodeljenId);
  const [razlog, setRazlog] = useState('');
  const [greska, setGreska] = useState<string | null>(null);
  const potvrdi = async (e: FormEvent) => {
    e.preventDefault();
    setGreska(await posalji('vrati-na-doradu', { inzenjerId: inz, razlog: razlog.trim() }, 'Pregled je vraćen na doradu.'));
  };
  return (
    <Modal open title="Vrati na doradu" onClose={onClose}>
      <form onSubmit={(e) => void potvrdi(e)} className="space-y-4" noValidate>
        <FormField label="Inženjer" htmlFor="vr-inz" hint="Predlaže se isti inženjer.">
          <Select id="vr-inz" value={inz} onChange={setInz} options={(inzenjeri.data ?? []).map((i) => ({ value: i.id, label: i.ime }))} />
        </FormField>
        <FormField label="Razlog" htmlFor="vr-razlog"><Textarea id="vr-razlog" rows={3} value={razlog} onChange={(e) => setRazlog(e.target.value)} /></FormField>
        {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>Odustani</Button>
          <Button type="submit">Vrati na doradu</Button>
        </div>
      </form>
    </Modal>
  );
}

function RazlogDijalog({ naslov, dugme, opis, onClose, onSubmit }: { naslov: string; dugme: string; opis: string; onClose: () => void; onSubmit: (razlog: string) => Promise<string | null> }) {
  const [razlog, setRazlog] = useState('');
  const [greska, setGreska] = useState<string | null>(null);
  return (
    <Modal open title={naslov} onClose={onClose}>
      <form onSubmit={(e) => { e.preventDefault(); void onSubmit(razlog.trim()).then(setGreska); }} className="space-y-4" noValidate>
        <p className="text-sm text-gray-600">{opis}</p>
        <FormField label="Razlog" htmlFor="rz-razlog"><Textarea id="rz-razlog" rows={3} value={razlog} onChange={(e) => setRazlog(e.target.value)} /></FormField>
        {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>Odustani</Button>
          <Button type="submit">{dugme}</Button>
        </div>
      </form>
    </Modal>
  );
}
