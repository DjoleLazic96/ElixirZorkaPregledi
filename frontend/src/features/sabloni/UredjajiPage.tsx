import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { Wrench } from 'lucide-react';
import { FilterTraka, type AktivanFilter } from '../../components/ui/FilterTraka';
import type { UredjajDto, UredjajInput } from '../../api/types';
import { useToast } from '../../app/providers/ToastProvider';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card, CardBody } from '../../components/ui/Card';
import { Checkbox } from '../../components/ui/Checkbox';
import { EmptyState } from '../../components/ui/EmptyState';
import { FetchingOverlay } from '../../components/ui/FetchingOverlay';
import { FormField } from '../../components/ui/FormField';
import { Input } from '../../components/ui/Input';
import { Modal } from '../../components/ui/Modal';
import { PageHeader } from '../../components/ui/PageHeader';
import { Select } from '../../components/ui/Select';
import { Ucitavanje } from '../../components/ui/Spinner';
import { porukaGreske } from '../../lib/apiError';
import { canEditOprema } from '../../lib/permissions';
import { useAuth } from '../auth/AuthProvider';
import { usePromeniStatusUredjaja, useSacuvajUredjaj, useSekcije, useUredjaji, type UredjajiFilteri } from './hooks';

/**
 * Registar opreme (R2.2, R2.3, R2.10). Svi vide; uređaje dodaje, menja i stavlja van upotrebe ko ima
 * „sabloni.nacrt“. Uređaj se nikad ne briše. Kartice umesto tabele, pa ekran radi i na telefonu.
 */
export function UredjajiPage() {
  const { me } = useAuth();
  const smeDaMenja = canEditOprema(me ?? { roles: [], prava: [] });
  const [f, setF] = useState<UredjajiFilteri>({ sekcijaId: null, status: 'aktivan', pretraga: '', bezSablona: false });
  const uredjaji = useUredjaji(f);
  const sekcije = useSekcije();
  const sacuvaj = useSacuvajUredjaj();
  const status = usePromeniStatusUredjaja();
  const toast = useToast();
  const [forma, setForma] = useState<UredjajDto | 'novi' | null>(null);
  const [greska, setGreska] = useState<string | null>(null);

  async function potvrdi(id: number | null, input: UredjajInput) {
    setGreska(null);
    try {
      await sacuvaj.mutateAsync({ id, input });
      toast.success(id === null ? 'Uređaj je dodat u registar.' : 'Uređaj je izmenjen.');
      setForma(null);
    } catch (err) {
      setGreska(porukaGreske(err, 'Čuvanje uređaja nije uspelo.'));
    }
  }

  async function promeniStatus(u: UredjajDto) {
    try {
      await status.mutateAsync(u);
      toast.success(u.status === 'aktivan' ? 'Uređaj je van upotrebe.' : 'Uređaj je vraćen u upotrebu.');
    } catch (err) {
      toast.error(porukaGreske(err, 'Promena statusa nije uspela.'));
    }
  }

  const opcijeSekcija = (sekcije.data ?? []).map((s) => ({ value: s.id, label: `Sekcija ${s.sifra}` }));
  const statusiUredjaja = [{ value: 'aktivan', label: 'U upotrebi' }, { value: 'van_upotrebe', label: 'Van upotrebe' }];
  const aktivni: AktivanFilter[] = [
    ...(f.pretraga.trim() ? [{ kljuc: 'pretraga', labela: `„${f.pretraga.trim()}“`, kucanje: true, ukloni: () => setF((x) => ({ ...x, pretraga: '' })) }] : []),
    ...(f.sekcijaId ? [{ kljuc: 'sekcija', labela: opcijeSekcija.find((x) => x.value === f.sekcijaId)?.label ?? 'Sekcija', ukloni: () => setF((x) => ({ ...x, sekcijaId: null })) }] : []),
    ...(f.status ? [{ kljuc: 'status', labela: statusiUredjaja.find((x) => x.value === f.status)?.label ?? f.status, ukloni: () => setF((x) => ({ ...x, status: null })) }] : []),
    ...(f.bezSablona ? [{ kljuc: 'bez', labela: 'Nije ni u jednom pregledu', ukloni: () => setF((x) => ({ ...x, bezSablona: false })) }] : []),
  ];
  // Za nov uređaj samo aktivne sekcije (zamerka 33); postojeći zadržava svoju i kad je neaktivna.
  const sekcijeZaFormu = (sekcije.data ?? [])
    .filter((s) => s.aktivna !== false || (forma !== null && forma !== 'novi' && forma.sekcijaId === s.id))
    .map((s) => ({ value: s.id, label: `Sekcija ${s.sifra}` }));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Uređaji"
        description="Centralni registar opreme: svaki uređaj postoji jednom, a šabloni pokazuju na njega."
        actions={
          smeDaMenja ? (
            <Button
              onClick={() => {
                setGreska(null);
                setForma('novi');
              }}
            >
              Novi uređaj
            </Button>
          ) : undefined
        }
      />

      <FilterTraka aktivni={aktivni} onObrisiSve={() => setF((x) => ({ ...x, pretraga: '', sekcijaId: null, status: null, bezSablona: false }))}>
          <Input
            aria-label="Pretraga uređaja"
            placeholder="Naziv ili oznaka…"
            value={f.pretraga}
            onChange={(e) => setF({ ...f, pretraga: e.target.value })}
          />
          <Select
            aria-label="Sekcija"
            value={f.sekcijaId}
            options={opcijeSekcija}
            placeholder="Sve sekcije"
            nullable
            onChange={(v) => setF({ ...f, sekcijaId: v })}
          />
          <Select
            aria-label="Status"
            value={f.status}
            options={statusiUredjaja}
            placeholder="Svi statusi"
            nullable
            onChange={(v) => setF({ ...f, status: v })}
          />
          <Checkbox
            label="Nije ni u jednom pregledu"
            checked={f.bezSablona}
            onChange={(e) => setF({ ...f, bezSablona: e.target.checked })}
          />
      </FilterTraka>

      {uredjaji.isLoading ? (
        <Ucitavanje />
      ) : uredjaji.isError ? (
        <p role="alert" className="text-sm text-red-600">
          {porukaGreske(uredjaji.error, 'Registar nije učitan.')}
        </p>
      ) : !uredjaji.data || uredjaji.data.length === 0 ? (
        <Card>
          <CardBody>
            <EmptyState icon={Wrench} title="Nema uređaja za zadate filtere." />
          </CardBody>
        </Card>
      ) : (
        <FetchingOverlay fetching={uredjaji.isFetching}>
          <p className="mb-2 text-sm text-gray-500">{uredjaji.data.length} uređaja</p>
          <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {uredjaji.data.map((u) => (
              <li key={u.id}>
                <Card className="h-full">
                  <CardBody className="flex h-full flex-col gap-2">
                    <p className="break-words font-medium text-gray-900">
                      <Link to={`/uredjaji/${u.id}`} className="hover:text-primary-700 hover:underline">
                        {u.naziv} {u.oznaka && <span className="whitespace-nowrap text-gray-600">{u.oznaka}</span>}
                      </Link>
                    </p>
                    <p className="text-xs text-gray-500">
                      Sekcija {u.sekcija} · {u.vrsta}
                    </p>
                    <div className="flex flex-wrap gap-1">
                      {u.status === 'van_upotrebe' && <Badge tone="amber">van upotrebe</Badge>}
                      {u.sabloni.length === 0 ? (
                        <Badge tone="gray">nije ni u jednom pregledu</Badge>
                      ) : (
                        u.sabloni.map((t) => (
                          <Badge key={t} tone="primary">
                            {t}
                          </Badge>
                        ))
                      )}
                    </div>
                    {smeDaMenja && (
                      <div className="mt-auto flex flex-wrap justify-end gap-1 pt-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            setGreska(null);
                            setForma(u);
                          }}
                        >
                          Izmeni
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => void promeniStatus(u)} disabled={status.isPending}>
                          {u.status === 'aktivan' ? 'Van upotrebe' : 'Vrati u upotrebu'}
                        </Button>
                      </div>
                    )}
                  </CardBody>
                </Card>
              </li>
            ))}
          </ul>
        </FetchingOverlay>
      )}

      {forma && (
        <UredjajForma
          key={forma === 'novi' ? 'novi' : forma.id}
          uredjaj={forma === 'novi' ? null : forma}
          opcijeSekcija={sekcijeZaFormu}
          saving={sacuvaj.isPending}
          greska={greska}
          onSubmit={(input) => void potvrdi(forma === 'novi' ? null : forma.id, input)}
          onClose={() => setForma(null)}
        />
      )}
    </div>
  );
}

