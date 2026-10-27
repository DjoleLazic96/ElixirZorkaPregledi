import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { KategorijaDto } from '../../api/types';
import { KategorijaForm } from './KategorijaForm';

const KATEGORIJA: KategorijaDto = {
  id: 1,
  sifra: 'KAT-01',
  naziv: 'Sirovine',
  opis: 'Ulazne sirovine',
  aktivan: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: null,
};

describe('KategorijaForm', () => {
  it('šifra i naziv su obavezni — greške uz polja, bez poziva onSubmit', async () => {
    const onSubmit = vi.fn();
    render(<KategorijaForm open kategorija={null} saving={false} error={null} onSubmit={onSubmit} onClose={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: 'Kreiraj' }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText('Šifra je obavezna.')).toBeInTheDocument();
    expect(screen.getByText('Naziv je obavezan.')).toBeInTheDocument();
  });

  it('validan unos poziva onSubmit sa trim-ovanim poljima i opis null kad je prazan', async () => {
    const onSubmit = vi.fn();
    render(<KategorijaForm open kategorija={null} saving={false} error={null} onSubmit={onSubmit} onClose={vi.fn()} />);
    await userEvent.type(screen.getByLabelText('Šifra'), '  KAT-02 ');
    await userEvent.type(screen.getByLabelText('Naziv'), 'Ambalaža');
    await userEvent.click(screen.getByRole('button', { name: 'Kreiraj' }));
    expect(onSubmit).toHaveBeenCalledWith({ sifra: 'KAT-02', naziv: 'Ambalaža', opis: null });
  });

  it('izmena popunjava polja i nudi „Sačuvaj"', () => {
    render(<KategorijaForm open kategorija={KATEGORIJA} saving={false} error={null} onSubmit={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByRole('dialog', { name: 'Izmena kategorije' })).toBeInTheDocument();
    expect(screen.getByLabelText('Šifra')).toHaveValue('KAT-01');
    expect(screen.getByLabelText('Naziv')).toHaveValue('Sirovine');
    expect(screen.getByLabelText('Opis (opciono)')).toHaveValue('Ulazne sirovine');
    expect(screen.getByRole('button', { name: 'Sačuvaj' })).toBeInTheDocument();
  });

  it('prikazuje greške po polju sa servera (400) i opštu grešku (422)', () => {
    render(
      <KategorijaForm
        open
        kategorija={null}
        saving={false}
        error="Kategorija sa ovom šifrom već postoji."
        fieldErrors={{ naziv: 'Naziv je predugačak.' }}
        onSubmit={vi.fn()}
        onClose={vi.fn()}
      />,
    );
    expect(screen.getByText('Naziv je predugačak.')).toBeInTheDocument();
    expect(screen.getByText('Kategorija sa ovom šifrom već postoji.')).toBeInTheDocument();
  });

  it('dok se čuva, dugme pokazuje „Čuvanje…" i onemogućeno je', () => {
    render(<KategorijaForm open kategorija={null} saving error={null} onSubmit={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Čuvanje…' })).toBeDisabled();
  });
});
