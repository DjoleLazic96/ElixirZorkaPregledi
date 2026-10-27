import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';

/**
 * TanStack Query provider — globalni cache za server state.
 *
 * Zasto TanStack Query:
 *   - automatska deduplikacija paralelnih zahteva
 *   - automatski refetch nakon mutacije (invalidateQueries)
 *   - laksa logika za loading/error stanja u komponentama
 *
 * Default-i:
 *   - staleTime 30s — podaci se smatraju "svezi" 30s pre nego sto query
 *     pokrene refetch u pozadini (mreza laksa nego svaki put)
 *   - retry 1 — ako mreza zazujti, jedan ponovni pokusaj je dovoljan
 */
export function QueryProvider({ children }: { children: ReactNode }) {
  // useState(() => ...) garantuje da se klijent kreira jednom po mount-u
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: false,
            retry: 1,
          },
          mutations: { retry: 0 },
        },
      })
  );

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
