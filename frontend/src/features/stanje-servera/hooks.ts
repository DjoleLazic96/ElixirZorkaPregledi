import { useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import type { ServerStanje } from '../../api/types';

/**
 * `GET /stanje-servera` — jedan poziv, ceo snapshot. Auto-osvežavanje na 15 s
 * (blueprint 10 §2) da CPU/RAM/uptime ostanu živi; „Osveži" dugme zove `refetch()`.
 */
export function useServerStanje() {
  return useQuery({
    queryKey: ['stanje-servera'],
    queryFn: async () => (await api.get<ServerStanje>('/stanje-servera')).data,
    refetchInterval: 15_000,
  });
}
