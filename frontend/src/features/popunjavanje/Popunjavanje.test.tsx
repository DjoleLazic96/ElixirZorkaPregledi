import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { renderSaProviderima } from '../../test/utils';
import { smanjeneDimenzije } from '../../lib/slika';
import { DonjiMeni } from '../../app/layout/Sidebar';
import { StavkaRed } from './PopunjavanjePage';
import type { StavkaPopunjavanjaDto } from './hooks';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn() } }));

const STAVKA: StavkaPopunjavanjaDto = {
  stavkaId: 7, aktivnost: 'Pregled rolni', odgovorIspravan: 'Okreće', odgovorNeispravan: 'Ne okreće',
  polja: [{ id: 1, naziv: 'Broj rolni koje ne okreću', tip: 'broj', jedinica: 'kom.', oznake: [], prikaz: 'uz_neispravan', obavezno: false }],
  odgovor: null, fotografije: [],
};

describe('StavkaRed', () => {
  it('klik na odgovor šalje operaciju sa stavkom i vrednošću', async () => {
    const posalji = vi.fn().mockResolvedValue(true);
    renderSaProviderima(<ul><StavkaRed pregledId={1} stavka={STAVKA} moja posalji={posalji} /></ul>);
    expect(screen.queryByLabelText(/Broj rolni/)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Ne okreće' }));
    expect(posalji).toHaveBeenCalledWith({ vrsta: 'odgovor', podaci: { stavkaId: 7, vrednost: 1, napomena: null, polja: {} } });
  });

  it('uz neispravan odgovor prikazuje obaveznu napomenu i dodatno polje', () => {
    renderSaProviderima(
      <ul><StavkaRed pregledId={1} stavka={{ ...STAVKA, odgovor: { vrednost: 1, napomena: null, polja: {} } }} moja posalji={vi.fn()} /></ul>,
    );
    expect(screen.getByPlaceholderText('Napomena je obavezna uz neispravan odgovor.')).toBeInTheDocument();
    expect(screen.getByLabelText(/Broj rolni koje ne okreću \(kom\.\)/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ne okreće' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('tuđa sekcija: odgovori su onemogućeni i nema dugmadi za sliku', () => {
    renderSaProviderima(<ul><StavkaRed pregledId={1} stavka={STAVKA} moja={false} posalji={vi.fn()} /></ul>);
    expect(screen.getByRole('button', { name: 'Okreće' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Fotografiši/ })).not.toBeInTheDocument();
  });
});

describe('mobilni prikaz i slike', () => {
  it('donji meni ima najviše 4 stavke i „Više“ sa ostalima', async () => {
    render(
      <MemoryRouter>
        <DonjiMeni pristup={{ roles: ['Admin'], prava: ['korisnici', 'podesavanja', 'neradni_dani', 'sabloni.nacrt'] }} />
      </MemoryRouter>,
    );
    const meni = screen.getByRole('navigation', { name: 'Donji meni' });
    expect(meni.querySelectorAll('a')).toHaveLength(4);
    await userEvent.click(screen.getByRole('button', { name: /Više/ }));
    expect(screen.getByRole('link', { name: 'Administracija' })).toBeInTheDocument();
  });

  it('slika se smanjuje tako da duža strana bude najviše 2048 px', () => {
    expect(smanjeneDimenzije(4000, 3000)).toEqual({ sirina: 2048, visina: 1536 });
    expect(smanjeneDimenzije(1000, 3000)).toEqual({ sirina: 683, visina: 2048 });
    expect(smanjeneDimenzije(800, 600)).toEqual({ sirina: 800, visina: 600 });
  });
});
