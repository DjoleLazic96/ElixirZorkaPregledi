import { useToast } from '../../app/providers/ToastProvider';
import { Button } from '../../components/ui/Button';
import { porukaGreske } from '../../lib/apiError';
import { useAuth } from '../auth/AuthProvider';
import { usePredlozi, useSlazemSe } from './izmene';

/**
 * Predlozi za isti šablon koji čekaju odluku, prikazani pre slanja novog (zamerka 37): ako je kolega već predložio isto,
 * korisnik klikne „Slažem se sa predlogom“ umesto da pošalje duplikat. Bez interneta se ne prikazuje (predlog i dalje može da ode).
 */
export function OtvoreniPredlozi({ tip }: { tip: string }) {
  const { me } = useAuth();
  const predlozi = usePredlozi('zahtev', { tip, enabled: typeof navigator === 'undefined' || navigator.onLine });
  const slazemSe = useSlazemSe();
  const toast = useToast();
  const lista = predlozi.data ?? [];
  if (lista.length === 0) return null;
  return (
    <div className="rounded-md border border-gray-200 bg-gray-50 p-2 text-sm">
      <p className="mb-1 font-medium text-gray-800">Već predloženo za {tip} (čeka odluku) — proverite da li je isto:</p>
      <ul className="max-h-48 space-y-2 overflow-y-auto">
        {lista.map((p) => {
          const moj = p.podneoId === me?.user.id;
          const slaze = (p.saglasni ?? []).some((s) => s.id === me?.user.id);
          return (
            <li key={p.id} className="flex flex-wrap items-center justify-between gap-2">
              <span className="min-w-0 flex-1 break-words">{p.opis} <span className="text-gray-500">· {p.podneo}{p.saglasni?.length ? ` + ${p.saglasni.length}` : ''}</span></span>
              {moj ? <span className="text-xs text-gray-500">vaš predlog</span> : slaze ? <span className="text-xs text-primary-700">slažete se</span> : (
                <Button size="sm" variant="secondary" disabled={slazemSe.isPending}
                  onClick={() => void slazemSe.mutateAsync(p.id).then(() => toast.success('Dodati ste uz predlog.')).catch((e) => toast.error(porukaGreske(e, 'Nije sačuvano.')))}>
                  Slažem se sa predlogom
                </Button>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