interface UredjajFormaProps {
  uredjaj: UredjajDto | null;
  opcijeSekcija: { value: number; label: string }[];
  saving: boolean;
  greska: string | null;
  onSubmit: (input: UredjajInput) => void;
  onClose: () => void;
  /** Sekcija izabrana unapred za nov uređaj (npr. iz „Dodaj stavku“ u nacrtu). */
  pocetnaSekcija?: number | null;
}

export function UredjajForma({ uredjaj, opcijeSekcija, saving, greska, onSubmit, onClose, pocetnaSekcija = null }: UredjajFormaProps) {
  const [naziv, setNaziv] = useState(uredjaj?.naziv ?? '');
  const [oznaka, setOznaka] = useState(uredjaj?.oznaka ?? '');
  const [sekcijaId, setSekcijaId] = useState<number | null>(uredjaj?.sekcijaId ?? pocetnaSekcija);
  const [vrsta, setVrsta] = useState(uredjaj?.vrsta ?? '');

  const potvrdi = (e: FormEvent) => {
    e.preventDefault();
    onSubmit({
      naziv: naziv.trim(),
      oznaka: oznaka.trim(),
      sekcijaId: sekcijaId ?? 0,
      vrsta: (vrsta.trim() || naziv.trim().split(' ')[0]) ?? '',
    });
  };

  return (
    <Modal open title={uredjaj ? 'Izmena uređaja' : 'Novi uređaj'} onClose={onClose}>
      <form onSubmit={potvrdi} className="space-y-4" noValidate>
        <FormField label="Naziv" htmlFor="ur-naziv">
          <Input id="ur-naziv" value={naziv} onChange={(e) => setNaziv(e.target.value)} />
        </FormField>
        <FormField label="Oznaka" htmlFor="ur-oznaka" hint="Prazno ako uređaj nema oznaku.">
          <Input id="ur-oznaka" value={oznaka} onChange={(e) => setOznaka(e.target.value)} />
        </FormField>
        <FormField label="Sekcija" htmlFor="ur-sekcija">
          <Select id="ur-sekcija" value={sekcijaId} options={opcijeSekcija} placeholder="Izaberite sekciju" onChange={setSekcijaId} />
        </FormField>
        <FormField label="Vrsta" htmlFor="ur-vrsta" hint="Npr. Transporter, Redler, Ventilator; prazno = prva reč naziva.">
          <Input id="ur-vrsta" value={vrsta} onChange={(e) => setVrsta(e.target.value)} />
        </FormField>
        {greska && (
          <p role="alert" className="text-sm text-red-600">
            {greska}
          </p>
        )}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={saving}>
            Odustani
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Čuvanje…' : 'Sačuvaj'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
