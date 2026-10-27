import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { AuditAkcija, AuditFilteri, AuditRedDto } from '../../api/types';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody } from '../../components/ui/Card';
import { DatePicker } from '../../components/ui/DatePicker';
import { EmptyState } from '../../components/ui/EmptyState';
import { FetchingOverlay } from '../../components/ui/FetchingOverlay';
import { FormField } from '../../components/ui/FormField';
import { Input } from '../../components/ui/Input';
import { Modal } from '../../components/ui/Modal';
import { PageHeader } from '../../components/ui/PageHeader';
import { Pagination } from '../../components/ui/Pagination';
import { Select } from '../../components/ui/Select';
import { Ucitavanje } from '../../components/ui/Spinner';
import { formatirajVremeSek, krajDanaUtc, pocetakDanaUtc } from '../../lib/datum';
import { DiffPrikaz } from './DiffPrikaz';
import { useAudit, useAuditEntiteti } from './hooks';
import { AKCIJE, akcijaLabela, akcijaTon } from './labele';

/**
 * Stranica „Audit log" (blueprint 08 §5, samo Admin). Filteri žive u URL-u
 * (`useSearchParams`) da bi se link mogao podeliti i osvežiti; svaka promena
 * filtera vraća na stranicu 1. Klik na red otvara modal sa `DiffPrikaz`.
 */
