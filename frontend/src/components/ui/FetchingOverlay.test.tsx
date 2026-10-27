import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FetchingOverlay } from './FetchingOverlay';

describe('FetchingOverlay', () => {
  it('uvek renderuje decu (sadržaj ostaje na ekranu)', () => {
    render(
      <FetchingOverlay fetching={false}>
        <div>SADRŽAJ</div>
      </FetchingOverlay>,
    );
    expect(screen.getByText('SADRŽAJ')).toBeInTheDocument();
    // Bez fetch-a nema oznake „Osvežavanje…".
    expect(screen.queryByText(/Osvežavanje/)).not.toBeInTheDocument();
  });

  it('kada fetching=true: sadržaj ostaje + prikazuje znak „Osvežavanje…" i zatamnjuje/blokira', () => {
    const { container } = render(
      <FetchingOverlay fetching>
        <div>SADRŽAJ</div>
      </FetchingOverlay>,
    );
    // Prethodni sadržaj se ne demontira.
    expect(screen.getByText('SADRŽAJ')).toBeInTheDocument();
    // Suptilni znak osvežavanja.
    expect(screen.getByText(/Osvežavanje/)).toBeInTheDocument();
    // Sadržaj je zatamnjen i onemogućen za klik dok traje fetch.
    const dim = container.querySelector('.opacity-60');
    expect(dim).not.toBeNull();
    expect(dim).toHaveClass('pointer-events-none');
    expect(dim).toHaveAttribute('aria-busy', 'true');
  });
});
