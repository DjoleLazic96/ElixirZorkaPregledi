import { describe, it, expect, vi } from 'vitest';

let prava: string[] = ['korisnici', 'podesavanja'];
vi.mock('../auth/AuthProvider', () => ({
  useAuth: () => ({ me: { user: { id: 'a1', userName: 'admin', fullName: 'Admin', email: null }, roles: ['Admin'], prava } }),
}));
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AdminLayout } from './AdminLayout';

function renderLayout(ruta: string) {
  render(
    <MemoryRouter initialEntries={[ruta]}>
      <Routes>
        <Route path="/admin" element={<AdminLayout />}>
          <Route path="korisnici" element={<div>KORISNICI</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe('AdminLayout', () => {
  it('prikazuje tab „Korisnici" kao aktivan i renderuje podstranu', () => {
    renderLayout('/admin/korisnici');
    expect(screen.getByRole('tablist', { name: 'Administracija' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Korisnici' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('KORISNICI')).toBeInTheDocument();
  });

  it('dijagnostika nije među tabovima — ona je u levom meniju', () => {
    renderLayout('/admin/korisnici');
    expect(screen.queryByRole('tab', { name: 'Audit log' })).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Stanje servera' })).not.toBeInTheDocument();
  });

  it('tab se vidi samo uz pravo', () => {
    prava = ['podesavanja'];
    renderLayout('/admin/korisnici');
    expect(screen.queryByRole('tab', { name: 'Korisnici' })).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Podešavanja' })).toBeInTheDocument();
    prava = ['korisnici', 'podesavanja'];
  });
});
