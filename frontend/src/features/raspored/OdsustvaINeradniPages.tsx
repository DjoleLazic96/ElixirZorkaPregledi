import { useState, type FormEvent } from 'react';
import { CalendarOff } from 'lucide-react';
import { api } from '../../api/client';
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
import { danSaDatumom, danasYmd, formatirajDatum } from '../../lib/datum';
import { imaPravo } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import { DodajDpDijalog, PovlacenjeDijalog } from './IzmeneRasporeda';
import { godineNeradnih } from './kalendar';
import { mnozina } from '../../lib/broj';
import {
  STATUS_ODSUSTVA, VRSTE_NERADNIH, VRSTE_ODSUSTVA, useNeradniDani, useObrisiNeradni, useOdsustva, useOtkaziOdsustvo, useUnesiNeradne, useZahtevOdsustva,
  type NeradniDanDto, type PregledKalendarDto, type RasporedDto,
} from './hooks';

/** „Moja odsustva“ (R3.19): zahtev za odsustvo i spisak ranijih sa statusom. */
export function MojaOdsustvaPage() {
  const { me } = useAuth();
  const odsustva = useOdsustva();
  const otkazi = useOtkaziOdsustvo();
  // Ko ima pravo odobravanja dobija sva odsustva; ovde su samo njegova.
  const moja = (odsustva.data ?? []).filter((o) => o.korisnikId === me?.user.id);
  const posalji = useZahtevOdsustva();
  const toast = useToast();
  const [od, setOd] = useState(danasYmd());
  const [doDatuma, setDo] = useState(danasYmd());
  const [vrsta, setVrsta] = useState<string | null>('godisnji');
  const [napomena, setNapomena] = useState('');
  const [greska, setGreska] = useState<string | null>(null);

  async function potvrdi(e: FormEvent) {
    e.preventDefault();
    setGreska(null);
    try {
      await posalji.mutateAsync({ od, do: doDatuma, vrsta: vrsta ?? 'godisnji', napomena: napomena.trim() || null });
      toast.success('Zahtev za odsustvo je poslat.');
      setNapomena('');
    } catch (err) {
      setGreska(porukaGreske(err, 'Zahtev nije poslat.'));
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Moja odsustva" />
      <Card>
        <CardHeader><CardTitle>Novi zahtev</CardTitle></CardHeader>
        <CardBody>
          <form onSubmit={potvrdi} className="grid gap-4 sm:grid-cols-2" noValidate>
            <FormField label="Od" htmlFor="mo-od"><DatePicker id="mo-od" value={od} onChange={(v) => { setOd(v); if (v > doDatuma) setDo(v); }} clearable={false} /></FormField>
            <FormField label="Do" htmlFor="mo-do"><DatePicker id="mo-do" value={doDatuma} onChange={setDo} min={od} clearable={false} /></FormField>
            <FormField label="Vrsta" htmlFor="mo-vrsta"><Select id="mo-vrsta" value={vrsta} onChange={setVrsta} options={VRSTE_ODSUSTVA} /></FormField>
            <FormField label="Napomena (nije obavezno)" htmlFor="mo-nap"><Input id="mo-nap" value={napomena} onChange={(e) => setNapomena(e.target.value)} /></FormField>
            {greska && <p role="alert" className="text-sm text-red-600 sm:col-span-2">{greska}</p>}
            <div className="flex justify-end sm:col-span-2">
              <Button type="submit" disabled={posalji.isPending}>{posalji.isPending ? 'Slanje…' : 'Pošalji zahtev'}</Button>
            </div>
          </form>
        </CardBody>
      </Card>
      <Card>
        <CardHeader><CardTitle>Zahtevi i odsustva</CardTitle></CardHeader>
        <CardBody>
          {odsustva.isLoading ? <Ucitavanje /> : !moja.length ? <EmptyState icon={CalendarOff} title="Još nema odsustava." /> : (
            <ul className="divide-y divide-gray-100">
              {moja.map((o) => (
                <li key={o.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                  <span>
                    {formatirajDatum(o.od)} – {formatirajDatum(o.do)} · {VRSTE_ODSUSTVA.find((v) => v.value === o.vrsta)?.label}
                    {o.razlogOdbijanja && <span className="block text-xs text-gray-500">{o.razlogOdbijanja}</span>}
                  </span>
                  <span className="flex items-center gap-2">
                    <Badge tone={STATUS_ODSUSTVA[o.status][0]}>{STATUS_ODSUSTVA[o.status][1]}</Badge>
                    {o.status === 'zahtev' && (
                      <Button size="sm" variant="ghost" disabled={otkazi.isPending}
                        onClick={() => void otkazi.mutateAsync(o.id).then(() => toast.success('Zahtev je povučen.')).catch((e) => toast.error(porukaGreske(e, 'Zahtev nije povučen.')))}>
                        Povuci zahtev
                      </Button>
                    )}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardBody>
      </Card>
    </div>
  );
}

/** „Neradni dani“ (R3.16, R3.17): unos od–do sa nazivom i vrstom; Admin i Direktor. */
export function NeradniDaniPage() {
  const { me } = useAuth();
  const sme = imaPravo(me ?? { roles: [], prava: [] }, 'neradni_dani');
  const [godina, setGodina] = useState(Number(danasYmd().slice(0, 4)));
  const dani = useNeradniDani(godina);
  const unesi = useUnesiNeradne();
  const obrisi = useObrisiNeradni();
  const toast = useToast();
  const [od, setOd] = useState(danasYmd());
  const [doDatuma, setDo] = useState(danasYmd());
  const [naziv, setNaziv] = useState('');
  const [vrsta, setVrsta] = useState<string | null>('drzavni');
  const [greska, setGreska] = useState<string | null>(null);

  // DP-ovi zakazani u periodu (zamerka 52): pre unosa se pita da li se pomeraju dalje u nizu.
  const [dpUPeriodu, setDpUPeriodu] = useState<PregledKalendarDto[] | null>(null);
  const [slobodan, setSlobodan] = useState<string | null>(null);
  const smeRaspored = imaPravo(me ?? { roles: [], prava: [] }, 'raspored.uredjivanje');
  const radni = vrsta === 'radni';
  // Zamerka 68: posle unosa radnog vikend dana pita se „Dodaj DP?“ za svaki uneti dan redom.
  const [radniZaDp, setRadniZaDp] = useState<string[]>([]);
  const [dodajDp, setDodajDp] = useState<string | null>(null);
  // Brisanje radnog vikend dana na kom je zakazan DP: pita se da li se DP pomera dalje u nizu.
  const [brisanje, setBrisanje] = useState<{ dan: NeradniDanDto; dp: PregledKalendarDto[] } | null>(null);
  const sledeci = () => setRadniZaDp((l) => l.slice(1));

  async function sacuvaj(pomeriDp: boolean) {
    try {
      const novi = await unesi.mutateAsync({ od, do: doDatuma, naziv: naziv.trim(), vrsta: vrsta ?? 'drzavni', pomeriDp });
      const dana = `${novi.length} ${mnozina(novi.length, { jednina: 'dan', paukal: 'dana', mnozina: 'dana' })}`;
      if (radni) toast.success(novi.length === 1 ? `${danSaDatumom(novi[0].datum)} je radni dan.` : `Uneto ${dana} kao radni dani.`);
      else toast.success(pomeriDp ? `Uneto ${dana}; DP-ovi su pomereni.` : `Uneto ${dana}.`);
      if (radni && smeRaspored) setRadniZaDp(novi.map((n) => n.datum).filter((d) => d >= danasYmd()));
      setNaziv('');
      setDpUPeriodu(null);
    } catch (err) {
      setDpUPeriodu(null);
      setGreska(porukaGreske(err, 'Neradni dani nisu uneti.'));
    }
  }

  async function potvrdi(e: FormEvent) {
    e.preventDefault();
    setGreska(null);
    if (radni) return void sacuvaj(false);   // vikendom DP-a nema, pa nema šta da se pomera
    try {
      const r = (await api.get<RasporedDto>('/raspored', { params: { od, do: doDatuma } })).data;
      const dp = r.pregledi.filter((p) => p.tip.startsWith('DP') && p.status === 'planiran' && p.planiraniDatum >= od && p.planiraniDatum <= doDatuma);
      if (dp.length > 0) return setDpUPeriodu(dp);
    } catch {
      // Provera nije uspela — unos ide kao i do sada (server odbija ako treba).
    }
    await sacuvaj(false);
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Neradni dani"
        actions={
          <Select aria-label="Godina" value={godina} onChange={(v) => v && setGodina(v)}
            options={godineNeradnih(Number(danasYmd().slice(0, 4))).map((g) => ({ value: g, label: String(g) }))} />
        }
      />
      {sme && (
        <Card>
          <CardBody>
            <form onSubmit={potvrdi} className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4" noValidate>
              <FormField label="Od" htmlFor="nd-od"><DatePicker id="nd-od" value={od} onChange={(v) => { setOd(v); if (v > doDatuma) setDo(v); }} clearable={false} /></FormField>
              <FormField label="Do" htmlFor="nd-do"><DatePicker id="nd-do" value={doDatuma} onChange={setDo} min={od} clearable={false} /></FormField>
              <FormField label="Naziv" htmlFor="nd-naziv"><Input id="nd-naziv" value={naziv} onChange={(e) => setNaziv(e.target.value)} placeholder="npr. Božić" /></FormField>
              <FormField label="Vrsta" htmlFor="nd-vrsta"><Select id="nd-vrsta" value={vrsta} onChange={setVrsta} options={VRSTE_NERADNIH} /></FormField>
              {radni && (
                <p className="text-sm text-gray-600 sm:col-span-2 lg:col-span-4">
                  Za subotu ili nedelju kada se radi. DP se tada ne zakazuje sam — posle unosa možete odmah da ga dodate, ili kasnije u Rasporedu.
                </p>
              )}
              {greska && <p role="alert" className="text-sm text-red-600 sm:col-span-2 lg:col-span-4">{greska}</p>}
              <div className="flex justify-end sm:col-span-2 lg:col-span-4">
                <Button type="submit" disabled={unesi.isPending}>Dodaj</Button>
              </div>
              {dpUPeriodu && (
                <Modal open title="U tom periodu su zakazani DP-ovi" onClose={() => setDpUPeriodu(null)}>
                  <div className="space-y-3 text-sm">
                    <ul className="list-disc space-y-0.5 pl-5">
                      {dpUPeriodu.map((p) => <li key={p.id}>Na {formatirajDatum(p.planiraniDatum)} je zakazan <b>{p.broj}</b> ({p.dodeljen})</li>)}
                    </ul>
                    <p className="text-gray-700">Neradnim danima se DP ne radi. <b>Unesi i pomeri DP-ove</b>: ti DP-ovi i naredni idu dalje u nizu, a inženjeri dobijaju obaveštenje.</p>
                    <div className="flex flex-wrap justify-end gap-2">
                      <Button type="button" variant="ghost" onClick={() => setDpUPeriodu(null)} disabled={unesi.isPending}>Odustani</Button>
                      <Button type="button" onClick={() => void sacuvaj(true)} disabled={unesi.isPending}>Unesi i pomeri DP-ove</Button>
                    </div>
                  </div>
                </Modal>
              )}
            </form>
          </CardBody>
        </Card>
      )}
      <Card>
        <CardBody>
          {dani.isLoading ? <Ucitavanje /> : !dani.data?.length ? <EmptyState icon={CalendarOff} title={`Nema neradnih dana za ${godina}.`} /> : (
            <ul className="divide-y divide-gray-100">
              {dani.data.map((d) => (
                <li key={d.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                  <span>{formatirajDatum(d.datum)} · {d.naziv} {d.vrsta === 'radni'
                    ? <Badge tone="green">radni dan</Badge>
                    : <span className="text-xs text-gray-500">({VRSTE_NERADNIH.find((v) => v.value === d.vrsta)?.label})</span>}</span>
                  {sme && (
                    <Button size="sm" variant="ghost" disabled={obrisi.isPending}
                      onClick={() => void obrisiDan(d)}>
                      Obriši
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </CardBody>
      </Card>
      {slobodan && <PovlacenjeDijalog datum={slobodan} onClose={() => setSlobodan(null)} />}
      {radniZaDp.length > 0 && !dodajDp && (
        <Modal open title="Radni dan" onClose={sledeci}>
          <div className="space-y-3 text-sm">
            <p>{danSaDatumom(radniZaDp[0])} je sada radni dan. Da li želite da dodate DP?</p>
            <div className="flex flex-wrap justify-end gap-2">
              <Button variant="ghost" onClick={sledeci}>Ne sada</Button>
              <Button onClick={() => setDodajDp(radniZaDp[0])}>Dodaj DP</Button>
            </div>
          </div>
        </Modal>
      )}
      {dodajDp && <DodajDpDijalog datum={dodajDp} onClose={() => { setDodajDp(null); sledeci(); }} />}
      {brisanje && (
        <Modal open title="Na taj dan je zakazan DP" onClose={() => setBrisanje(null)}>
          <div className="space-y-3 text-sm">
            <ul className="list-disc space-y-0.5 pl-5">
              {brisanje.dp.map((p) => <li key={p.id}>Na {formatirajDatum(p.planiraniDatum)} je zakazan <b>{p.broj}</b> ({p.dodeljen})</li>)}
            </ul>
            <p className="text-gray-700">Kad se radni dan obriše, tog dana se DP ne radi. <b>Obriši i pomeri DP</b>: taj DP i naredni idu dalje u nizu, a inženjeri dobijaju obaveštenje.</p>
            <div className="flex flex-wrap justify-end gap-2">
              <Button variant="ghost" onClick={() => setBrisanje(null)} disabled={obrisi.isPending}>Odustani</Button>
              <Button onClick={() => void obrisiDan(brisanje.dan, true)} disabled={obrisi.isPending}>Obriši i pomeri DP</Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );

  async function obrisiDan(d: NeradniDanDto, pomeriDp = false) {
    if (d.vrsta === 'radni' && !pomeriDp) {
      try {
        const r = (await api.get<RasporedDto>('/raspored', { params: { od: d.datum, do: d.datum } })).data;
        const dp = r.pregledi.filter((p) => p.tip.startsWith('DP') && p.status === 'planiran' && p.planiraniDatum === d.datum);
        if (dp.length > 0) return setBrisanje({ dan: d, dp });
      } catch {
        // Provera nije uspela — briše se kao i do sada; DP koji ostane kalendar označava crveno.
      }
    }
    try {
      const r = await obrisi.mutateAsync({ id: d.id, pomeriDp });
      setBrisanje(null);
      toast.success(d.vrsta === 'radni' ? (pomeriDp ? 'Radni dan je obrisan, a DP je pomeren.' : 'Radni dan je obrisan.') : 'Neradni dan je obrisan.');
      // Dan je sada prazan usred niza DP-ova — nudi se povlačenje (zamerka 56).
      if (r.slobodanDan && smeRaspored) setSlobodan(r.slobodanDan);
    } catch (err) {
      setBrisanje(null);
      toast.error(porukaGreske(err, 'Brisanje nije uspelo.'));
    }
  }
}
