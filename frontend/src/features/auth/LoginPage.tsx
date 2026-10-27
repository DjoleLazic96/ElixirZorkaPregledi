import { type FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { NAZIV_APLIKACIJE } from '../../components/OAplikaciji';
import { Button } from '../../components/ui/Button';
import { Card, CardBody } from '../../components/ui/Card';
import { FormField } from '../../components/ui/FormField';
import { Input } from '../../components/ui/Input';
import { porukaGreske } from '../../lib/apiError';
import { VERZIJA } from '../../lib/verzija';
import { useAuth } from './AuthProvider';

/**
 * Stranica za prijavu. Uspeh → `/` (AppRoutes vodi na landing). Greška iz
 * `porukaGreske` (401 nosi istu generičku poruku za sve razloge — ne otkrivamo
 * da li korisnik postoji). Kad je sesija istekla, iznad forme stoji objašnjenje.
 */
export function LoginPage() {
  const { login, sesijaIstekla } = useAuth();
  const navigate = useNavigate();

  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [greska, setGreska] = useState<string | null>(null);
  const [saljem, setSaljem] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setGreska(null);
    setSaljem(true);
    try {
      await login(userName, password);
      navigate('/', { replace: true });
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
              <Input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
              />
            </FormField>

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
