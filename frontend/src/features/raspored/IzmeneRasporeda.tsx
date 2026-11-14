import { useState } from 'react';
import { useToast } from '../../app/providers/ToastProvider';
import { Button } from '../../components/ui/Button';
import { FormField } from '../../components/ui/FormField';
import { Modal } from '../../components/ui/Modal';
import { Textarea } from '../../components/ui/Textarea';
import { porukaGreske } from '../../lib/apiError';
import { dodajDane, formatirajDatum, kratakDan } from '../../lib/datum';
import { useOtkaziPregled, usePovuci, usePromeniTipDp, useRaspored, type PregledKalendarDto } from './hooks';
import { mnozina } from '../../lib/broj';

/**
 * Otkazivanje planiranog pregleda (zamerka 27). Za DP dve opcije: „Pomeri raspored“ (pregled se ne gubi — on i naredni DP-ovi
 * idu dan napred, kao kod propuštenog DP-a) i „Otkaži, zadrži raspored“ (poništava se, naredni ostaju gde su). PZ se otkazuje.
 * Razlog je obavezan; inženjer dobija obaveštenje, a otkazan pregled se ne računa u ispunjenje plana.
 */
export function OtkazivanjeForma({ pregled, onDone, onBack }: { pregled: PregledKalendarDto; onDone: (r: PregledKalendarDto) => void; onBack: () => void }) {
  const otkazi = useOtkaziPregled();
  const toast = useToast();
  const [razlog, setRazlog] = useState('');
  const [greska, setGreska] = useState<string | null>(null);
  const dp = pregled.tip.startsWith('DP');

  async function posalji(pomeri: boolean) {
    if (!razlog.trim()) return setGreska('Razlog je obavezan.');
    try {
      const r = await otkazi.mutateAsync({ id: pregled.id, razlog: razlog.trim(), pomeri });
      toast.success(pomeri ? `${pregled.broj} je pomeren na ${formatirajDatum(r.planiraniDatum)}, a raspored za dan napred.` : `${pregled.broj} je otkazan.`);
      onDone(r);
    } catch (err) {
      setGreska(porukaGreske(err, 'Pregled nije otkazan.'));
    }
  }

  return (
    <div className="space-y-4">
      <FormField label="Razlog" htmlFor="otk-razlog">
        <Textarea id="otk-razlog" rows={3} value={razlog} onChange={(e) => setRazlog(e.target.value)} />
      </FormField>
      {dp ? (
        <ul className="space-y-2 text-sm text-gray-700">
          <li><b>Pomeri raspored</b> — {pregled.broj} prelazi na sledeći radni dan, a ceo raspored DP-a se pomera za dan (redosled DP1/DP2 ostaje, inženjeri ostaju na svojim danima).</li>
          <li><b>Otkaži, zadrži raspored</b> — {pregled.broj} se poništava (ostaje u arhivi, broj se ne koristi ponovo), a naredni pregledi ostaju gde su.</li>
        </ul>
      ) : (
        <p className="text-sm text-gray-700">{pregled.broj} se poništava i ostaje u arhivi. Sledeći PZ po redu se računa bez njega.</p>
      )}
      {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
      <div className="flex flex-wrap justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onBack} disabled={otkazi.isPending}>Nazad</Button>
        {dp && <Button type="button" variant="secondary" onClick={() => void posalji(false)} disabled={otkazi.isPending}>Otkaži, zadrži raspored</Button>}
        {dp
          ? <Button type="button" onClick={() => void posalji(true)} disabled={otkazi.isPending}>Pomeri raspored</Button>
          : <Button type="button" variant="danger" onClick={() => void posalji(false)} disabled={otkazi.isPending}>Otkaži pregled</Button>}
      </div>
    </div>
  );
}

/**
 * „Promeni DP“ (zamerka 31): drugi tip planiranog DP-a, uz izbor da li se naredni planirani DP-ovi usklade (smena DP1/DP2).
 * Pitanje se postavlja samo kad posle tog dana ima zakazanih DP-ova (zamerka 44). Promenjeni pregledi dobijaju nove brojeve;
 * stari se ne koriste ponovo.
 */
export function TipDpForma({ pregled, onDone, onBack }: { pregled: PregledKalendarDto; onDone: () => void; onBack: () => void }) {
  const promeni = usePromeniTipDp();
  const toast = useToast();
  const [greska, setGreska] = useState<string | null>(null);
  const novi = pregled.tip === 'DP1' ? 'DP2' : 'DP1';
  const posle = useRaspored(dodajDane(pregled.planiraniDatum, 1), dodajDane(pregled.planiraniDatum, 366));
  const naredni = posle.data?.pregledi.filter((x) => x.tip.startsWith('DP') && x.status === 'planiran' && x.planiraniDatum > pregled.planiraniDatum).length;

  async function posalji(uskladi: boolean) {
    try {
      const r = await promeni.mutateAsync({ id: pregled.id, tip: novi, uskladi });
      toast.success(`${pregled.broj} je sada ${r.broj}${uskladi ? '; naredni DP-ovi su usklađeni' : ''}.`);
      onDone();
    } catch (err) {
      setGreska(porukaGreske(err, 'Tip nije promenjen.'));
    }
  }

  const zauzeto = promeni.isPending || naredni === undefined;
  return (
    <div className="space-y-4 text-sm">
      {naredni ? (
        <>
          <p>
            {pregled.broj} ({formatirajDatum(pregled.planiraniDatum)}) postaje <b>{novi}</b> i dobija nov broj. Posle ovog dana ima još{' '}
            {naredni} {naredni === 1 ? 'zakazan DP' : 'zakazanih DP-ova'}. Uskladiti i {naredni === 1 ? 'njega' : 'njih'}?
          </p>
          <ul className="space-y-1 text-gray-700">
            <li><b>Da</b> — naredni planirani DP-ovi se preokrenu, pa smena DP1/DP2 ostaje ispravna.</li>
            <li><b>Ne</b> — menja se samo ovaj dan.</li>
          </ul>
        </>
      ) : (
        <p>{pregled.broj} ({formatirajDatum(pregled.planiraniDatum)}) postaje <b>{novi}</b> i dobija nov broj.</p>
      )}
      {greska && <p role="alert" className="text-red-600">{greska}</p>}
      <div className="flex flex-wrap justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onBack} disabled={promeni.isPending}>Nazad</Button>
        {naredni ? (
          <>
            <Button type="button" variant="secondary" onClick={() => void posalji(false)} disabled={zauzeto}>Ne, samo ovaj</Button>
            <Button type="button" onClick={() => void posalji(true)} disabled={zauzeto}>Da, uskladi naredne</Button>
          </>
        ) : (
          <Button type="button" onClick={() => void posalji(false)} disabled={zauzeto}>Promeni u {novi}</Button>
        )}
      </div>
    </div>
  );
}

/**
 * „Povuci raspored“ (zamerka 29): posle izmene je dan ostao bez DP-a usred niza. Povlačenjem ga preuzima prvi kasniji DP,
 * svaki sledeći ide dan unazad (redosled i brojevi ostaju), a poslednji zakazani dan ostaje slobodan.
 */
export function PovlacenjeDijalog({ datum, pz = false, onClose }: { datum: string; pz?: boolean; onClose: () => void }) {
  const povuci = usePovuci();
  const toast = useToast();
  const [greska, setGreska] = useState<string | null>(null);

  async function potvrdi() {
    try {
      const r = await povuci.mutateAsync(datum);
      toast.success(`Raspored je povučen (${r.pomereno} ${mnozina(r.pomereno, { jednina: 'DP', paukal: 'DP-a', mnozina: 'DP-ova' })}).`);
      onClose();
    } catch (err) {
      setGreska(porukaGreske(err, 'Raspored nije povučen.'));
    }
  }

  return (
    <Modal open title="Dan je ostao bez DP-a" onClose={onClose}>
      <div className="space-y-3 text-sm">
        <p>{kratakDan(datum)}, {formatirajDatum(datum)} je sada slobodan, a posle njega ima zakazanih DP-ova.</p>
        {pz && <p>Kad je PZ zakazan, DP-ovi su pomereni za dan napred. „Povuci raspored“ ih vraća kako su bili pre tog PZ-a.</p>}
        <ul className="space-y-1 text-gray-700">
          <li><b>Povuci raspored</b> — naredni DP-ovi idu dan unazad (redosled DP1/DP2 i inženjeri po danima ostaju); poslednji zakazani dan ostaje slobodan za sledeći predlog.</li>
          <li><b>Ostavi prazno</b> — taj dan ostaje bez DP-a.</li>
        </ul>
        {greska && <p role="alert" className="text-red-600">{greska}</p>}
        <div className="flex flex-wrap justify-end gap-2">
          <Button variant="ghost" onClick={onClose} disabled={povuci.isPending}>Ostavi prazno</Button>
          <Button onClick={() => void potvrdi()} disabled={povuci.isPending}>{povuci.isPending ? 'Čuvanje…' : 'Povuci raspored'}</Button>
        </div>
      </div>
    </Modal>
  );
}

/** Upozorenje ispod datuma: koji DP se pomera (zamerka 43), ili da započet DP ostaje i taj dan se rade oba (zamerka 53). */
export function UpozorenjeDp({ dp, radnja }: { dp: PregledKalendarDto; radnja: string }) {
  return (
    <p role="status" className="rounded-md border border-amber-200 bg-amber-50 p-2 text-sm text-amber-900">
      {dp.status === 'u_radu'
        ? <>⚠ <b>{dp.broj} ({dp.dodeljen})</b> je već započet i ostaje tog dana — taj dan se rade i DP i PZ.</>
        : <>⚠ Na taj dan je zakazan <b>{dp.broj} ({dp.dodeljen})</b>. Ako {radnja} PZ, on i svi naredni DP-ovi se pomeraju za jedan dan napred.</>}
    </p>
  );
}

