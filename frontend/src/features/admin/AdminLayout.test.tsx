import { describe, it, expect } from 'vitest';
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
});
