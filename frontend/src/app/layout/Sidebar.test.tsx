import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { GIT_SHA, VERZIJA } from '../../lib/verzija';
import { Sidebar } from './Sidebar';

const PRAVA: Record<string, string[]> = {
  Admin: ['analitika', 'sabloni.nacrt', 'neradni_dani', 'korisnici', 'podesavanja'],
  Inzenjer: ['pregledi.popunjavanje', 'analitika'],
};

function renderSidebar(roles: string[]) {
  render(<Sidebar pristup={{ roles, prava: roles.flatMap((r) => PRAVA[r] ?? []) }} onOdjava={() => {}} />, { wrapper: MemoryRouter });
}

describe('Sidebar — navigacija po roli', () => {
  it('Admin vidi sve stavke', () => {
    renderSidebar(['Admin']);
    ['Kalendar', 'Pregledi', 'Akcije', 'Neispravnosti', 'Šabloni', 'Uređaji', 'Neradni dani', 'Administracija', 'Audit log', 'Stanje servera'].forEach((l) =>
      expect(screen.getByRole('link', { name: l })).toBeInTheDocument(),
    );
  });

  it('Inženjer vidi Šablone i Uređaje, bez administracije', () => {
    renderSidebar(['Inzenjer']);
    expect(screen.getByRole('link', { name: 'Šabloni' })).toBeInTheDocument();
    ['Administracija', 'Audit log', 'Stanje servera'].forEach((l) =>
      expect(screen.queryByRole('link', { name: l })).not.toBeInTheDocument(),
    );
  });

  it('dijagnostika vodi na sopstvene rute, ne pod /admin', () => {
    renderSidebar(['Admin']);
    expect(screen.getByRole('link', { name: 'Audit log' })).toHaveAttribute('href', '/audit-log');
    expect(screen.getByRole('link', { name: 'Stanje servera' })).toHaveAttribute('href', '/stanje-servera');
  });

  it('prikazuje odjavu i verziju sa git SHA u title-u', () => {
    renderSidebar(['Inzenjer']);
    expect(screen.getByRole('button', { name: /Odjava/ })).toBeInTheDocument();
    const verzija = screen.getByText(`v${VERZIJA}`);
    expect(verzija).toHaveAttribute('title', GIT_SHA);
  });
});
