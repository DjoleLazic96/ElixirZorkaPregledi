import type { ReactElement, ReactNode } from 'react';
import type { AxiosResponse, InternalAxiosRequestConfig } from 'axios';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, type RenderOptions } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ConfirmProvider } from '../app/providers/ConfirmProvider';
import { ToastProvider } from '../app/providers/ToastProvider';

interface Opcije extends Omit<RenderOptions, 'wrapper'> {
  /** Početna ruta MemoryRouter-a (uključujući query string). */
  ruta?: string;
}

/**
 * Render sa svim providerima koje stranice očekuju (blueprint 16 §3.3):
 * QueryClient bez retry-ja, MemoryRouter, Toast i Confirm. Vraća i `qc` za
 * proveru keša kad treba.
 */
export function renderSaProviderima(ui: ReactElement, { ruta = '/', ...opcije }: Opcije = {}) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: 0 } },
  });
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[ruta]}>
        <ToastProvider>
          <ConfirmProvider>{children}</ConfirmProvider>
        </ToastProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
  return { ...render(ui, { wrapper: Wrapper, ...opcije }), qc };
}

/** Minimalan axios odgovor za `vi.mocked(api.get).mockResolvedValue(odgovor(...))`. */
export function odgovor<T>(data: T): AxiosResponse<T> {
  return {
    data,
    status: 200,
    statusText: 'OK',
    headers: {},
    config: {} as InternalAxiosRequestConfig,
  };
}

/** Axios-olika greška sa RFC 7807 telom, za `mockRejectedValue`. */
export function apiGreska(status: number, data: Record<string, unknown> = {}): Error {
  return Object.assign(new Error(`HTTP ${status}`), {
    response: { status, data: { status, ...data } },
  });
}
