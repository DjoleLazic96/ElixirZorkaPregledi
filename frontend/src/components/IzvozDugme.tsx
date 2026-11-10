import { useState } from 'react';
import { Download } from 'lucide-react';
import { useToast } from '../app/providers/ToastProvider';
import { porukaGreske } from '../lib/apiError';
import { preuzmiFajl } from '../lib/download';
import { Button } from './ui/Button';

/** „Preuzmi Excel“ za filtriranu listu (R8.13: izvoz uzima filtrirano). */
export function IzvozDugme({ url, params, ime }: { url: string; params?: Record<string, string | number | boolean | undefined>; ime: string }) {
  const toast = useToast();
  const [radi, setRadi] = useState(false);
  return (
    <Button variant="secondary" disabled={radi} onClick={() => { setRadi(true); void preuzmiFajl(url, params ?? {}, ime).catch((e) => toast.error(porukaGreske(e, 'Izvoz nije uspeo.'))).finally(() => setRadi(false)); }}>
      <Download size={14} aria-hidden /> {radi ? 'Preuzimanje…' : 'Preuzmi Excel'}
    </Button>
  );
}
