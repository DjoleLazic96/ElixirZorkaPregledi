import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { promeniLozinku } from '../features/auth/auth-api';
import { apiGreska, renderSaProviderima } from '../test/utils';
import { PromenaLozinke } from './PromenaLozinke';

vi.mock('../features/auth/auth-api', () => ({ promeniLozinku: vi.fn() }));

async function popuni(trenutna: string, nova: string, potvrda: string) {
  await userEvent.type(screen.getByLabelText('Trenutna lozinka'), trenutna);
  await userEvent.type(screen.getByLabelText('Nova lozinka'), nova);
  await userEvent.type(screen.getByLabelText('Potvrda nove lozinke'), potvrda);
  await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj' }));
}

describe('PromenaLozinke', () => {
  beforeEach(() => {
    vi.mocked(promeniLozinku).mockReset();
  });

  it('ne šalje zahtev kad se potvrda ne poklapa ili je nova lozinka prekratka', async () => {
    renderSaProviderima(<PromenaLozinke open onClose={vi.fn()} />);
    await popuni('Stara123', 'kratka', 'drugacija');
    expect(screen.getByText('Nova lozinka mora imati najmanje 8 znakova.')).toBeInTheDocument();
    expect(screen.getByText('Potvrda se ne poklapa sa novom lozinkom.')).toBeInTheDocument();
    expect(promeniLozinku).not.toHaveBeenCalled();
  });

  it('uspeh: šalje currentPassword/newPassword, prikazuje toast i zatvara dijalog', async () => {
    vi.mocked(promeniLozinku).mockResolvedValue();
    const onClose = vi.fn();
    renderSaProviderima(<PromenaLozinke open onClose={onClose} />);
    await popuni('Stara123', 'NovaLozinka1', 'NovaLozinka1');

    expect(promeniLozinku).toHaveBeenCalledWith({ currentPassword: 'Stara123', newPassword: 'NovaLozinka1' });
    expect(await screen.findByText('Lozinka je promenjena.')).toBeInTheDocument();
    expect(onClose).toHaveBeenCalled();
  });

  it('400 sa errors.currentPassword prikazuje grešku uz polje trenutne lozinke', async () => {
    vi.mocked(promeniLozinku).mockRejectedValue(
      apiGreska(400, { errors: { currentPassword: ['Trenutna lozinka nije ispravna.'] } }),
    );
    renderSaProviderima(<PromenaLozinke open onClose={vi.fn()} />);
    await popuni('Pogresna1', 'NovaLozinka1', 'NovaLozinka1');
    expect(await screen.findByText('Trenutna lozinka nije ispravna.')).toBeInTheDocument();
  });

  it('ostale greške idu kao jedna poruka pod formom', async () => {
    vi.mocked(promeniLozinku).mockRejectedValue(apiGreska(422, { greske: ['Nova lozinka ne sme biti ista kao stara.'] }));
    renderSaProviderima(<PromenaLozinke open onClose={vi.fn()} />);
    await popuni('Stara123', 'Stara1234', 'Stara1234');
    expect(await screen.findByText('Nova lozinka ne sme biti ista kao stara.')).toBeInTheDocument();
  });
});
