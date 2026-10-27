import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import type { CurrentUserResponse } from '../api/types';
import { renderSaProviderima } from '../test/utils';

// useAuth se kontroliše po testu; ostatak AuthProvider modula nije potreban.
let authValue: { me: CurrentUserResponse | null | undefined; initializing: boolean; sesijaIstekla: boolean };
vi.mock('../features/auth/AuthProvider', () => ({
  useAuth: () => ({ ...authValue, login: vi.fn(), logout: vi.fn() }),
}));

// Stranice gađaju API — menjamo ih sentinelima da test ostane fokusiran na guardove.
vi.mock('../features/kategorije/KategorijePage', () => ({ KategorijePage: () => <div>KATEGORIJE-EKRAN</div> }));
vi.mock('../features/admin/korisnici/KorisniciPage', () => ({ KorisniciPage: () => <div>KORISNICI-EKRAN</div> }));
vi.mock('../features/audit-log/AuditLogPage', () => ({ AuditLogPage: () => <div>AUDIT-EKRAN</div> }));
vi.mock('../features/stanje-servera/StanjeServeraPage', () => ({
  StanjeServeraPage: () => <div>SERVER-EKRAN</div>,
}));

import { AppRoutes } from './AppRoutes';

const KORISNIK: CurrentUserResponse = {
  user: { id: 'u1', userName: 'pera', fullName: 'Pera Perić', email: null },
  roles: ['Korisnik'],
};
const ADMIN: CurrentUserResponse = {
  user: { id: 'a1', userName: 'admin', fullName: 'Administrator', email: null },
  roles: ['Admin'],
};

function renderAt(ruta: string, me: CurrentUserResponse | null | undefined, initializing = false) {
  authValue = { me, initializing, sesijaIstekla: false };
  renderSaProviderima(<AppRoutes />, { ruta });
}

describe('AppRoutes — guardovi', () => {
  it('dok se proverava sesija prikazuje „Učitavanje…" preko celog ekrana', () => {
    renderAt('/kategorije', null, true);
    expect(screen.getByRole('status')).toHaveTextContent('Učitavanje…');
  });

  it('neulogovan korisnik na bilo kojoj ruti vidi stranicu za prijavu', () => {
    renderAt('/kategorije', undefined);
    expect(screen.getByRole('heading', { name: 'TPM pregledi' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Prijavi se' })).toBeInTheDocument();
  });

  it('ulogovan korisnik na / i na /login završava na /kategorije', () => {
    renderAt('/', KORISNIK);
    expect(screen.getByText('KATEGORIJE-EKRAN')).toBeInTheDocument();
  });

  it('ulogovan korisnik na /login biva vraćen na landing', () => {
    renderAt('/login', ADMIN);
    expect(screen.getByText('KATEGORIJE-EKRAN')).toBeInTheDocument();
  });

  it('Korisnik na /admin biva vraćen na /kategorije (ne vidi korisnike)', () => {
    renderAt('/admin', KORISNIK);
    expect(screen.getByText('KATEGORIJE-EKRAN')).toBeInTheDocument();
    expect(screen.queryByText('KORISNICI-EKRAN')).not.toBeInTheDocument();
  });

  it('Korisnik na /audit-log i /stanje-servera biva vraćen na /kategorije', () => {
    renderAt('/audit-log', KORISNIK);
    expect(screen.getByText('KATEGORIJE-EKRAN')).toBeInTheDocument();
    expect(screen.queryByText('AUDIT-EKRAN')).not.toBeInTheDocument();
  });

  it('Admin na /admin dolazi na podstranu Korisnici (index redirect)', () => {
    renderAt('/admin', ADMIN);
    expect(screen.getByText('KORISNICI-EKRAN')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Korisnici' })).toHaveAttribute('aria-selected', 'true');
  });

  it('Admin vidi audit log i stanje servera', () => {
    renderAt('/stanje-servera', ADMIN);
    expect(screen.getByText('SERVER-EKRAN')).toBeInTheDocument();
  });

  it('nepoznata adresa daje 404 stranicu unutar layout-a, ne tihi redirect', () => {
    renderAt('/ne-postoji', KORISNIK);
    expect(screen.getByRole('heading', { name: 'Stranica nije pronađena.' })).toBeInTheDocument();
    // Layout (sidebar) je i dalje tu.
    expect(screen.getByRole('link', { name: /Kategorije/ })).toBeInTheDocument();
  });
});
