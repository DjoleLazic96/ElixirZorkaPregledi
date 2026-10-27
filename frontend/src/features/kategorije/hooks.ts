/**
 * PRIMER feature-a — obrazac za sve ostale; kopiraj ili obriši.
 *
 * Jedan `hooks.ts` po feature-u (blueprint 13 §5): axios poziv inline, bez
 * „service" sloja; mutacija invalidira korenski ključ u `onSuccess`; paginirana
 * lista koristi `keepPreviousData`.
 */
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { KategorijaDto, KategorijaInput, KategorijeFilteri, StranicaDto } from '../../api/types';

const KEY = 'kategorije';

export function useKategorije(filteri: KategorijeFilteri) {
  return useQuery({
    queryKey: [KEY, filteri],
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const params: Record<string, string | number | boolean> = {
        stranica: filteri.stranica,
        velicina: filteri.velicina,
        sort: filteri.sort,
        smer: filteri.smer,
      };
      if (filteri.pretraga) params.pretraga = filteri.pretraga;
      if (filteri.aktivan !== undefined) params.aktivan = filteri.aktivan;
      return (await api.get<StranicaDto<KategorijaDto>>('/kategorije', { params })).data;
    },
  });
}

export function useKreirajKategoriju() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: KategorijaInput) => (await api.post<KategorijaDto>('/kategorije', input)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [KEY] });
    },
  });
}

export function useIzmeniKategoriju() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (args: { id: number; input: KategorijaInput }) =>
      (await api.put<KategorijaDto>(`/kategorije/${args.id}`, args.input)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [KEY] });
    },
  });
}

export function useDeaktivirajKategoriju() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => (await api.post<KategorijaDto>(`/kategorije/${id}/deaktiviraj`)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [KEY] });
    },
  });
}

export function useAktivirajKategoriju() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => (await api.post<KategorijaDto>(`/kategorije/${id}/aktiviraj`)).data,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [KEY] });
    },
  });
}
