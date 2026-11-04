import { Eye, EyeOff } from 'lucide-react';
import { adresaPovratka } from './povratak';
import { Checkbox } from '../../components/ui/Checkbox';
import { type FormEvent, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { NAZIV_APLIKACIJE } from '../../components/OAplikaciji';
import { Button } from '../../components/ui/Button';
import { Card, CardBody } from '../../components/ui/Card';
import { FormField } from '../../components/ui/FormField';
import { Input } from '../../components/ui/Input';
import { porukaGreske } from '../../lib/apiError';
import { VERZIJA } from '../../lib/verzija';
import { useAuth } from './AuthProvider';

/**
 * Stranica za prijavu. Uspeh → adresa sa koje je korisnik došao (npr. QR nalepnica uređaja), inače Početna. Greška iz
 * `porukaGreske` (401 nosi istu generičku poruku za sve razloge — ne otkrivamo
 * da li korisnik postoji). Kad je sesija istekla, iznad forme stoji objašnjenje.
 */
export function LoginPage() {
  const { login, sesijaIstekla } = useAuth();
  const navigate = useNavigate();
  const povratak = adresaPovratka(useLocation().state);

  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [zapamti, setZapamti] = useState(false);
  const [vidiLozinku, setVidiLozinku] = useState(false);
  const [greska, setGreska] = useState<string | null>(null);
  const [saljem, setSaljem] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setGreska(null);
    setSaljem(true);
    try {
      await login(userName, password, zapamti);
      navigate(povratak, { replace: true });
    } catch (err) {
      setGreska(porukaGreske(err, 'Prijava nije uspela.'));
    } finally {
      setSaljem(false);
    }
  }

  return (
    <div className="grid min-h-screen place-items-center p-4">
      <Card className="w-full max-w-sm">
        <CardBody>
          <h1 className="text-xl font-semibold text-gray-900">{NAZIV_APLIKACIJE}</h1>
          <p className="mt-1 text-sm text-gray-500">Prijavite se da nastavite.</p>

          {sesijaIstekla && (
            <p
              role="status"
              className="mt-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800"
            >
              Sesija je istekla. Prijavite se ponovo.
            </p>
          )}

          <form onSubmit={onSubmit} className="mt-6 space-y-4">
            <FormField label="Korisničko ime" htmlFor="username">
              <Input
                id="username"
                value={userName}
                onChange={(e) => setUserName(e.target.value)}
                autoComplete="username"
                required
                autoFocus
              />
            </FormField>

            <FormField label="Lozinka" htmlFor="password">
              <div className="relative">
                <Input
                  id="password"
                  type={vidiLozinku ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="current-password"
                  className="pr-11"
                  required
                />
                {/* „Oko“ (R1.11): lozinka ostaje vidljiva dok se ponovo ne klikne. */}
                <button
                  type="button"
                  onClick={() => setVidiLozinku((v) => !v)}
                  aria-label={vidiLozinku ? 'Sakrij lozinku' : 'Prikaži lozinku'}
                  aria-pressed={vidiLozinku}
                  className="absolute inset-y-0 right-0 flex w-11 items-center justify-center text-gray-500 hover:text-gray-800"
                >
                  {vidiLozinku ? <EyeOff size={16} aria-hidden /> : <Eye size={16} aria-hidden />}
                </button>
              </div>
            </FormField>

            <Checkbox
              label="Zapamti me na ovom uređaju"
              description="Na službenom telefonu obavezno: ostajete prijavljeni i radite bez interneta."
              checked={zapamti}
              onChange={(e) => setZapamti(e.target.checked)}
            />

            {greska !== null && <p className="text-sm text-red-600">{greska}</p>}

            <Button type="submit" className="w-full" disabled={saljem}>
              {saljem ? 'Prijavljivanje…' : 'Prijavi se'}
            </Button>
          </form>

          <p className="mt-6 text-center text-xs text-gray-400">v{VERZIJA}</p>
        </CardBody>
      </Card>
    </div>
  );
}
