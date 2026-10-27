import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { GIT_SHA, VERZIJA } from '../../lib/verzija';
import { Sidebar } from './Sidebar';

function renderSidebar(roles: string[]) {
  render(<Sidebar roles={roles} />, { wrapper: MemoryRouter });
}

describe('Sidebar — navigacija po roli', () => {
  it('Admin vidi sve četiri stavke', () => {
    renderSidebar(['Admin']);
    ['Kategorije', 'Administracija', 'Audit log', 'Stanje servera'].forEach((l) =>
      expect(screen.getByRole('link', { name: l })).toBeInTheDocument(),
    );
  });

  it('Korisnik vidi samo Kategorije', () => {
    renderSidebar(['Korisnik']);
    expect(screen.getByRole('link', { name: 'Kategorije' })).toBeInTheDocument();
    ['Administracija', 'Audit log', 'Stanje servera'].forEach((l) =>
      expect(screen.queryByRole('link', { name: l })).not.toBeInTheDocument(),
    );
  });

  it('dijagnostika vodi na sopstvene rute, ne pod /admin', () => {
    renderSidebar(['Admin']);
    expect(screen.getByRole('link', { name: 'Audit log' })).toHaveAttribute('href', '/audit-log');
    expect(screen.getByRole('link', { name: 'Stanje servera' })).toHaveAttribute('href', '/stanje-servera');
  });

  it('prikazuje naziv aplikacije i verziju sa git SHA u title-u', () => {
    renderSidebar(['Korisnik']);
    expect(screen.getByText('TPM pregledi')).toBeInTheDocument();
    const verzija = screen.getByText(`v${VERZIJA}`);
    expect(verzija).toHaveAttribute('title', GIT_SHA);
  });
});
