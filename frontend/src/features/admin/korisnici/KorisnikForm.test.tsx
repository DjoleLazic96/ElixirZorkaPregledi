import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { KorisnikDto } from '../../../api/types';
import { KorisnikForm } from './KorisnikForm';

function renderNova(onSubmit = vi.fn()) {
  render(<KorisnikForm open korisnik={null} saving={false} error={null} onSubmit={onSubmit} onClose={vi.fn()} />);
  return onSubmit;
}

describe('KorisnikForm', () => {
  it('blokira kreiranje sa prekratkim korisničkim imenom i lozinkom i prikazuje greške uz polja', async () => {
    const onSubmit = renderNova();
    await userEvent.type(screen.getByLabelText('Korisničko ime'), 'ab');
    await userEvent.type(screen.getByLabelText('Ime i prezime'), 'Test Korisnik');
    await userEvent.type(screen.getByLabelText('Lozinka'), 'kratka');
    await userEvent.click(screen.getByRole('button', { name: 'Kreiraj' }));

    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText('Korisničko ime mora imati najmanje 3 znaka.')).toBeInTheDocument();
    expect(screen.getByText('Lozinka mora imati najmanje 8 znakova.')).toBeInTheDocument();
  });

  it('odbija neispravan e-mail', async () => {
    const onSubmit = renderNova();
    await userEvent.type(screen.getByLabelText('Korisničko ime'), 'petar');
    await userEvent.type(screen.getByLabelText('Ime i prezime'), 'Petar');
    await userEvent.type(screen.getByLabelText('E-mail (opciono)'), 'nije-mejl');
    await userEvent.type(screen.getByLabelText('Lozinka'), 'Lozinka12');
    await userEvent.click(screen.getByRole('button', { name: 'Kreiraj' }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText('E-mail adresa nije ispravna.')).toBeInTheDocument();
  });

  it('poziva onSubmit sa validnim podacima; podrazumevana rola je Korisnik, prazan e-mail → null', async () => {
    const onSubmit = renderNova();
    await userEvent.type(screen.getByLabelText('Korisničko ime'), 'petar');
    await userEvent.type(screen.getByLabelText('Ime i prezime'), 'Petar Petrović');
    await userEvent.type(screen.getByLabelText('Lozinka'), 'Lozinka12');
    await userEvent.click(screen.getByRole('button', { name: 'Kreiraj' }));

    expect(onSubmit).toHaveBeenCalledWith({
      korisnickoIme: 'petar',
      ime: 'Petar Petrović',
      email: null,
      lozinka: 'Lozinka12',
      rola: 'Korisnik',
    });
  });

  it('radio kartica „Administrator" menja rolu', async () => {
    const onSubmit = renderNova();
    await userEvent.type(screen.getByLabelText('Korisničko ime'), 'ana');
    await userEvent.type(screen.getByLabelText('Ime i prezime'), 'Ana');
    await userEvent.type(screen.getByLabelText('Lozinka'), 'Lozinka12');
    await userEvent.click(screen.getByRole('radio', { name: /Administrator/ }));
    await userEvent.click(screen.getByRole('button', { name: 'Kreiraj' }));
    expect(onSubmit.mock.calls[0][0].rola).toBe('Admin');
  });

  it('u izmeni je korisničko ime zaključano, lozinke nema, a polja su popunjena', () => {
    const k: KorisnikDto = {
      id: 'u1',
      korisnickoIme: 'petar',
      ime: 'Petar Petrović',
      email: 'petar@elixirgroup.rs',
      rola: 'Admin',
      aktivan: true,
      poslednjaPrijava: null,
    };
    render(<KorisnikForm open korisnik={k} saving={false} error={null} onSubmit={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByRole('dialog', { name: 'Izmena korisnika' })).toBeInTheDocument();
    expect(screen.getByLabelText('Korisničko ime')).toBeDisabled();
    expect(screen.getByLabelText('Ime i prezime')).toHaveValue('Petar Petrović');
    expect(screen.getByLabelText('E-mail (opciono)')).toHaveValue('petar@elixirgroup.rs');
    expect(screen.queryByLabelText('Lozinka')).not.toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /Administrator/ })).toBeChecked();
    expect(screen.getByRole('button', { name: 'Sačuvaj' })).toBeInTheDocument();
  });

  it('prikazuje grešku sa servera pod formom', () => {
    render(
      <KorisnikForm open korisnik={null} saving={false} error="Korisničko ime je zauzeto." onSubmit={vi.fn()} onClose={vi.fn()} />,
    );
    expect(screen.getByText('Korisničko ime je zauzeto.')).toBeInTheDocument();
  });
});
