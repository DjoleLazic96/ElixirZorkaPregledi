import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';

/** Zvono, Web Push i primaoci obaveštenja (`/obavestenja`, `/push`, `/podesavanja/primaoci`). */
export interface ObavestenjeDto {
  id: number;
  vrsta: string;
  naslov: string;
  tekst: string;
  link: string | null;
  kreiranoAt: string;
  procitano: boolean;
}

export interface ZvonoDto { neprocitanih: number; cekaOdluku: number; stavke: ObavestenjeDto[] }

export interface VrstaPrimalacaDto { vrsta: string; naziv: string; opis: string; podeseno: boolean; korisnikIds: string[] }
export interface PrimaociDto { vrste: VrstaPrimalacaDto[]; korisnici: { id: string; ime: string }[] }

const ZVONO = ['zvono'];

/** Zvono se osvežava na minut (push stiže odmah, a zvono je i za one bez push-a). */
export function useZvono() {
  return useQuery({ queryKey: ZVONO, queryFn: async () => (await api.get<ZvonoDto>('/obavestenja')).data, refetchInterval: 60_000 });
}

export function useProcitano() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (ids: number[] | null) => (await api.post<ZvonoDto>('/obavestenja/procitano', { ids })).data,
    onSuccess: (d) => qc.setQueryData(ZVONO, d),
  });
}

export function usePrimaoci() {
  return useQuery({ queryKey: ['primaoci'], queryFn: async () => (await api.get<PrimaociDto>('/podesavanja/primaoci')).data });
}

export function useSacuvajPrimaoce() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (v: Record<string, string[]>) => (await api.put<PrimaociDto>('/podesavanja/primaoci', v)).data,
    onSuccess: (d) => qc.setQueryData(['primaoci'], d),
  });
}

// ─── Web Push ──────────────────────────────────────────────────────────

/** Da li pregledač uopšte podržava push (Chrome na Androidu i računaru; iPhone samo kao instalirana aplikacija). */
export const pushPodrzan = () => typeof window !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;

function kljucUBajtove(b64: string): Uint8Array<ArrayBuffer> {
  const s = atob((b64 + '='.repeat((4 - (b64.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/'));
  const niz = new Uint8Array(new ArrayBuffer(s.length));
  for (let i = 0; i < s.length; i++) niz[i] = s.charCodeAt(i);
  return niz;
}

/** Trenutna pretplata ovog uređaja (null = nije uključeno). */
export async function trenutnaPretplata(): Promise<PushSubscription | null> {
  if (!pushPodrzan()) return null;
  const reg = await navigator.serviceWorker.getRegistration();
  return (await reg?.pushManager.getSubscription()) ?? null;
}

/** Traži dozvolu, pretplaćuje uređaj i javlja serveru. Baca grešku sa porukom za korisnika. */
export async function ukljuciPush(): Promise<void> {
  if (!pushPodrzan()) throw new Error('Ovaj pregledač ne podržava obaveštenja. Na telefonu koristite Chrome i dodajte aplikaciju na početni ekran.');
  const kljuc = (await api.get<{ kljuc: string | null }>('/push/kljuc')).data.kljuc;
  if (!kljuc) throw new Error('Obaveštenja na telefonu nisu podešena na serveru.');
  // Bez service worker-a (npr. lokalni razvoj) `ready` bi čekao zauvek.
  const reg = await navigator.serviceWorker.getRegistration();
  if (!reg) throw new Error('Aplikacija još nije spremna za obaveštenja. Osvežite stranicu i pokušajte ponovo.');
  if ((await Notification.requestPermission()) !== 'granted') throw new Error('Obaveštenja nisu dozvoljena. Dozvolite ih u podešavanjima pregledača za ovu adresu.');
  const pretplata = (await reg.pushManager.getSubscription()) ?? (await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: kljucUBajtove(kljuc) }));
  const j = pretplata.toJSON();
  await api.post('/push/pretplata', { endpoint: j.endpoint, p256dh: j.keys?.p256dh, auth: j.keys?.auth });
}

/** Odjava uređaja (i pri odjavi korisnika — sledeći korisnik na istom telefonu ne dobija tuđa obaveštenja). */
export async function iskljuciPush(): Promise<void> {
  const p = await trenutnaPretplata();
  if (!p) return;
  await api.delete('/push/pretplata', { data: { endpoint: p.endpoint } }).catch(() => {});
  await p.unsubscribe();
}
