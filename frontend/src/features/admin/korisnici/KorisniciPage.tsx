import { useState, type FormEvent } from 'react';
import { KeyRound, Plus, Users } from 'lucide-react';
import type { KorisnikDto } from '../../../api/types';
import { useConfirm } from '../../../app/providers/ConfirmProvider';
import { useToast } from '../../../app/providers/ToastProvider';
import { Badge } from '../../../components/ui/Badge';
import { Button } from '../../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../../components/ui/Card';
import { Checkbox } from '../../../components/ui/Checkbox';
import { EmptyState } from '../../../components/ui/EmptyState';
import { FetchingOverlay } from '../../../components/ui/FetchingOverlay';
import { FormField } from '../../../components/ui/FormField';
import { Input } from '../../../components/ui/Input';
import { Modal } from '../../../components/ui/Modal';
import { Ucitavanje } from '../../../components/ui/Spinner';
import { porukaGreske } from '../../../lib/apiError';
import { formatirajVreme, relativnoVreme } from '../../../lib/datum';
import { ROLA_LABELE } from '../../../lib/permissions';
import {
  useAktivirajKorisnika,
  useDeaktivirajKorisnika,
  useIzmeniKorisnika,
  useKorisnici,
  useKreirajKorisnika,
  useResetLozinke,
} from './hooks';
import { KorisnikForm, type KorisnikFormResult } from './KorisnikForm';

/**
 * Stranica „Korisnici" (Admin). Stranica drži mutacije, toastove i grešku forme;
 * `KorisnikForm` je prezentaciona i remontira se preko `key`. Deaktivacija i
 * aktivacija traže potvrdu; reset lozinke ide kroz zaseban mali modal.
 */
export function KorisniciPage() {
  const [prikaziNeaktivne, setPrikaziNeaktivne] = useState(false);
  const korisnici = useKorisnici(prikaziNeaktivne ? undefined : true);
  const kreiraj = useKreirajKorisnika();
  const izmeni = useIzmeniKorisnika();
  const deaktiviraj = useDeaktivirajKorisnika();
  const aktiviraj = useAktivirajKorisnika();
  const toast = useToast();
  const confirm = useConfirm();

  const [formaOtvorena, setFormaOtvorena] = useState(false);
  const [uredjivan, setUredjivan] = useState<KorisnikDto | null>(null);
  const [formaGreska, setFormaGreska] = useState<string | null>(null);
  const [resetZa, setResetZa] = useState<KorisnikDto | null>(null);

  function otvoriFormu(k: KorisnikDto | null) {
    setUredjivan(k);
    setFormaGreska(null);
    setFormaOtvorena(true);
  }

  async function sacuvaj(r: KorisnikFormResult) {
    setFormaGreska(null);
    try {
      if (uredjivan) {
        await izmeni.mutateAsync({ id: uredjivan.id, input: { ime: r.ime, email: r.email, rola: r.rola } });
        toast.success('Korisnik je izmenjen.');
      } else {
        await kreiraj.mutateAsync({
          korisnickoIme: r.korisnickoIme,
          ime: r.ime,
          email: r.email,
          lozinka: r.lozinka,
          rola: r.rola,
        });
        toast.success('Korisnik je kreiran.');
      }
      setFormaOtvorena(false);
    } catch (err) {
      setFormaGreska(porukaGreske(err, 'Čuvanje korisnika nije uspelo.'));
    }
  }

  async function promeniAktivnost(k: KorisnikDto) {
    const ok = await confirm(
      k.aktivan
        ? {
            title: 'Deaktivacija korisnika',
            message: `Korisnik „${k.ime}" (${k.korisnickoIme}) više neće moći da se prijavi. Nalog se ne briše i može se ponovo aktivirati.`,
            confirmLabel: 'Deaktiviraj',
            tone: 'danger',
          }
        : {
            title: 'Aktivacija korisnika',
            message: `Korisnik „${k.ime}" (${k.korisnickoIme}) će ponovo moći da se prijavi.`,
            confirmLabel: 'Aktiviraj',
            tone: 'normal',
          },
    );
    if (!ok) return;
    try {
      if (k.aktivan) await deaktiviraj.mutateAsync(k.id);
      else await aktiviraj.mutateAsync(k.id);
      toast.success(k.aktivan ? 'Korisnik je deaktiviran.' : 'Korisnik je aktiviran.');
    } catch (err) {
      toast.error(porukaGreske(err, 'Promena statusa nije uspela.'));
    }
  }

  const lista = korisnici.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle>Korisnici</CardTitle>
        <Button onClick={() => otvoriFormu(null)}>
          <Plus size={14} aria-hidden />
          Novi korisnik
        </Button>
      </CardHeader>
      <CardBody className="space-y-4">
        <Checkbox
          label="Prikaži i neaktivne"
          checked={prikaziNeaktivne}
          onChange={(e) => setPrikaziNeaktivne(e.target.checked)}
        />

        {korisnici.isPending ? (
          <Ucitavanje />
        ) : korisnici.isError ? (
          <div className="flex items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            <span>Učitavanje korisnika nije uspelo.</span>
            <Button variant="secondary" size="sm" onClick={() => void korisnici.refetch()}>
              Pokušaj ponovo
            </Button>
          </div>
        ) : lista.length === 0 ? (
          <EmptyState icon={Users} title="Nema korisnika za zadate filtere." />
        ) : (
          <FetchingOverlay fetching={korisnici.isFetching}>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="border-b border-gray-200 bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
                  <tr>
                    <th className="px-3 py-2 font-medium">Korisničko ime</th>
                    <th className="px-3 py-2 font-medium">Ime</th>
                    <th className="px-3 py-2 font-medium">E-mail</th>
                    <th className="px-3 py-2 font-medium">Rola</th>
                    <th className="px-3 py-2 font-medium">Status</th>
                    <th className="px-3 py-2 font-medium">Poslednja prijava</th>
                    <th className="px-3 py-2 text-right font-medium">Akcije</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {lista.map((k) => (
                    <tr key={k.id} className="hover:bg-gray-50">
                      <td className="px-3 py-2 font-medium text-gray-800">{k.korisnickoIme}</td>
                      <td className="px-3 py-2 text-gray-700">{k.ime}</td>
                      <td className="px-3 py-2 text-gray-700">{k.email ?? '—'}</td>
                      <td className="px-3 py-2 text-gray-700">{ROLA_LABELE[k.rola] ?? k.rola}</td>
                      <td className="px-3 py-2">
                        {k.aktivan ? <Badge tone="primary">Aktivan</Badge> : <Badge tone="gray">Neaktivan</Badge>}
                      </td>
                      <td
                        className="px-3 py-2 text-xs text-gray-500"
                        title={k.poslednjaPrijava ? formatirajVreme(k.poslednjaPrijava) : undefined}
                      >
                        {k.poslednjaPrijava ? relativnoVreme(k.poslednjaPrijava) : 'nikad'}
                      </td>
                      <td className="px-3 py-2 text-right">
                        <div className="flex justify-end gap-1">
                          <Button variant="ghost" size="sm" onClick={() => otvoriFormu(k)}>
                            Izmeni
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setResetZa(k)}
                            aria-label={`Reset lozinke — ${k.korisnickoIme}`}
                            title="Reset lozinke"
                          >
                            <KeyRound size={12} aria-hidden />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => void promeniAktivnost(k)}
                            disabled={deaktiviraj.isPending || aktiviraj.isPending}
                          >
                            {k.aktivan ? 'Deaktiviraj' : 'Aktiviraj'}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </FetchingOverlay>
        )}
      </CardBody>

      {formaOtvorena && (
        <KorisnikForm
          // key remontira formu kad se promeni cilj (novi vs. konkretan korisnik),
          // pa se početno stanje osveži bez sync-effect-a.
          key={uredjivan?.id ?? 'novi'}
          open
          korisnik={uredjivan}
          saving={kreiraj.isPending || izmeni.isPending}
          error={formaGreska}
          onSubmit={sacuvaj}
          onClose={() => setFormaOtvorena(false)}
        />
      )}

      {resetZa && <ResetLozinkeModal korisnik={resetZa} onClose={() => setResetZa(null)} />}
    </Card>
  );
}

