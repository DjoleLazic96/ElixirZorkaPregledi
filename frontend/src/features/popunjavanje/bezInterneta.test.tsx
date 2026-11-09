import { describe, it, expect, vi, afterEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider, onlineManager } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { dodajOperaciju, dodajSliku } from '../../offline/sinhronizacija';
import { useOperacija, useOtpremiSliku } from './hooks';

vi.mock('../../offline/sinhronizacija', () => ({
  db: {}, obezbediPregled: vi.fn(), useLokalno: vi.fn(), dodajOperaciju: vi.fn(async () => {}), dodajSliku: vi.fn(async () => {}),
}));

function omotac({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>;
}

describe('Popunjavanje bez interneta', () => {
  afterEach(() => onlineManager.setOnline(true));

  it('odgovor i slika se upisuju lokalno odmah, ne čekaju povratak veze', async () => {
    onlineManager.setOnline(false);
    const op = renderHook(() => useOperacija(2), { wrapper: omotac });
    const sl = renderHook(() => useOtpremiSliku(2), { wrapper: omotac });
    op.result.current.mutate({ vrsta: 'predaja', podaci: {} });
    sl.result.current.mutate({ stavkaId: 11, slika: new Blob(['x']), izvor: 'kamera' });
    await waitFor(() => expect(op.result.current.isSuccess).toBe(true));
    await waitFor(() => expect(sl.result.current.isSuccess).toBe(true));
    expect(dodajOperaciju).toHaveBeenCalledWith(2, { vrsta: 'predaja', podaci: {} });
    expect(dodajSliku).toHaveBeenCalled();
  });
});
