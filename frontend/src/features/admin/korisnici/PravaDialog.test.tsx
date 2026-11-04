import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { KorisnikDto } from '../../../api/types';
import { PravaDialog } from './PravaDialog';

const SUPERVIZOR: KorisnikDto = {
  id: 's1',
  korisnickoIme: 'mika',
  ime: 'Mika Mikić',
  email: null,
  rola: 'Supervizor',
  prava: ['analitika'],
  aktivan: true,
  poslednjaPrijava: null,
};

function renderDialog(korisnik = SUPERVIZOR) {
  const onSubmit = vi.fn();
  render(<PravaDialog korisnik={korisnik} saving={false} onSubmit={onSubmit} onClose={vi.fn()} />);
  return onSubmit;
}

describe('PravaDialog', () => {
  it('štikliranje šalje ceo skup redom iz matrice', async () => {
    const onSubmit = renderDialog();
    await userEvent.click(screen.getByLabelText('Overa, dorada i poništavanje pregleda'));
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj prava' }));
    expect(onSubmit).toHaveBeenCalledWith(['analitika', 'overa']);
  });

  it('popunjavanje pregleda je zaključano za sve osim Inženjera', () => {
    renderDialog();
    expect(screen.getByLabelText(/^Popunjavanje pregleda/)).toBeDisabled();
  });

  it('„Vrati podrazumevana“ vraća skup role', async () => {
    const onSubmit = renderDialog();
    await userEvent.click(screen.getByRole('button', { name: 'Vrati podrazumevana' }));
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj prava' }));
    expect(onSubmit).toHaveBeenCalledWith([
      'analitika', 'overa', 'raspored.uredjivanje', 'odsustva.odobravanje', 'akcije.zatvaranje',
    ]);
  });

  it('Inženjer može da dobije popunjavanje', () => {
    renderDialog({ ...SUPERVIZOR, rola: 'Inzenjer', prava: [] });
    expect(screen.getByLabelText(/^Popunjavanje pregleda/)).toBeEnabled();
  });
});