/** Mali modal za admin reset tuđe lozinke (`POST /korisnici/{id}/reset-lozinka`). */
function ResetLozinkeModal({ korisnik, onClose }: { korisnik: KorisnikDto; onClose: () => void }) {
  const reset = useResetLozinke();
  const toast = useToast();
  const [novaLozinka, setNovaLozinka] = useState('');
  const [greska, setGreska] = useState<string | null>(null);

  async function potvrdi(e: FormEvent) {
    e.preventDefault();
    if (novaLozinka.length < 8) {
      setGreska('Lozinka mora imati najmanje 8 znakova.');
      return;
    }
    setGreska(null);
    try {
      await reset.mutateAsync({ id: korisnik.id, input: { novaLozinka } });
      toast.success('Lozinka je resetovana.');
      onClose();
    } catch (err) {
      setGreska(porukaGreske(err, 'Reset lozinke nije uspeo.'));
    }
  }

  return (
    <Modal open title={`Reset lozinke — ${korisnik.korisnickoIme}`} onClose={onClose}>
      <form onSubmit={potvrdi} className="space-y-4" noValidate>
        <FormField
          label="Nova lozinka"
          htmlFor="reset-nova-lozinka"
          error={greska}
          hint="Najmanje 8 znakova, bar jedna cifra, veliko i malo slovo. Korisnik će biti odjavljen sa svih uređaja."
        >
          <Input
            id="reset-nova-lozinka"
            type="password"
            value={novaLozinka}
            onChange={(e) => setNovaLozinka(e.target.value)}
            autoComplete="new-password"
            hasError={!!greska}
          />
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={reset.isPending}>
            Odustani
          </Button>
          <Button type="submit" disabled={reset.isPending}>
            {reset.isPending ? 'Čuvanje…' : 'Resetuj lozinku'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
