import { useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import type { PromenaLozinkeInput } from '../api/types';
import { useToast } from '../app/providers/ToastProvider';
import { promeniLozinku } from '../features/auth/auth-api';
import { greskePoPolju, porukaGreske } from '../lib/apiError';
import { Button } from './ui/Button';
import { FormField } from './ui/FormField';
import { Input } from './ui/Input';
import { Modal } from './ui/Modal';

interface PromenaLozinkeProps {
  open: boolean;
  onClose: () => void;
}

interface Greske {
  trenutna?: string;
  nova?: string;
  potvrda?: string;
  opsta?: string;
}

/** Minimalna dužina po politici lozinke (blueprint 07 §1) — server je autoritativan. */
const MIN_DUZINA = 8;

/**
 * Dijalog za promenu sopstvene lozinke (`POST /auth/change-password`).
 * Roditelj ga renderuje uslovno (`{open && …}`), pa se stanje resetuje pri
 * svakom otvaranju bez sync-effect-a.
 */
export function PromenaLozinke({ open, onClose }: PromenaLozinkeProps) {
  const toast = useToast();
  const [trenutna, setTrenutna] = useState('');
  const [nova, setNova] = useState('');
  const [potvrda, setPotvrda] = useState('');
  const [greske, setGreske] = useState<Greske>({});

  const mutacija = useMutation({
    // Eksplicitan omotač: TanStack prosleđuje i drugi (context) argument, koji API poziv ne očekuje.
    mutationFn: (input: PromenaLozinkeInput) => promeniLozinku(input),
    onSuccess: () => {
      toast.success('Lozinka je promenjena.');
      onClose();
    },
    onError: (err) => {
      // 400 nosi `errors.currentPassword|newPassword` → mapiraj na polja; sve
      // ostalo (422, 500, mreža) ide kao opšta poruka pod formom.
      const poPolju = greskePoPolju(err);
      if (poPolju.currentPassword || poPolju.newPassword) {
        setGreske({ trenutna: poPolju.currentPassword, nova: poPolju.newPassword });
      } else {
        setGreske({ opsta: porukaGreske(err, 'Promena lozinke nije uspela.') });
      }
    },
  });

  function submit(e: FormEvent) {
    e.preventDefault();
    const nove: Greske = {};
    if (trenutna === '') nove.trenutna = 'Unesite trenutnu lozinku.';
    if (nova.length < MIN_DUZINA) nove.nova = `Nova lozinka mora imati najmanje ${MIN_DUZINA} znakova.`;
    if (potvrda !== nova) nove.potvrda = 'Potvrda se ne poklapa sa novom lozinkom.';
    setGreske(nove);
    if (Object.keys(nove).length > 0) return;
    mutacija.mutate({ currentPassword: trenutna, newPassword: nova });
  }

  return (
    <Modal open={open} title="Promena lozinke" onClose={onClose}>
      <form onSubmit={submit} className="space-y-4" noValidate>
        <FormField label="Trenutna lozinka" htmlFor="lozinka-trenutna" error={greske.trenutna}>
          <Input
            id="lozinka-trenutna"
            type="password"
            autoComplete="current-password"
            value={trenutna}
            onChange={(e) => setTrenutna(e.target.value)}
            hasError={!!greske.trenutna}
          />
        </FormField>
        <FormField
          label="Nova lozinka"
          htmlFor="lozinka-nova"
          error={greske.nova}
          hint={`Najmanje ${MIN_DUZINA} znakova, bar jedna cifra, veliko i malo slovo.`}
        >
          <Input
            id="lozinka-nova"
            type="password"
            autoComplete="new-password"
            value={nova}
            onChange={(e) => setNova(e.target.value)}
            hasError={!!greske.nova}
          />
        </FormField>
        <FormField label="Potvrda nove lozinke" htmlFor="lozinka-potvrda" error={greske.potvrda}>
          <Input
            id="lozinka-potvrda"
            type="password"
            autoComplete="new-password"
            value={potvrda}
            onChange={(e) => setPotvrda(e.target.value)}
            hasError={!!greske.potvrda}
          />
        </FormField>

        {greske.opsta && (
          <p className="whitespace-pre-line text-sm text-red-600">{greske.opsta}</p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose} disabled={mutacija.isPending}>
            Odustani
          </Button>
          <Button type="submit" disabled={mutacija.isPending}>
            {mutacija.isPending ? 'Čuvanje…' : 'Sačuvaj'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
