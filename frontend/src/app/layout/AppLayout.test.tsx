import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router-dom';
import { api } from '../../api/client';
import { VERZIJA } from '../../lib/verzija';
import { odgovor, renderSaProviderima } from '../../test/utils';
import { AppLayout } from './AppLayout';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn() } }));

const logoutMock = vi.fn().mockResolvedValue(undefined);
vi.mock('../../features/auth/AuthProvider', () => ({
  useAuth: () => ({
    me: { user: { id: 'a1', userName: 'admin', fullName: 'Ana Anić', email: null }, roles: ['Admin'] },
    initializing: false,
    sesijaIstekla: false,
    login: vi.fn(),
    logout: logoutMock,
  }),
}));

function renderLayout() {
  renderSaProviderima(
    <Routes>
      <Route element={<AppLayout />}>
        <Route path="/" element={<div>SADRŽAJ</div>} />
      </Route>
    </Routes>,
  );
}

describe('AppLayout — header i korisnički meni', () => {
  beforeEach(() => {
    logoutMock.mockClear();
    vi.mocked(api.get).mockReset();
  });

  it('prikazuje ime korisnika i sadržaj rute', () => {
    renderLayout();
    expect(screen.getByRole('button', { name: 'Ana Anić' })).toBeInTheDocument();
    expect(screen.getByText('SADRŽAJ')).toBeInTheDocument();
  });

  it('meni nudi „O aplikaciji", „Promena lozinke" i „Odjavi se"; odjava poziva logout', async () => {
    renderLayout();
    await userEvent.click(screen.getByRole('button', { name: 'Ana Anić' }));
    const meni = screen.getByRole('menu');
    expect(meni).toBeInTheDocument();
    ['O aplikaciji', 'Promena lozinke', 'Odjavi se'].forEach((l) =>
      expect(screen.getByRole('menuitem', { name: l })).toBeInTheDocument(),
    );
    await userEvent.click(screen.getByRole('menuitem', { name: 'Odjavi se' }));
    expect(logoutMock).toHaveBeenCalledOnce();
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('„O aplikaciji" otvara dijalog sa verzijom', async () => {
    vi.mocked(api.get).mockResolvedValue(
      odgovor({ verzija: VERZIJA, gitSha: 'dev', buildUtc: null, okruzenje: 'Development' }),
    );
    renderLayout();
    await userEvent.click(screen.getByRole('button', { name: 'Ana Anić' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'O aplikaciji' }));
    expect(screen.getByRole('dialog', { name: 'O aplikaciji' })).toBeInTheDocument();
    expect(await screen.findByText('Development')).toBeInTheDocument();
  });

  it('„Promena lozinke" otvara formu', async () => {
    renderLayout();
    await userEvent.click(screen.getByRole('button', { name: 'Ana Anić' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Promena lozinke' }));
    expect(screen.getByRole('dialog', { name: 'Promena lozinke' })).toBeInTheDocument();
    expect(screen.getByLabelText('Trenutna lozinka')).toHaveFocus();
  });
});
