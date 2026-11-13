import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import { useToast } from '../../app/providers/ToastProvider';
import { Button } from '../../components/ui/Button';
import { DatePicker } from '../../components/ui/DatePicker';
import { FormField } from '../../components/ui/FormField';
import { Input } from '../../components/ui/Input';
import { Modal } from '../../components/ui/Modal';
import { Select } from '../../components/ui/Select';
import { Textarea } from '../../components/ui/Textarea';
import { porukaGreske } from '../../lib/apiError';
import { danasYmd, formatirajDatum } from '../../lib/datum';
import { smanjiSliku } from '../../lib/slika';
import { odbaciZahtevZatvaranja, posaljiZahtevZatvaranja, useNeposlatiZahtevi } from '../../offline/sinhronizacija';
import { useAuth } from '../auth/AuthProvider';
import { useInzenjeri } from '../raspored/hooks';
import { OtkaziZahtev } from '../raspored/OtkaziZahtev';
import type { AkcijaDetaljDto, AkcijaDto } from './hooks';
import { ZADUZENI_PREDLOZI } from './oznake';

const DRUGO = '__drugo__';

/** Slika sa servera (uz zahtev ili zatvorenu akciju) kroz API sa tokenom — img ne nosi Authorization. */
function SlikaZatvaranja({ url }: { url: string }) {
  const [src, setSrc] = useState<string | null>(null);
  useEffect(() => {
    let ziv = true;
    let objekat: string | null = null;
    void api.get<Blob>(url, { responseType: 'blob' }).then((r) => {
      if (!ziv) return;
      objekat = URL.createObjectURL(r.data);
      setSrc(objekat);
    }).catch(() => undefined);
    return () => {
      ziv = false;
      if (objekat) URL.revokeObjectURL(objekat);
    };
  }, [url]);
  return src ? <a href={src} target="_blank" rel="noreferrer"><img src={src} alt="Slika uz zatvaranje" className="h-24 w-24 rounded-lg border border-gray-200 object-cover" /></a> : null;
}

/**
 * Forma „šta je urađeno, kada i ko“ (zamerka 49): inženjer šalje zahtev za zatvaranje (uz sliku po želji, radi i bez
 * interneta), a Supervizor ili Direktor istim poljima zatvaraju akciju sa ekrana.
 */
export function ZatvaranjeForma({ akcija, zahtev, onClose }: { akcija: AkcijaDto; zahtev: boolean; onClose: () => void }) {
  const { me } = useAuth();
  const inzenjeri = useInzenjeri();
  const qc = useQueryClient();
  const toast = useToast();
  const [uradjeno, setUradjeno] = useState('');
  const [datum, setDatum] = useState(danasYmd());
  const [ko, setKo] = useState<string | null>(me?.user.fullName ?? null);
  const [drugo, setDrugo] = useState('');
  const [slika, setSlika] = useState<File | null>(null);
  const [greska, setGreska] = useState<string | null>(null);
  const [salje, setSalje] = useState(false);

  const opcije = [
    ...new Set([...(me?.user.fullName ? [me.user.fullName] : []), ...(inzenjeri.data ?? []).map((i) => i.ime)]),
  ].map((ime) => ({ value: ime, label: ime }))
    .concat(ZADUZENI_PREDLOZI.map((z) => ({ value: `Služba: ${z}`, label: `Služba: ${z}` })))
    .concat([{ value: DRUGO, label: 'Drugo (upišite)' }]);

  async function posalji() {
    const izvrsilac = ko === DRUGO ? drugo.trim() : ko ?? '';
    if (!uradjeno.trim()) return setGreska('Upišite šta je urađeno.');
    if (!izvrsilac) return setGreska('Izaberite ko je uradio.');
    setSalje(true);
    setGreska(null);
    try {
      if (zahtev) {
        const r = await posaljiZahtevZatvaranja({ akcijaId: akcija.id, uradjeno: uradjeno.trim(), datum, izvrsilac, slika: slika ? await smanjiSliku(slika) : undefined });
        if ('greska' in r) return setGreska(r.greska);
        toast.success(r.stanje === 'poslat' ? 'Zahtev je poslat. Javićemo vam kad Supervizor ili Direktor odluči.' : 'Nema interneta — zahtev će se poslati kad se veza vrati.');
      } else {
        const d = (await api.post<AkcijaDetaljDto>(`/akcije/${akcija.id}/zatvori`, { uradjeno: uradjeno.trim(), datum, izvrsilac })).data;
        qc.setQueryData(['akcije', 'detalj', akcija.id], d);
        toast.success('Akcija je zatvorena.');
      }
      void qc.invalidateQueries({ queryKey: ['akcije'] });
      onClose();
    } catch (err) {
      setGreska(porukaGreske(err, 'Nije sačuvano.'));
    } finally {
      setSalje(false);
    }
  }

  return (
    <Modal open title={zahtev ? `Zahtev za zatvaranje — ${akcija.broj}` : `Zatvori akciju — ${akcija.broj}`} onClose={onClose}>
      <div className="space-y-4">
        <FormField label="Šta je urađeno" htmlFor="zat-uradjeno">
          <Textarea id="zat-uradjeno" rows={3} value={uradjeno} maxLength={1000} onChange={(e) => setUradjeno(e.target.value)} />
        </FormField>
        <FormField label="Kada" htmlFor="zat-datum">
          <DatePicker id="zat-datum" value={datum} onChange={setDatum} max={danasYmd()} clearable={false} />
        </FormField>
        <FormField label="Ko je uradio" htmlFor="zat-ko">
          <Select id="zat-ko" value={ko} onChange={setKo} options={opcije} />
        </FormField>
        {ko === DRUGO && <Input aria-label="Ko je uradio (upišite)" value={drugo} maxLength={200} onChange={(e) => setDrugo(e.target.value)} />}
        {zahtev && (
          <FormField label="Slika (nije obavezno)" htmlFor="zat-slika">
            <input id="zat-slika" type="file" accept="image/*" capture="environment" className="block w-full text-sm"
              onChange={(e) => setSlika(e.target.files?.[0] ?? null)} />
          </FormField>
        )}
        {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose} disabled={salje}>Odustani</Button>
          <Button onClick={() => void posalji()} disabled={salje}>{salje ? 'Slanje…' : zahtev ? 'Pošalji zahtev' : 'Zatvori akciju'}</Button>
        </div>
      </div>
    </Modal>
  );
}

