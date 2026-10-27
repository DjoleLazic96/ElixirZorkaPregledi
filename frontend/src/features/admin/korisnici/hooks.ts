import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../api/client';
import type {
  KorisnikDto,
  KorisnikInput,
  KorisnikIzmenaInput,
  ResetLozinkaInput,
} from '../../../api/types';

/**
 * Hookovi za upravljanje korisnicima (`/korisnici`, Admin). Jedan korenski ključ;
 * svaka mutacija ga invalidira. Guardovi (sopstvena rola, poslednji admin…) stižu
 * kao 422 `greske[]` i prikazuju se kroz `porukaGreske`.
 */
const KEY = 'korisnici';

/** `aktivan` undefined = svi korisnici; `true` = samo aktivni. */
export function useKorisnici(aktivan?: boolean) {
  return useQuery({
    queryKey: [KEY, { aktivan }],
    queryFn: async () =>
      (await api.get<KorisnikDto[]>('/korisnici', { params: aktivan === undefined ? {} : { aktivan } }))
        .data,
  });
}

export function useKreirajKorisnika() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: KorisnikInput) => (await api.post<KorisnikDto>('/korisnici', input)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [KEY] });
    },
  });
}

export function useIzmeniKorisnika() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (args: { id: string; input: KorisnikIzmenaInput }) =>
      (await api.put<KorisnikDto>(`/korisnici/${args.id}`, args.input)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [KEY] });
    },
  });
}

export function useResetLozinke() {
  return useMutation({
    mutationFn: async (args: { id: string; input: ResetLozinkaInput }) => {
      await api.post(`/korisnici/${args.id}/reset-lozinka`, args.input);
    },
  });
}

export function useDeaktivirajKorisnika() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await api.post<KorisnikDto>(`/korisnici/${id}/deaktiviraj`)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [KEY] });
    },
  });
}

export function useAktivirajKorisnika() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => (await api.post<KorisnikDto>(`/korisnici/${id}/aktiviraj`)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [KEY] });
    },
  });
}
