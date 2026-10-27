import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { VERZIJA } from '../../lib/verzija';
import { apiGreska } from '../../test/utils';

const loginMock = vi.fn();
const navigateMock = vi.fn();
let sesijaIstekla = false;

vi.mock('./AuthProvider', () => ({
  useAuth: () => ({ login: loginMock, logout: vi.fn(), me: undefined, initializing: false, sesijaIstekla }),
}));
vi.mock('react-router-dom', async (orig) => ({
  ...(await orig<typeof import('react-router-dom')>()),
  useNavigate: () => navigateMock,
}));

import { LoginPage } from './LoginPage';

function renderLogin() {
  render(<LoginPage />, { wrapper: MemoryRouter });
}

describe('LoginPage', () => {
  beforeEach(() => {
    loginMock.mockReset();
    navigateMock.mockReset();
    sesijaIstekla = false;
  });

  it('submit poziva login sa unetim podacima i navigira na /', async () => {
    loginMock.mockResolvedValue(undefined);
    renderLogin();
    await userEvent.type(screen.getByLabelText('Korisničko ime'), 'admin');
    await userEvent.type(screen.getByLabelText('Lozinka'), 'Admin123!');
    await userEvent.click(screen.getByRole('button', { name: 'Prijavi se' }));

    expect(loginMock).toHaveBeenCalledWith('admin', 'Admin123!');
    expect(navigateMock).toHaveBeenCalledWith('/', { replace: true });
  });

  it('prikazuje poruku sa servera kad prijava ne uspe (401)', async () => {
    loginMock.mockRejectedValue(apiGreska(401, { detail: 'Neispravno korisničko ime ili lozinka.' }));
    renderLogin();
    await userEvent.type(screen.getByLabelText('Korisničko ime'), 'admin');
    await userEvent.type(screen.getByLabelText('Lozinka'), 'pogresna');
    await userEvent.click(screen.getByRole('button', { name: 'Prijavi se' }));

    expect(await screen.findByText('Neispravno korisničko ime ili lozinka.')).toBeInTheDocument();
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('bez odgovora servera prikazuje generičku poruku', async () => {
    loginMock.mockRejectedValue(new Error('Network Error'));
    renderLogin();
    await userEvent.type(screen.getByLabelText('Korisničko ime'), 'admin');
    await userEvent.type(screen.getByLabelText('Lozinka'), 'x');
    await userEvent.click(screen.getByRole('button', { name: 'Prijavi se' }));
    expect(await screen.findByText('Prijava nije uspela.')).toBeInTheDocument();
  });

  it('kad je sesija istekla, iznad forme stoji objašnjenje', () => {
    sesijaIstekla = true;
    renderLogin();
    expect(screen.getByRole('status')).toHaveTextContent('Sesija je istekla. Prijavite se ponovo.');
  });

  it('bez isteka sesije nema poruke', () => {
    renderLogin();
    expect(screen.queryByText(/Sesija je istekla/)).not.toBeInTheDocument();
  });

  it('prikazuje verziju frontenda u podnožju', () => {
    renderLogin();
    expect(screen.getByText(`v${VERZIJA}`)).toBeInTheDocument();
  });
});
