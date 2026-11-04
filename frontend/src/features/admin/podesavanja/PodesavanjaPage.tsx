import { useState, type FormEvent } from 'react';
import type { PodesavanjaDto } from '../../../api/types';
import { useToast } from '../../../app/providers/ToastProvider';
import { Button } from '../../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../../components/ui/Card';
import { Checkbox } from '../../../components/ui/Checkbox';
import { FormField } from '../../../components/ui/FormField';
import { Input } from '../../../components/ui/Input';
import { Ucitavanje } from '../../../components/ui/Spinner';
import { porukaGreske } from '../../../lib/apiError';
import { useIzmeniPodesavanja, usePodesavanja } from './hooks';

/**
 * „Podešavanja“ (R12.1, pravo „podesavanja“). Stranica drži mutaciju i toast; forma se remontira
 * preko `key` kad stignu podaci, pa nema sync-effect-a. Primaoci obaveštenja dolaze u fazi 09.
 */
export function PodesavanjaPage() {
  const podesavanja = usePodesavanja();
  const izmeni = useIzmeniPodesavanja();
  const toast = useToast();
  const [greska, setGreska] = useState<string | null>(null);

  async function sacuvaj(v: PodesavanjaDto) {
    setGreska(null);
    try {
      await izmeni.mutateAsync(v);
      toast.success('Podešavanja su sačuvana.');
    } catch (err) {
      setGreska(porukaGreske(err, 'Čuvanje podešavanja nije uspelo.'));
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Podešavanja</CardTitle>
      </CardHeader>
      <CardBody>
        {podesavanja.isLoading ? (
          <Ucitavanje />
        ) : podesavanja.isError || !podesavanja.data ? (
          <p role="alert" className="text-sm text-red-600">
            {porukaGreske(podesavanja.error, 'Podešavanja nisu učitana.')}
          </p>
        ) : (
          <PodesavanjaForma
            key={JSON.stringify(podesavanja.data)}
            pocetno={podesavanja.data}
            saving={izmeni.isPending}
            greska={greska}
            onSubmit={(v) => void sacuvaj(v)}
          />
        )}
      </CardBody>
    </Card>
  );
}

type BrojPolje = 'granicaPauzeMin' | 'odstupanjeSataMin' | 'rokOvereSati' | 'ponovljeniKvarBroj' | 'ponovljeniKvarDana' | 'vanFunkcijeDana';
type VremePolje = 'krajRadnogDana' | 'upozorenjeDp' | 'upozorenjePz';

const VREMENA: ReadonlyArray<{ k: VremePolje; label: string; hint: string }> = [
  { k: 'upozorenjeDp', label: 'Upozorenje „DP nije započet“', hint: 'Obaveštenje ako dnevni pregled do tada nije započet.' },
  { k: 'upozorenjePz', label: 'Upozorenje „PZ nije započet“', hint: 'Isto za pregled u planskom zastoju.' },
  { k: 'krajRadnogDana', label: 'Kraj radnog dana', hint: 'Posle ovog vremena nezapočet DP se prenosi na sledeći radni dan.' },
];

const BROJEVI: ReadonlyArray<{ k: BrojPolje; label: string; hint: string }> = [
  { k: 'granicaPauzeMin', label: 'Granica pauze (min)', hint: 'Veći razmak između dva odgovora ne ulazi u aktivno vreme pregleda.' },
  { k: 'odstupanjeSataMin', label: 'Dozvoljeno odstupanje sata telefona (min)', hint: 'Veće odstupanje se označava kontroloru.' },
  { k: 'rokOvereSati', label: 'Rok za overu (sati radnih dana)', hint: 'Po isteku ide podsetnik.' },
  { k: 'ponovljeniKvarBroj', label: 'Ponovljeni kvar — broj neispravnih', hint: 'Ista stavka neispravna ovoliko puta…' },
  { k: 'ponovljeniKvarDana', label: 'Ponovljeni kvar — u periodu (dana)', hint: '…u ovoliko dana šalje obaveštenje.' },
  { k: 'vanFunkcijeDana', label: 'Dugo van funkcije (dana)', hint: 'Obaveštenje posle ovoliko dana, i ponovo na svakih toliko.' },
];

interface FormaProps {
  pocetno: PodesavanjaDto;
  saving: boolean;
  greska: string | null;
  onSubmit: (v: PodesavanjaDto) => void;
}

function PodesavanjaForma({ pocetno, saving, greska, onSubmit }: FormaProps) {
  const [v, setV] = useState<PodesavanjaDto>(pocetno);

  const potvrdi = (e: FormEvent) => {
    e.preventDefault();
    onSubmit(v);
  };

  return (
    <form onSubmit={potvrdi} className="space-y-6" noValidate>
      <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {VREMENA.map((f) => (
          <FormField key={f.k} label={f.label} htmlFor={`pod-${f.k}`} hint={f.hint}>
            <Input
              id={`pod-${f.k}`}
              type="time"
              value={v[f.k].slice(0, 5)}
              onChange={(e) => setV({ ...v, [f.k]: `${e.target.value}:00` })}
            />
          </FormField>
        ))}
      </section>

      <Checkbox
        label="DP se radi i na dan PZ-a"
        description="Podrazumevano se na dan planskog zastoja dnevni pregled ne radi."
        checked={v.dpNaDanPz}
        onChange={(e) => setV({ ...v, dpNaDanPz: e.target.checked })}
      />

      <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {BROJEVI.map((f) => (
          <FormField key={f.k} label={f.label} htmlFor={`pod-${f.k}`} hint={f.hint}>
            <Input
              id={`pod-${f.k}`}
              type="number"
              inputMode="numeric"
              min={1}
              value={v[f.k]}
              onChange={(e) => setV({ ...v, [f.k]: Number(e.target.value) })}
            />
          </FormField>
        ))}
      </section>

      {greska && (
        <p role="alert" className="text-sm text-red-600">
          {greska}
        </p>
      )}

      <div className="flex justify-end">
        <Button type="submit" disabled={saving}>
          {saving ? 'Čuvanje…' : 'Sačuvaj podešavanja'}
        </Button>
      </div>
    </form>
  );
}
