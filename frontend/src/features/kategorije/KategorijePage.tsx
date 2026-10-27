/**
 * PRIMER feature-a — obrazac za sve ostale; kopiraj ili obriši.
 *
 * Paginirani šifarnik sa server-side pretragom i sortiranjem. Pokazuje ceo
 * standard (blueprint 13/14): URL kao izvor istine za filtere (`useSearchParams`),
 * četiri stanja ekrana (učitavanje / greška / prazno / podaci), `SortableTh`,
 * `Pagination`, `FetchingOverlay`, confirm pre deaktivacije, toast posle svake
 * mutacije, prezentaciona forma remontirana preko `key`, akcije skrivene (ne
 * `disabled`) kad rola nema pravo.
 */
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Plus, Tag } from 'lucide-react';
import type { KategorijaDto, KategorijaInput, KategorijaSort, KategorijeFilteri, Smer } from '../../api/types';
import { useConfirm } from '../../app/providers/ConfirmProvider';
import { useToast } from '../../app/providers/ToastProvider';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody } from '../../components/ui/Card';
import { Checkbox } from '../../components/ui/Checkbox';
import { EmptyState } from '../../components/ui/EmptyState';
import { FetchingOverlay } from '../../components/ui/FetchingOverlay';
import { Input } from '../../components/ui/Input';
import { PageHeader } from '../../components/ui/PageHeader';
import { Pagination } from '../../components/ui/Pagination';
import { SortableTh } from '../../components/ui/SortableTh';
import { Ucitavanje } from '../../components/ui/Spinner';
import { greskePoPolju, porukaGreske } from '../../lib/apiError';
import { cn } from '../../lib/cn';
import { canEditKategorije } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import {
  useAktivirajKategoriju,
  useDeaktivirajKategoriju,
  useIzmeniKategoriju,
  useKategorije,
  useKreirajKategoriju,
} from './hooks';
import { KategorijaForm } from './KategorijaForm';
import { KATEGORIJA_MNOZINA, PORUKE } from './labele';

const SORT_KOLONE: ReadonlyArray<KategorijaSort> = ['sifra', 'naziv'];

