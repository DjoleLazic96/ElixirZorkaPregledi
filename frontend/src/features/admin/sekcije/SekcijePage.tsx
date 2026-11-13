import { useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ArrowDown, ArrowUp } from 'lucide-react';
import { api } from '../../../api/client';
import type { SekcijaDto } from '../../../api/types';
import { useToast } from '../../../app/providers/ToastProvider';
import { Badge } from '../../../components/ui/Badge';
import { Button } from '../../../components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '../../../components/ui/Card';
import { Checkbox } from '../../../components/ui/Checkbox';
import { FormField } from '../../../components/ui/FormField';
import { Input } from '../../../components/ui/Input';
import { Modal } from '../../../components/ui/Modal';
import { Ucitavanje } from '../../../components/ui/Spinner';
import { porukaGreske } from '../../../lib/apiError';
import { useSekcije } from '../../sabloni/hooks';

/** Sekcije menjaju registar, šablone i filtere svuda, pa se posle izmene sve to učitava iznova. */
function useSekcijeIzmena() {
  const qc = useQueryClient();
  const posle = () => ['sekcije', 'uredjaji', 'sabloni'].forEach((k) => void qc.invalidateQueries({ queryKey: [k] }));
  const nova = useMutation({
    mutationFn: async (v: { sifra: string; naziv: string | null }) => (await api.post<SekcijaDto>('/sekcije', v)).data,
    onSuccess: posle,
  });
  const izmena = useMutation({
    mutationFn: async (v: { id: number; naziv: string; aktivna: boolean }) => (await api.put<SekcijaDto>(`/sekcije/${v.id}`, { naziv: v.naziv, aktivna: v.aktivna })).data,
    onSuccess: posle,
  });
  const redosled = useMutation({
    mutationFn: async (ids: number[]) => (await api.put<SekcijaDto[]>('/sekcije/redosled', { ids })).data,
    onSuccess: posle,
  });
  return { nova, izmena, redosled };
}

/**
 * Sekcije pogona (zamerka 33, samo Admin): dodavanje, preimenovanje, redosled i „neaktivna“. Brisanja nema — na sekciju
 * pokazuju uređaji i stari pregledi; neaktivna se samo ne nudi za nove uređaje. Šifra se ne menja.
 */
export function SekcijePage() {
  const sekcije = useSekcije();
  const { nova, izmena, redosled } = useSekcijeIzmena();
  const toast = useToast();
  const [forma, setForma] = useState<SekcijaDto | 'nova' | null>(null);
  const lista = sekcije.data ?? [];

  async function pomeri(i: number, smer: -1 | 1) {
    const ids = lista.map((s) => s.id);
    [ids[i], ids[i + smer]] = [ids[i + smer], ids[i]];
    try {
      await redosled.mutateAsync(ids);
    } catch (err) {
      toast.error(porukaGreske(err, 'Redosled nije sačuvan.'));
    }
  }

  return (
    <Card>
      <CardHeader className="flex flex-wrap items-center justify-between gap-2">
        <CardTitle>Sekcije</CardTitle>
        <Button size="sm" onClick={() => setForma('nova')}>Nova sekcija</Button>
      </CardHeader>
      <CardBody>
        <p className="mb-3 text-sm text-gray-600">Neaktivna sekcija se ne nudi za nove uređaje; postojeći uređaji i stari pregledi ostaju u njoj.</p>
        {sekcije.isLoading ? <Ucitavanje /> : (
          <ul className="divide-y divide-gray-100">
            {lista.map((s, i) => (
              <li key={s.id} className="flex flex-wrap items-center gap-2 py-2">
                <span className="w-14 font-semibold">{s.sifra}</span>
                <span className="min-w-0 flex-1 break-words">{s.naziv}</span>
                {s.aktivna === false && <Badge>neaktivna</Badge>}
                <Button variant="ghost" size="sm" aria-label={`Pomeri ${s.naziv} gore`} disabled={i === 0 || redosled.isPending} onClick={() => void pomeri(i, -1)}><ArrowUp size={14} aria-hidden /></Button>
                <Button variant="ghost" size="sm" aria-label={`Pomeri ${s.naziv} dole`} disabled={i === lista.length - 1 || redosled.isPending} onClick={() => void pomeri(i, 1)}><ArrowDown size={14} aria-hidden /></Button>
                <Button variant="ghost" size="sm" onClick={() => setForma(s)}>Izmeni</Button>
              </li>
            ))}
          </ul>
        )}
      </CardBody>
      {forma && (
        <SekcijaForma
          sekcija={forma === 'nova' ? null : forma}
          saving={nova.isPending || izmena.isPending}
          onClose={() => setForma(null)}
          onSubmit={async (v) => {
            if (forma === 'nova') await nova.mutateAsync({ sifra: v.sifra, naziv: v.naziv || null });
            else await izmena.mutateAsync({ id: forma.id, naziv: v.naziv, aktivna: v.aktivna });
            toast.success(forma === 'nova' ? 'Sekcija je dodata.' : 'Sekcija je izmenjena.');
            setForma(null);
          }}
        />
      )}
    </Card>
  );
}

function SekcijaForma({ sekcija, saving, onClose, onSubmit }: {
  sekcija: SekcijaDto | null; saving: boolean; onClose: () => void;
  onSubmit: (v: { sifra: string; naziv: string; aktivna: boolean }) => Promise<void>;
}) {
  const [sifra, setSifra] = useState(sekcija?.sifra ?? '');
  const [naziv, setNaziv] = useState(sekcija?.naziv ?? '');
  const [aktivna, setAktivna] = useState(sekcija?.aktivna !== false);
  const [greska, setGreska] = useState<string | null>(null);

  async function potvrdi(e: FormEvent) {
    e.preventDefault();
    if (!sekcija && !sifra.trim()) return setGreska('Šifra je obavezna.');
    if (sekcija && !naziv.trim()) return setGreska('Naziv je obavezan.');
    try {
      await onSubmit({ sifra: sifra.trim(), naziv: naziv.trim(), aktivna });
    } catch (err) {
      setGreska(porukaGreske(err, 'Sekcija nije sačuvana.'));
    }
  }

  return (
    <Modal open title={sekcija ? `Sekcija ${sekcija.sifra}` : 'Nova sekcija'} onClose={onClose}>
      <form onSubmit={potvrdi} className="space-y-4" noValidate>
        {!sekcija && (
          <FormField label="Šifra" htmlFor="sek-sifra" hint="Kao na obrascu, npr. 80. Kasnije se ne menja.">
            <Input id="sek-sifra" value={sifra} onChange={(e) => setSifra(e.target.value)} maxLength={8} />
          </FormField>
        )}
        <FormField label="Naziv" htmlFor="sek-naziv" hint={sekcija ? undefined : 'Prazno = „Sekcija {šifra}“.'}>
          <Input id="sek-naziv" value={naziv} onChange={(e) => setNaziv(e.target.value)} maxLength={100} />
        </FormField>
        {sekcija && <Checkbox label="Aktivna (nudi se za nove uređaje)" checked={aktivna} onChange={(e) => setAktivna(e.target.checked)} />}
        {greska && <p role="alert" className="text-sm text-red-600">{greska}</p>}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={saving}>Odustani</Button>
          <Button type="submit" disabled={saving}>{saving ? 'Čuvanje…' : 'Sačuvaj'}</Button>
        </div>
      </form>
    </Modal>
  );
}