export function AuditLogPage() {
  const [sp, setSp] = useSearchParams();

  const korisnik = sp.get('korisnik') ?? '';
  const entitet = sp.get('entitet');
  const akcija = sp.get('akcija') as AuditAkcija | null;
  const od = sp.get('od') ?? '';
  const doDatum = sp.get('do') ?? '';
  const stranica = Math.max(1, Number(sp.get('stranica')) || 1);
  const velicina = Number(sp.get('velicina')) || 50;

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

  const filteri: AuditFilteri = {
    stranica,
    velicina,
    korisnik: korisnik.trim() || undefined,
    entitet: entitet ?? undefined,
    akcija: akcija ?? undefined,
    // Granice LOKALNOG dana kao UTC ISO — kolona je timestamptz, a zone-less
    // string bi Npgsql odbio (Kind=Unspecified → 500).
    od: od ? pocetakDanaUtc(od) : undefined,
    doDatum: doDatum ? krajDanaUtc(doDatum) : undefined,
  };

  const upit = useAudit(filteri);
  const entiteti = useAuditEntiteti();
  const [izabran, setIzabran] = useState<AuditRedDto | null>(null);

  const stavke = upit.data?.stavke ?? [];
  const ukupno = upit.data?.ukupno ?? 0;
  const opsegGreska = od && doDatum && od > doDatum;

  return (
    <div className="space-y-4">
      <PageHeader title="Audit log" description="Ko je, kada, šta i odakle promenio — izmene podataka i prijave." />

      <Card>
        <CardBody className="space-y-4">
          <div className="grid gap-3 md:grid-cols-5">
            <FormField label="Korisnik" htmlFor="audit-korisnik">
              <Input
                id="audit-korisnik"
                value={korisnik}
                onChange={(e) => postavi({ korisnik: e.target.value })}
                placeholder="korisničko ime…"
              />
            </FormField>
            <FormField label="Entitet" htmlFor="audit-entitet">
              <Select
                id="audit-entitet"
                value={entitet}
                options={(entiteti.data ?? []).map((e) => ({ value: e.entitet, label: e.labela }))}
                nullable
                placeholder="Svi entiteti"
                onChange={(v) => postavi({ entitet: v })}
              />
            </FormField>
            <FormField label="Akcija" htmlFor="audit-akcija">
              <Select
                id="audit-akcija"
                value={akcija}
                options={AKCIJE.map((a) => ({ value: a.value, label: a.label }))}
                nullable
                placeholder="Sve akcije"
                onChange={(v) => postavi({ akcija: v })}
              />
            </FormField>
            <FormField
              label="Od datuma"
              htmlFor="audit-od"
              error={opsegGreska ? 'Datum „od" mora biti pre ili jednak datumu „do".' : null}
            >
              <DatePicker id="audit-od" value={od} onChange={(v) => postavi({ od: v })} />
            </FormField>
            <FormField label="Do datuma" htmlFor="audit-do">
              <DatePicker id="audit-do" value={doDatum} onChange={(v) => postavi({ do: v })} />
            </FormField>
          </div>

          {upit.isPending ? (
            <Ucitavanje />
          ) : upit.isError ? (
            <div className="flex items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              <span>Učitavanje audit loga nije uspelo.</span>
              <Button variant="secondary" size="sm" onClick={() => void upit.refetch()}>
                Pokušaj ponovo
              </Button>
            </div>
          ) : stavke.length === 0 ? (
            <EmptyState title="Nema zapisa za zadate filtere." />
          ) : (
            <FetchingOverlay fetching={upit.isFetching}>
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="border-b border-gray-200 bg-gray-50 text-left text-xs uppercase tracking-wide text-gray-500">
                    <tr>
                      <th className="px-3 py-2 font-medium">Vreme</th>
                      <th className="px-3 py-2 font-medium">Korisnik</th>
                      <th className="px-3 py-2 font-medium">Akcija</th>
                      <th className="px-3 py-2 font-medium">Entitet</th>
                      <th className="px-3 py-2 font-medium">ID</th>
                      <th className="px-3 py-2 font-medium">IP</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {stavke.map((r) => (
                      <tr
                        key={r.id}
                        onClick={() => setIzabran(r)}
                        className="cursor-pointer hover:bg-gray-50"
                        title="Kliknite za detalje"
                      >
                        <td className="whitespace-nowrap px-3 py-2 align-top text-xs tabular-nums text-gray-500">
                          {formatirajVremeSek(r.vreme)}
                        </td>
                        <td className="px-3 py-2 align-top text-gray-700">{r.korisnik ?? '—'}</td>
                        <td className="px-3 py-2 align-top">
                          <Badge tone={akcijaTon(r.akcija)}>{akcijaLabela(r.akcija)}</Badge>
                        </td>
                        <td className="px-3 py-2 align-top text-gray-700">{r.entitetLabela}</td>
                        <td className="px-3 py-2 align-top tabular-nums text-gray-500">
                          {r.entitetId ?? '—'}
                        </td>
                        <td className="px-3 py-2 align-top text-xs text-gray-500">{r.ip ?? '—'}</td>
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
              itemLabel={{ jednina: 'zapis', paukal: 'zapisa', mnozina: 'zapisa' }}
            />
          )}
        </CardBody>
      </Card>

      {izabran && (
        <Modal
          open
          title={`Detalji — ${izabran.entitetLabela}`}
          onClose={() => setIzabran(null)}
          widthClass="max-w-lg"
        >
          <div className="space-y-4">
            <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-gray-600">
              <dt>Vreme</dt>
              <dd className="text-gray-800">{formatirajVremeSek(izabran.vreme)}</dd>
              <dt>Korisnik</dt>
              <dd className="text-gray-800">{izabran.korisnik ?? '—'}</dd>
              <dt>Akcija</dt>
              <dd className="text-gray-800">{akcijaLabela(izabran.akcija)}</dd>
              <dt>ID zapisa</dt>
              <dd className="text-gray-800">{izabran.entitetId ?? '—'}</dd>
              <dt>IP adresa</dt>
              <dd className="text-gray-800">{izabran.ip ?? '—'}</dd>
            </dl>
            <DiffPrikaz
              akcija={izabran.akcija}
              stareVrednosti={izabran.stareVrednosti}
              noveVrednosti={izabran.noveVrednosti}
            />
          </div>
        </Modal>
      )}
    </div>
  );
}
