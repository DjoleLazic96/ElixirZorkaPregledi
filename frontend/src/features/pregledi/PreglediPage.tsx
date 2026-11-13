import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Download, FileSearch } from 'lucide-react';
import { useToast } from '../../app/providers/ToastProvider';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody } from '../../components/ui/Card';
import { DatePicker } from '../../components/ui/DatePicker';
import { EmptyState } from '../../components/ui/EmptyState';
import { FetchingOverlay } from '../../components/ui/FetchingOverlay';
import { Input } from '../../components/ui/Input';
import { PageHeader } from '../../components/ui/PageHeader';
import { Pagination } from '../../components/ui/Pagination';
import { Select } from '../../components/ui/Select';
import { Ucitavanje } from '../../components/ui/Spinner';
import { porukaGreske } from '../../lib/apiError';
import { formatirajDatum, formatirajVreme } from '../../lib/datum';
import { preuzmiFajl } from '../../lib/download';
import { useInzenjeri } from '../raspored/hooks';
import { useAuth } from '../auth/AuthProvider';
import { imaPravo } from '../../lib/permissions';
import { STATUSI, parametri, statusLabela, tonStatusa, usePregledi, type PreglediFilteri } from './hooks';

const TIPOVI = ['DP1', 'DP2', 'PZ1', 'PZ2', 'PZ3', 'PZ4'].map((t) => ({ value: t, label: t }));

/**
 * Arhiva pregleda (R6.1, R8.13): svi vide sve preglede; filteri datum od–do, tip, status, inženjer i broj; izvoz uzima
 * filtrirano. Računar: tabela; telefon: kartice (spec §8.1).
 */
export function PreglediPage() {
  const { me } = useAuth();
  // Inženjer podrazumevano vidi samo svoje preglede (zamerka 35); filter „Inženjer“ se isključuje za sve.
  const [f, setF] = useState<PreglediFilteri>(() => ({
    od: '', do: '', tip: null, status: null, inzenjerId: me && imaPravo(me, 'pregledi.popunjavanje') ? me.user.id : null, pretraga: '', stranica: 1, velicina: 50,
  }));
  const pregledi = usePregledi(f);
  const inzenjeri = useInzenjeri();
  const toast = useToast();
  const [preuzima, setPreuzima] = useState(false);
  const postavi = (izmena: Partial<PreglediFilteri>) => setF({ ...f, ...izmena, stranica: 1 });

  async function excel() {
    setPreuzima(true);
    try {
      await preuzmiFajl('/izvoz/pregledi.xlsx', parametri(f), 'Pregledi.xlsx');
    } catch (err) {
      toast.error(porukaGreske(err, 'Izvoz nije uspeo.'));
    } finally {
      setPreuzima(false);
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Pregledi"
        actions={<Button variant="secondary" onClick={() => void excel()} disabled={preuzima}><Download size={14} aria-hidden /> {preuzima ? 'Preuzimanje…' : 'Preuzmi Excel'}</Button>}
      />
      <Card>
        <CardBody className="grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
          <Input aria-label="Broj pregleda" placeholder="Broj, npr. DP1-2026-0142" value={f.pretraga} onChange={(e) => postavi({ pretraga: e.target.value })} />
          <DatePicker aria-label="Od" value={f.od} onChange={(v) => postavi({ od: v })} placeholder="Od" />
          <DatePicker aria-label="Do" value={f.do} onChange={(v) => postavi({ do: v })} placeholder="Do" min={f.od || undefined} />
          <Select aria-label="Tip" value={f.tip} options={TIPOVI} placeholder="Svi tipovi" nullable onChange={(v) => postavi({ tip: v })} />
          <Select aria-label="Status" value={f.status} options={STATUSI} placeholder="Svi statusi" nullable onChange={(v) => postavi({ status: v })} />
          <Select aria-label="Inženjer" value={f.inzenjerId} options={(inzenjeri.data ?? []).map((i) => ({ value: i.id, label: i.ime }))}
            placeholder="Svi inženjeri" nullable onChange={(v) => postavi({ inzenjerId: v })} />
        </CardBody>
      </Card>

      {pregledi.isLoading ? <Ucitavanje /> : pregledi.isError ? (
        <p role="alert" className="text-sm text-red-600">{porukaGreske(pregledi.error, 'Arhiva nije učitana.')}</p>
      ) : !pregledi.data?.stavke.length ? (
        <Card><CardBody><EmptyState icon={FileSearch} title="Nema pregleda za zadate filtere." /></CardBody></Card>
      ) : (
        <FetchingOverlay fetching={pregledi.isFetching}>
          <Card className="hidden xl:block">
            <table className="w-full text-sm">
              <thead className="border-b border-gray-200 bg-gray-50 text-left text-[13px] text-gray-500">
                <tr>{['Broj', 'Datum', 'Inženjer', 'Status', 'Neispravnih', 'Predat', 'Overio'].map((h) => <th key={h} className="px-3 py-2 font-medium">{h}</th>)}</tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {pregledi.data.stavke.map((p) => (
                  <tr key={p.id} className="hover:bg-gray-50">
                    <td className="whitespace-nowrap px-3 py-2 font-medium"><Link className="text-primary-700 hover:underline" to={`/pregledi/${p.id}`}>{p.broj}</Link>{p.dorada > 0 && <span className="ml-1 text-xs text-gray-500">dorada {p.dorada}</span>}</td>
                    <td className="whitespace-nowrap px-3 py-2">{formatirajDatum(p.planiraniDatum)}</td>
                    <td className="px-3 py-2">{p.inzenjer}</td>
                    <td className="px-3 py-2"><Badge tone={tonStatusa(p.status)}>{statusLabela(p.status)}</Badge></td>
                    <td className="px-3 py-2">{p.neispravnih > 0 ? <span className="font-semibold text-red-700">{p.neispravnih}</span> : '0'}</td>
                    <td className="whitespace-nowrap px-3 py-2">{p.predatAt ? formatirajVreme(p.predatAt) : '—'}</td>
                    <td className="px-3 py-2">{p.overio ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
          <ul className="space-y-2 xl:hidden">
            {pregledi.data.stavke.map((p) => (
              <li key={p.id}>
                <Link to={`/pregledi/${p.id}`} className="block rounded-md border border-gray-200 bg-white p-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="whitespace-nowrap font-semibold text-primary-700">{p.broj}</span>
                    <Badge tone={tonStatusa(p.status)}>{statusLabela(p.status)}</Badge>
                  </div>
                  <p className="mt-1 text-xs text-gray-600">{formatirajDatum(p.planiraniDatum)} · {p.inzenjer}{p.neispravnih > 0 && ` · ${p.neispravnih} neispravnih`}</p>
                </Link>
              </li>
            ))}
          </ul>
          <div className="mt-3">
            <Pagination page={f.stranica} pageSize={f.velicina} total={pregledi.data.ukupno}
              onPageChange={(s) => setF({ ...f, stranica: s })} onPageSizeChange={(v) => setF({ ...f, velicina: v, stranica: 1 })} />
          </div>
        </FetchingOverlay>
      )}
    </div>
  );
}
