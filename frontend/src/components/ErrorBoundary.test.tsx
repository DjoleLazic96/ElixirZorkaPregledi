import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ErrorBoundary } from './ErrorBoundary';

function Eksplodira({ greska }: { greska: Error }): never {
  throw greska;
}

describe('ErrorBoundary', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renderuje decu dok nema greške', () => {
    render(
      <ErrorBoundary>
        <p>Sve radi.</p>
      </ErrorBoundary>,
    );
    expect(screen.getByText('Sve radi.')).toBeInTheDocument();
  });

  it('hvata grešku i prikazuje poruku sa dugmetom „Osveži stranicu"', () => {
    // React loguje uhvaćenu grešku — utišano da test izlaz ostane čist.
    vi.spyOn(console, 'error').mockImplementation(() => {});
    render(
      <ErrorBoundary>
        <Eksplodira greska={new Error('bum')} />
      </ErrorBoundary>,
    );
    expect(screen.getByText('Došlo je do greške u aplikaciji.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Osveži stranicu' })).toBeInTheDocument();
  });

  it('prikazuje traceId kad ga greška nosi (API greška)', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const greska = Object.assign(new Error('500'), {
      response: { data: { traceId: '4bf92f3577b34da6a3ce929d0e0e4736' } },
    });
    render(
      <ErrorBoundary>
        <Eksplodira greska={greska} />
      </ErrorBoundary>,
    );
    expect(screen.getByText('4bf92f3577b34da6a3ce929d0e0e4736')).toBeInTheDocument();
  });
});
