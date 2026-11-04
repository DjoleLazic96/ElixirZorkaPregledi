import { describe, it, expect, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { api } from '../../api/client';
import { odgovor, renderSaProviderima } from '../../test/utils';
import type { PrimaociDto, ZvonoDto } from './hooks';
import { PrimaociCard } from './PrimaociCard';
import { Zvono } from './Zvono';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }));

const ZVONO: ZvonoDto = {
  neprocitanih: 1, cekaOdluku: 2,
  stavke: [
    { id: 7, vrsta: 'dorada', naslov: 'DP1-2026-0001 vraćen na doradu', tekst: 'Fale slike', link: '/pregled/1/popuni', kreiranoAt: new Date().toISOString(), procitano: false },
    { id: 6, vrsta: 'dodela', naslov: 'Novi pregledi u rasporedu', tekst: 'Dodeljeni su vam…', link: '/kalendar', kreiranoAt: new Date().toISOString(), procitano: true },
  ],
};

describe('Zvono', () => {
  it('broj je nepročitano + čeka moju odluku; klik označava pročitano', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor(ZVONO));
    vi.mocked(api.post).mockResolvedValue(odgovor({ ...ZVONO, neprocitanih: 0 }));
    renderSaProviderima(<Zvono />);
    const dugme = await screen.findByRole('button', { name: 'Obaveštenja (3)' });
    await userEvent.click(dugme);
    const d = screen.getByRole('dialog', { name: 'Obaveštenja' });
    expect(within(d).getByText(/Čeka moju odluku/)).toHaveTextContent('2');
    await userEvent.click(within(d).getByText('DP1-2026-0001 vraćen na doradu'));
    expect(vi.mocked(api.post)).toHaveBeenCalledWith('/obavestenja/procitano', { ids: [7] });
  });
});

describe('Primaoci obaveštenja', () => {
  it('štiklira primaoca i čuva po vrsti', async () => {
    const p: PrimaociDto = {
      vrste: [{ vrsta: 'rok_overe', naziv: 'Podsetnik za overu', opis: 'x', podeseno: false, korisnikIds: ['s'] }],
      korisnici: [{ id: 's', ime: 'Sup' }, { id: 'a', ime: 'Admin' }],
    };
    vi.mocked(api.get).mockResolvedValue(odgovor(p));
    vi.mocked(api.put).mockResolvedValue(odgovor(p));
    renderSaProviderima(<PrimaociCard />);
    expect(await screen.findByText('(podrazumevano)')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Podsetnik za overu: Admin' }));
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj primaoce' }));
    expect(vi.mocked(api.put)).toHaveBeenCalledWith('/podesavanja/primaoci', { rok_overe: ['s', 'a'] });
  });
});
