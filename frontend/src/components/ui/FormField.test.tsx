import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FormField } from './FormField';
import { Input } from './Input';

describe('FormField', () => {
  it('vezuje labelu za kontrolu preko htmlFor', () => {
    render(
      <FormField label="Šifra" htmlFor="sifra">
        <Input id="sifra" />
      </FormField>,
    );
    expect(screen.getByLabelText('Šifra')).toBeInTheDocument();
  });

  it('prikazuje pomoćni tekst dok nema greške, a grešku umesto njega kad postoji', () => {
    const { rerender } = render(
      <FormField label="Naziv" htmlFor="naziv" hint="Najviše 100 znakova.">
        <Input id="naziv" />
      </FormField>,
    );
    expect(screen.getByText('Najviše 100 znakova.')).toBeInTheDocument();

    rerender(
      <FormField label="Naziv" htmlFor="naziv" hint="Najviše 100 znakova." error="Naziv je obavezan.">
        <Input id="naziv" />
      </FormField>,
    );
    expect(screen.getByText('Naziv je obavezan.')).toBeInTheDocument();
    expect(screen.queryByText('Najviše 100 znakova.')).not.toBeInTheDocument();
  });
});
