import { useState } from 'react';
import { useToast } from '../../app/providers/ToastProvider';
import { Button } from '../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../components/ui/Card';
import { Ucitavanje } from '../../components/ui/Spinner';
import { porukaGreske } from '../../lib/apiError';
import { usePrimaoci, useSacuvajPrimaoce, type PrimaociDto } from './hooks';

/** „Primaoci obaveštenja“ u Podešavanjima (R9.4): štikliranje po vrsti; dok se ne sačuva, važe Supervizor i Direktor. */
export function PrimaociCard() {
  const primaoci = usePrimaoci();
  return (
    <Card>
      <CardHeader><CardTitle>Primaoci obaveštenja</CardTitle></CardHeader>
      <CardBody>
        {primaoci.isLoading ? <Ucitavanje /> : !primaoci.data
          ? <p role="alert" className="text-sm text-red-600">{porukaGreske(primaoci.error, 'Primaoci nisu učitani.')}</p>
          : <PrimaociForma key={JSON.stringify(primaoci.data)} pocetno={primaoci.data} />}
      </CardBody>
    </Card>
  );
}

function PrimaociForma({ pocetno }: { pocetno: PrimaociDto }) {
  const sacuvaj = useSacuvajPrimaoce();
  const toast = useToast();
  const [izbor, setIzbor] = useState<Record<string, string[]>>(() => Object.fromEntries(pocetno.vrste.map((v) => [v.vrsta, v.korisnikIds])));

  function prekidac(vrsta: string, id: string) {
    setIzbor((s) => ({ ...s, [vrsta]: s[vrsta].includes(id) ? s[vrsta].filter((x) => x !== id) : [...s[vrsta], id] }));
  }

  async function posalji() {
    try {
      await sacuvaj.mutateAsync(izbor);
      toast.success('Primaoci su sačuvani.');
    } catch (e) {
      toast.error(porukaGreske(e, 'Čuvanje nije uspelo.'));
    }
  }

  return (
    <div className="space-y-3">
      <p className="text-sm text-gray-600">Lična obaveštenja (dodela, dorada, ishod zahteva) uvek idu onome koga se tiču.</p>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="text-xs text-gray-500">
            <tr>
              <th className="px-2 py-2 text-left">Obaveštenje</th>
              {pocetno.korisnici.map((k) => <th key={k.id} className="px-2 py-2 text-center font-medium">{k.ime}</th>)}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {pocetno.vrste.map((v) => (
              <tr key={v.vrsta}>
                <td className="px-2 py-2">
                  <span className="block font-medium">{v.naziv}{!v.podeseno && <span className="ml-1 text-xs font-normal text-gray-500">(podrazumevano)</span>}</span>
                  <span className="block text-xs text-gray-500">{v.opis}</span>
                </td>
                {pocetno.korisnici.map((k) => (
                  <td key={k.id} className="px-2 py-2 text-center">
                    <input type="checkbox" className="h-5 w-5 accent-primary-600" aria-label={`${v.naziv}: ${k.ime}`}
                      checked={izbor[v.vrsta].includes(k.id)} onChange={() => prekidac(v.vrsta, k.id)} />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Button disabled={sacuvaj.isPending} onClick={() => void posalji()}>{sacuvaj.isPending ? 'Čuvanje…' : 'Sačuvaj primaoce'}</Button>
    </div>
  );
}