export function KategorijePage() {
  const { me } = useAuth();
  const smeDaMenja = canEditKategorije(me?.roles ?? []);
  const toast = useToast();
  const confirm = useConfirm();

  // Filteri žive u URL-u: link se može podeliti, a F5 ne gubi stanje.
  const [sp, setSp] = useSearchParams();
  const pretraga = sp.get('pretraga') ?? '';
  const prikaziNeaktivne = sp.get('neaktivne') === '1';
  const stranica = Math.max(1, Number(sp.get('stranica')) || 1);
  const velicina = Number(sp.get('velicina')) || 50;
  const sortParam = sp.get('sort');
  const sort: KategorijaSort = SORT_KOLONE.includes(sortParam as KategorijaSort)
    ? (sortParam as KategorijaSort)
    : 'sifra';
  const smer: Smer = sp.get('smer') === 'desc' ? 'desc' : 'asc';

  /** Upis u URL; prazna vrednost briše param. Promena filtera resetuje stranicu. */
  function postavi(izmene: Record<string, string | null>, resetujStranicu = true) {
    setSp(
      (prev) => {
        const sledeci = new URLSearchParams(prev);
        for (const [k, v] of Object.entries(izmene)) {
          if (v === null || v === '') sledeci.delete(k);
          else sledeci.set(k, v);
        }
        if (resetujStranicu) sledeci.delete('stranica');
        return sledeci;
      },
      { replace: true },
    );
  }

  const filteri: KategorijeFilteri = {
    stranica,
    velicina,
    sort,
    smer,
    pretraga: pretraga.trim() || undefined,
    aktivan: prikaziNeaktivne ? undefined : true,
  };
  const upit = useKategorije(filteri);
  const kreiraj = useKreirajKategoriju();
  const izmeni = useIzmeniKategoriju();
  const deaktiviraj = useDeaktivirajKategoriju();
  const aktiviraj = useAktivirajKategoriju();

  const [formaOtvorena, setFormaOtvorena] = useState(false);
  const [uredjivana, setUredjivana] = useState<KategorijaDto | null>(null);
  const [formaGreska, setFormaGreska] = useState<string | null>(null);
  const [formaGreskePolja, setFormaGreskePolja] = useState<Record<string, string>>({});

  function otvoriFormu(k: KategorijaDto | null) {
    setUredjivana(k);
    setFormaGreska(null);
    setFormaGreskePolja({});
    setFormaOtvorena(true);
  }

  async function sacuvaj(input: KategorijaInput) {
    setFormaGreska(null);
    setFormaGreskePolja({});
    try {
      if (uredjivana) {
        await izmeni.mutateAsync({ id: uredjivana.id, input });
        toast.success(PORUKE.izmenjena);
      } else {
        await kreiraj.mutateAsync(input);
        toast.success(PORUKE.kreirana);
      }
      setFormaOtvorena(false);
    } catch (err) {
      // 400 → greške uz polja; 422 (duplikat šifre) i ostalo → jedna poruka pod formom.
      const poPolju = greskePoPolju(err);
      if (Object.keys(poPolju).length > 0) setFormaGreskePolja(poPolju);
      else setFormaGreska(porukaGreske(err, 'Čuvanje kategorije nije uspelo.'));
    }
  }

  async function promeniAktivnost(k: KategorijaDto) {
    const ok = await confirm(
      k.aktivan
        ? {
            title: 'Deaktivacija kategorije',
            message: `Kategorija „${k.naziv}" (${k.sifra}) neće više biti dostupna za izbor. Postojeći podaci ostaju; kategorija se može ponovo aktivirati.`,
            confirmLabel: 'Deaktiviraj',
            tone: 'danger',
          }
        : {
            title: 'Aktivacija kategorije',
            message: `Kategorija „${k.naziv}" (${k.sifra}) će ponovo biti dostupna za izbor.`,
            confirmLabel: 'Aktiviraj',
            tone: 'normal',
          },
    );
    if (!ok) return;
    try {
      if (k.aktivan) await deaktiviraj.mutateAsync(k.id);
      else await aktiviraj.mutateAsync(k.id);
      toast.success(k.aktivan ? PORUKE.deaktivirana : PORUKE.aktivirana);
    } catch (err) {
      toast.error(porukaGreske(err, 'Promena statusa nije uspela.'));
    }
  }

  const stavke = upit.data?.stavke ?? [];
  const ukupno = upit.data?.ukupno ?? 0;
  const imaFiltera = pretraga.trim() !== '' || prikaziNeaktivne;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Kategorije"
        description="Primer šifarnika — obrazac za sve ostale ekrane."
        actions={
          smeDaMenja && (
            <Button onClick={() => otvoriFormu(null)}>
              <Plus size={14} aria-hidden />
              Nova kategorija
            </Button>
          )
        }
      />

      <Card>
        <CardBody className="space-y-4">
          <div className="grid items-end gap-3 md:grid-cols-4">
            <Input
              aria-label="Pretraga"
              value={pretraga}
              onChange={(e) => postavi({ pretraga: e.target.value })}
              placeholder="Pretraga po šifri ili nazivu…"
              className="md:col-span-2"
            />
            <Checkbox
              label="Prikaži i neaktivne"
              checked={prikaziNeaktivne}
              onChange={(e) => postavi({ neaktivne: e.target.checked ? '1' : null })}
              className="pb-2"
            />
          </div>

          {upit.isPending ? (
            <Ucitavanje />
          ) : upit.isError ? (
            <div className="flex items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              <span>Učitavanje kategorija nije uspelo.</span>
              <Button variant="secondary" size="sm" onClick={() => void upit.refetch()}>
                Pokušaj ponovo
              </Button>
            </div>
          ) : stavke.length === 0 ? (
            <EmptyState
              icon={Tag}
              title={imaFiltera ? PORUKE.nemaZaFiltere : PORUKE.josNema}
              description={imaFiltera || !smeDaMenja ? undefined : PORUKE.dodajPrvu}
              action={
                !imaFiltera &&
                smeDaMenja && (
                  <Button variant="secondary" onClick={() => otvoriFormu(null)}>
                    <Plus size={14} aria-hidden />
                    Nova kategorija
                  </Button>
                )
              }
            />
          ) : (
            <FetchingOverlay fetching={upit.isFetching}>
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="border-b border-gray-200 bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
                    <tr>
                      <SortableTh
                        column="sifra"
                        current={sort}
                        direction={smer}
                        onSort={(k, s) => postavi({ sort: k, smer: s })}
                      >
                        Šifra
                      </SortableTh>
                      <SortableTh
                        column="naziv"
                        current={sort}
                        direction={smer}
                        onSort={(k, s) => postavi({ sort: k, smer: s })}
                      >
                        Naziv
                      </SortableTh>
                      <th className="px-3 py-2 font-medium">Opis</th>
                      <th className="px-3 py-2 font-medium">Status</th>
                      {smeDaMenja && <th className="px-3 py-2 text-right font-medium">Akcije</th>}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {stavke.map((k) => (
                      <tr key={k.id} className={cn('hover:bg-gray-50', !k.aktivan && 'text-gray-400')}>
                        <td className="px-3 py-2 font-medium tabular-nums">{k.sifra}</td>
                        <td className="px-3 py-2">{k.naziv}</td>
                        <td className="max-w-md truncate px-3 py-2" title={k.opis ?? undefined}>
                          {k.opis ?? '—'}
                        </td>
                        <td className="px-3 py-2">
                          {k.aktivan ? <Badge tone="primary">Aktivna</Badge> : <Badge tone="gray">Neaktivna</Badge>}
                        </td>
                        {smeDaMenja && (
                          <td className="px-3 py-2 text-right">
                            <div className="flex justify-end gap-1">
                              <Button variant="ghost" size="sm" onClick={() => otvoriFormu(k)}>
                                Izmeni
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
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </FetchingOverlay>
          )}

          {!upit.isPending && !upit.isError && (
            <Pagination
              page={stranica}
              pageSize={velicina}
              total={ukupno}
              onPageChange={(s) => postavi({ stranica: String(s) }, false)}
              onPageSizeChange={(v) => postavi({ velicina: String(v) })}
              itemLabel={KATEGORIJA_MNOZINA}
            />
          )}
        </CardBody>
      </Card>

      {formaOtvorena && (
        <KategorijaForm
          // key remontira formu kad se promeni cilj (nova vs. konkretna kategorija).
          key={uredjivana?.id ?? 'nova'}
          open
          kategorija={uredjivana}
          saving={kreiraj.isPending || izmeni.isPending}
          error={formaGreska}
          fieldErrors={formaGreskePolja}
          onSubmit={sacuvaj}
          onClose={() => setFormaOtvorena(false)}
        />
      )}
    </div>
  );
}
