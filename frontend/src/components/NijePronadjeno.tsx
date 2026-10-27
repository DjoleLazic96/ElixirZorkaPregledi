import { useNavigate } from 'react-router-dom';
import { Button } from './ui/Button';

/**
 * 404 stranica unutar layout-a (blueprint 13 §4: eksplicitna stranica, ne tihi
 * redirect — korisnik treba da vidi da je link pogrešan, ne da „ništa ne radi").
 */
export function NijePronadjeno() {
  const navigate = useNavigate();
  return (
    <div className="flex flex-col items-center py-20 text-center">
      <p className="text-5xl font-semibold text-gray-300">404</p>
      <h1 className="mt-2 text-xl font-semibold text-gray-900">Stranica nije pronađena.</h1>
      <p className="mt-1 text-sm text-gray-500">
        Adresa koju ste otvorili ne postoji ili je premeštena.
      </p>
      <Button variant="secondary" className="mt-6" onClick={() => navigate('/')}>
        Nazad na početnu
      </Button>
    </div>
  );
}
