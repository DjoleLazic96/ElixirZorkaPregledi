import { useState, type FormEvent } from 'react';
import type { KorisnikDto } from '../../../api/types';
import { Button } from '../../../components/ui/Button';
import { FormField } from '../../../components/ui/FormField';
import { Input } from '../../../components/ui/Input';
import { Modal } from '../../../components/ui/Modal';
import { cn } from '../../../lib/cn';
import { ROLA_LABELE, type Rola } from '../../../lib/permissions';

/** Opisi rola za radio kartice — jedino mesto gde se prava objašnjavaju korisniku. */
const ROLE: ReadonlyArray<{ value: Rola; opis: string }> = [
  { value: 'Admin', opis: 'Pun pristup: svi podaci, korisnici, audit log i stanje servera.' },
  { value: 'Korisnik', opis: 'Rad sa podacima aplikacije, bez administracije.' },
];

export interface KorisnikFormResult {
  korisnickoIme: string;
  ime: string;
  email: string | null;
  lozinka: string;
  rola: Rola;
}

interface KorisnikFormProps {
  open: boolean;
  /** null → kreiranje; inače izmena (korisničko ime i lozinka se ne menjaju ovde). */
  korisnik: KorisnikDto | null;
  saving: boolean;
  /** Greška sa servera (422/500) koju roditelj prosleđuje. */
  error: string | null;
  onSubmit: (r: KorisnikFormResult) => void;
  onClose: () => void;
}

interface Greske {
  korisnickoIme?: string;
  ime?: string;
  email?: string;
  lozinka?: string;
}

const MIN_LOZINKA = 8;

/**
 * Prezentaciona forma (blueprint 13 §6): stanje polja iz props-a pri mount-u;
 * roditelj je remontira sa `key={korisnik?.id ?? 'novi'}` umesto sync-effect-a.
 * Minimalne klijentske provere; server je autoritativan.
 */
export function KorisnikForm({ open, korisnik, saving, error, onSubmit, onClose }: KorisnikFormProps) {
  const jeIzmena = korisnik !== null;
  const [korisnickoIme, setKorisnickoIme] = useState(korisnik?.korisnickoIme ?? '');
  const [ime, setIme] = useState(korisnik?.ime ?? '');
  const [email, setEmail] = useState(korisnik?.email ?? '');
  const [lozinka, setLozinka] = useState('');
  const [rola, setRola] = useState<Rola>(korisnik?.rola ?? 'Korisnik');
  const [greske, setGreske] = useState<Greske>({});

  function submit(e: FormEvent) {
    e.preventDefault();
    const ci = korisnickoIme.trim();
    const im = ime.trim();
    const em = email.trim();
    const nove: Greske = {};
    if (!jeIzmena && ci.length < 3) nove.korisnickoIme = 'Korisničko ime mora imati najmanje 3 znaka.';
    if (im === '') nove.ime = 'Ime je obavezno.';
    if (em !== '' && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(em)) nove.email = 'E-mail adresa nije ispravna.';
    if (!jeIzmena && lozinka.length < MIN_LOZINKA) {
      nove.lozinka = `Lozinka mora imati najmanje ${MIN_LOZINKA} znakova.`;
    }
    setGreske(nove);
    if (Object.keys(nove).length > 0) return;
    onSubmit({ korisnickoIme: ci, ime: im, email: em === '' ? null : em, lozinka, rola });
  }

  return (
    <Modal
      open={open}
      title={jeIzmena ? 'Izmena korisnika' : 'Novi korisnik'}
      onClose={onClose}
      widthClass="max-w-lg"
    >
      <form onSubmit={submit} className="space-y-4" noValidate>
        <FormField
          label="Korisničko ime"
          htmlFor="korisnik-korisnicko-ime"
          error={greske.korisnickoIme}
          hint={jeIzmena ? 'Korisničko ime se ne može menjati.' : undefined}
        >
          <Input
            id="korisnik-korisnicko-ime"
            value={korisnickoIme}
            onChange={(e) => setKorisnickoIme(e.target.value)}
            placeholder="npr. p.petrovic"
            autoComplete="off"
            disabled={jeIzmena}
            hasError={!!greske.korisnickoIme}
          />
        </FormField>

        <FormField label="Ime i prezime" htmlFor="korisnik-ime" error={greske.ime}>
          <Input
            id="korisnik-ime"
            value={ime}
            onChange={(e) => setIme(e.target.value)}
            placeholder="npr. Petar Petrović"
            hasError={!!greske.ime}
          />
        </FormField>

        <FormField label="E-mail (opciono)" htmlFor="korisnik-email" error={greske.email}>
          <Input
            id="korisnik-email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="npr. petar.petrovic@elixirgroup.rs"
            autoComplete="off"
            hasError={!!greske.email}
          />
        </FormField>

        {!jeIzmena && (
          <FormField
            label="Lozinka"
            htmlFor="korisnik-lozinka"
            error={greske.lozinka}
            hint={`Najmanje ${MIN_LOZINKA} znakova, bar jedna cifra, veliko i malo slovo.`}
          >
            <Input
              id="korisnik-lozinka"
              type="password"
              value={lozinka}
              onChange={(e) => setLozinka(e.target.value)}
              autoComplete="new-password"
              hasError={!!greske.lozinka}
            />
          </FormField>
        )}

        <fieldset>
          <legend className="mb-2 block text-xs font-medium text-gray-600">Rola</legend>
          <div className="space-y-1">
            {ROLE.map((opt) => {
              const izabrana = rola === opt.value;
              return (
                <label
                  key={opt.value}
                  className={cn(
                    'flex cursor-pointer items-start gap-3 rounded-md border px-3 py-2 text-sm transition-colors',
                    izabrana ? 'border-primary-500 bg-primary-50' : 'border-gray-200 hover:bg-gray-50',
                  )}
                >
                  <input
                    type="radio"
                    name="rola"
                    value={opt.value}
                    checked={izabrana}
                    onChange={() => setRola(opt.value)}
                    className="mt-0.5 accent-primary-500"
                  />
                  <span className="flex-1">
                    <span className="block font-medium text-gray-900">{ROLA_LABELE[opt.value]}</span>
                    <span className="mt-0.5 block text-xs text-gray-500">{opt.opis}</span>
                  </span>
                </label>
              );
            })}
          </div>
        </fieldset>

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
