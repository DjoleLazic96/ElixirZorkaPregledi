import { useState, type FormEvent } from 'react';
import { CalendarOff } from 'lucide-react';
import { useToast } from '../../app/providers/ToastProvider';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { DatePicker } from '../../components/ui/DatePicker';
import { EmptyState } from '../../components/ui/EmptyState';
import { FormField } from '../../components/ui/FormField';
import { Input } from '../../components/ui/Input';
import { PageHeader } from '../../components/ui/PageHeader';
import { Select } from '../../components/ui/Select';
import { Ucitavanje } from '../../components/ui/Spinner';
import { porukaGreske } from '../../lib/apiError';
import { danasYmd, formatirajDatum } from '../../lib/datum';
import { imaPravo } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import {
  STATUS_ODSUSTVA, VRSTE_NERADNIH, VRSTE_ODSUSTVA, useNeradniDani, useObrisiNeradni, useOdsustva, useOtkaziOdsustvo, useUnesiNeradne, useZahtevOdsustva,
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
            <FormField label="Od" htmlFor="mo-od"><DatePicker id="mo-od" value={od} onChange={setOd} clearable={false} /></FormField>
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

  async function potvrdi(e: FormEvent) {
    e.preventDefault();
    setGreska(null);
    try {
      const novi = await unesi.mutateAsync({ od, do: doDatuma, naziv: naziv.trim(), vrsta: vrsta ?? 'drzavni' });
      toast.success(`Uneto ${novi.length} dana.`);
      setNaziv('');
    } catch (err) {
      setGreska(porukaGreske(err, 'Neradni dani nisu uneti.'));
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Neradni dani"
        actions={
          <Select aria-label="Godina" value={godina} onChange={(v) => v && setGodina(v)}
            options={[-1, 0, 1].map((d) => ({ value: Number(danasYmd().slice(0, 4)) + d, label: String(Number(danasYmd().slice(0, 4)) + d) }))} />
        }
      />
      {sme && (
        <Card>
          <CardBody>
            <form onSubmit={potvrdi} className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4" noValidate>
              <FormField label="Od" htmlFor="nd-od"><DatePicker id="nd-od" value={od} onChange={setOd} clearable={false} /></FormField>
              <FormField label="Do" htmlFor="nd-do"><DatePicker id="nd-do" value={doDatuma} onChange={setDo} min={od} clearable={false} /></FormField>
              <FormField label="Naziv" htmlFor="nd-naziv"><Input id="nd-naziv" value={naziv} onChange={(e) => setNaziv(e.target.value)} placeholder="npr. Božić" /></FormField>
              <FormField label="Vrsta" htmlFor="nd-vrsta"><Select id="nd-vrsta" value={vrsta} onChange={setVrsta} options={VRSTE_NERADNIH} /></FormField>
              {greska && <p role="alert" className="text-sm text-red-600 sm:col-span-2 lg:col-span-4">{greska}</p>}
              <div className="flex justify-end sm:col-span-2 lg:col-span-4">
                <Button type="submit" disabled={unesi.isPending}>Dodaj</Button>
              </div>
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
                  <span>{formatirajDatum(d.datum)} · {d.naziv} <span className="text-xs text-gray-500">({VRSTE_NERADNIH.find((v) => v.value === d.vrsta)?.label})</span></span>
                  {sme && (
                    <Button size="sm" variant="ghost" disabled={obrisi.isPending}
                      onClick={() => void obrisi.mutateAsync(d.id).then(() => toast.success('Neradni dan je obrisan.'), (err) => toast.error(porukaGreske(err, 'Brisanje nije uspelo.')))}>
                      Obriši
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
