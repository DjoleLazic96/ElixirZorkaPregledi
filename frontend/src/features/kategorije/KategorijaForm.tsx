/**
 * PRIMER feature-a — obrazac za sve ostale; kopiraj ili obriši.
 *
 * Prezentaciona forma u modalu (blueprint 13 §6): stanje polja iz props-a pri
 * mount-u, roditelj remontira sa `key={kategorija?.id ?? 'nova'}`. Minimalne
 * klijentske provere (obavezno, dužina); server je autoritativan — 400 stiže kao
 * `fieldErrors`, 422/500 kao `error`. Tehnički props su engleski, domenski (`kategorija`) srpski.
 */
import { useState, type FormEvent } from 'react';
import type { KategorijaDto, KategorijaInput } from '../../api/types';
import { Button } from '../../components/ui/Button';
import { FormField } from '../../components/ui/FormField';
import { Input } from '../../components/ui/Input';
import { Modal } from '../../components/ui/Modal';
import { Textarea } from '../../components/ui/Textarea';

interface KategorijaFormProps {
  open: boolean;
  /** null → kreiranje; inače izmena. */
  kategorija: KategorijaDto | null;
  saving: boolean;
  /** Opšta greška sa servera (422 pravilo, 500). */
  error: string | null;
  /** Greške po polju iz 400 odgovora (`greskePoPolju`). */
  fieldErrors?: Record<string, string>;
  onSubmit: (input: KategorijaInput) => void;
  onClose: () => void;
}

interface Greske {
  sifra?: string;
  naziv?: string;
}

const MAX_SIFRA = 20;
const MAX_NAZIV = 100;

export function KategorijaForm({
  open,
  kategorija,
  saving,
  error,
  fieldErrors = {},
  onSubmit,
  onClose,
}: KategorijaFormProps) {
  const jeIzmena = kategorija !== null;
  const [sifra, setSifra] = useState(kategorija?.sifra ?? '');
  const [naziv, setNaziv] = useState(kategorija?.naziv ?? '');
  const [opis, setOpis] = useState(kategorija?.opis ?? '');
  const [greske, setGreske] = useState<Greske>({});

  // Lokalna provera ima prednost dok korisnik ne ispravi polje; onda se vidi serverska.
  const greskaSifra = greske.sifra ?? fieldErrors.sifra;
  const greskaNaziv = greske.naziv ?? fieldErrors.naziv;

  function submit(e: FormEvent) {
    e.preventDefault();
    const s = sifra.trim();
    const n = naziv.trim();
    const nove: Greske = {};
    if (s === '') nove.sifra = 'Šifra je obavezna.';
    else if (s.length > MAX_SIFRA) nove.sifra = `Šifra može imati najviše ${MAX_SIFRA} znakova.`;
    if (n === '') nove.naziv = 'Naziv je obavezan.';
    else if (n.length > MAX_NAZIV) nove.naziv = `Naziv može imati najviše ${MAX_NAZIV} znakova.`;
    setGreske(nove);
    if (Object.keys(nove).length > 0) return;
    onSubmit({ sifra: s, naziv: n, opis: opis.trim() === '' ? null : opis.trim() });
  }

  return (
    <Modal open={open} title={jeIzmena ? 'Izmena kategorije' : 'Nova kategorija'} onClose={onClose}>
      <form onSubmit={submit} className="space-y-4" noValidate>
        <FormField label="Šifra" htmlFor="kategorija-sifra" error={greskaSifra}>
          <Input
            id="kategorija-sifra"
            value={sifra}
            onChange={(e) => {
              setSifra(e.target.value);
              setGreske((g) => ({ ...g, sifra: undefined }));
            }}
            placeholder="npr. KAT-01"
            maxLength={MAX_SIFRA}
            autoComplete="off"
            hasError={!!greskaSifra}
          />
        </FormField>

        <FormField label="Naziv" htmlFor="kategorija-naziv" error={greskaNaziv}>
          <Input
            id="kategorija-naziv"
            value={naziv}
            onChange={(e) => {
              setNaziv(e.target.value);
              setGreske((g) => ({ ...g, naziv: undefined }));
            }}
            placeholder="npr. Sirovine"
            maxLength={MAX_NAZIV}
            hasError={!!greskaNaziv}
          />
        </FormField>

        <FormField label="Opis (opciono)" htmlFor="kategorija-opis" error={fieldErrors.opis}>
          <Textarea
            id="kategorija-opis"
            value={opis}
            onChange={(e) => setOpis(e.target.value)}
            hasError={!!fieldErrors.opis}
          />
        </FormField>

        {error && <p className="whitespace-pre-line text-sm text-red-600">{error}</p>}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={saving}>
            Odustani
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Čuvanje…' : jeIzmena ? 'Sačuvaj' : 'Kreiraj'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