/** Zahtev koji čeka odluku: podaci i slika; odlučuje Supervizor ili Direktor, a podnosilac može da ga otkaže (zamerke 47, 49). */
export function ZahtevNaCekanju({ akcija, odlucuje }: { akcija: AkcijaDto; odlucuje: boolean }) {
  const z = akcija.zahtevZatvaranja!;
  const { me } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const [odbija, setOdbija] = useState(false);
  const [razlog, setRazlog] = useState('');
  const [salje, setSalje] = useState(false);

  async function poziv(putanja: string, telo?: object, uspeh?: string) {
    setSalje(true);
    try {
      const d = (await api.post<AkcijaDetaljDto>(`/akcije/zahtevi-zatvaranja/${z.id}/${putanja}`, telo ?? {})).data;
      qc.setQueryData(['akcije', 'detalj', akcija.id], d);
      void qc.invalidateQueries({ queryKey: ['akcije'] });
      if (uspeh) toast.success(uspeh);
      setOdbija(false);
    } catch (err) {
      toast.error(porukaGreske(err, 'Odluka nije sačuvana.'));
      throw err;
    } finally {
      setSalje(false);
    }
  }

  return (
    <div className="space-y-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm">
      <p className="font-semibold text-amber-900">Zahtev za zatvaranje ({z.podneo}) — čeka odluku</p>
      <p className="break-words">{z.uradjeno}</p>
      <p className="text-gray-700">{formatirajDatum(z.datum)} · uradio {z.izvrsilac}</p>
      {z.imaSliku && <SlikaZatvaranja url={`/akcije/${akcija.id}/slika?zahtev=${z.id}`} />}
      <div className="flex flex-wrap gap-2">
        {odlucuje && (
          <>
            <Button size="sm" disabled={salje} onClick={() => void poziv('odluka', { odobreno: true }, 'Akcija je zatvorena.').catch(() => undefined)}>Odobri i zatvori</Button>
            <Button size="sm" variant="ghost" disabled={salje} onClick={() => setOdbija(true)}>Odbij</Button>
          </>
        )}
        {z.podneoId === me?.user.id && <OtkaziZahtev opis="zahtev za zatvaranje" otkazi={() => poziv('otkazi')} />}
      </div>
      {odbija && (
        <Modal open title="Odbij zahtev" onClose={() => setOdbija(false)}>
          <div className="space-y-4">
            <Textarea aria-label="Razlog odbijanja" placeholder="Razlog odbijanja" rows={3} value={razlog} onChange={(e) => setRazlog(e.target.value)} />
            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={() => setOdbija(false)}>Nazad</Button>
              <Button disabled={!razlog.trim() || salje} onClick={() => void poziv('odluka', { odobreno: false, razlog: razlog.trim() }, 'Zahtev je odbijen.').catch(() => undefined)}>Odbij</Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}

/** Kako je akcija zatvorena: šta, kada, ko i slika (zamerka 49). */
export function NacinZatvaranja({ akcija }: { akcija: AkcijaDto }) {
  if (!akcija.uradjeno) return null;
  return (
    <div className="space-y-1">
      <p className="break-words"><span className="text-gray-600">Urađeno:</span> {akcija.uradjeno}</p>
      <p className="text-gray-600">{akcija.uradjenoDatum && formatirajDatum(akcija.uradjenoDatum)} · uradio {akcija.izvrsilac}</p>
      {akcija.imaSlikuZatvaranja && <SlikaZatvaranja url={`/akcije/${akcija.id}/slika`} />}
    </div>
  );
}

/** Zahtev sa ovog telefona koji još nije stigao na server (bez interneta) ili ga je server odbio. */
export function NeposlatiZahtevi({ akcijaId }: { akcijaId: number }) {
  const lista = useNeposlatiZahtevi(akcijaId);
  return (
    <>
      {lista.map((z) => (
        <div key={z.klijentId} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-gray-200 bg-gray-50 p-2 text-sm">
          <span className="break-words">{z.greska ? `Zahtev nije primljen: ${z.greska}` : 'Zahtev za zatvaranje čeka slanje — poslaće se kad se veza vrati.'}</span>
          {z.greska && <Button size="sm" variant="ghost" onClick={() => void odbaciZahtevZatvaranja(z.klijentId)}>Odbaci</Button>}
        </div>
      ))}
    </>
  );
}
