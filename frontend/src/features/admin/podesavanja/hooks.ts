import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../api/client';
import type { PodesavanjaDto } from '../../../api/types';

/** Podešavanja aplikacije (`/podesavanja`, pravo „podesavanja“). */
const KEY = 'podesavanja';

export function usePodesavanja() {
  return useQuery({
    queryKey: [KEY],
    queryFn: async () => (await api.get<PodesavanjaDto>('/podesavanja')).data,
  });
}

export function useIzmeniPodesavanja() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (input: PodesavanjaDto) => (await api.put<PodesavanjaDto>('/podesavanja', input)).data,
    onSuccess: (data) => {
      qc.setQueryData([KEY], data);
    },
  });
}
