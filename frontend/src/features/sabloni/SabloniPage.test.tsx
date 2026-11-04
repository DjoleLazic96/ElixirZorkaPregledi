import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { api } from '../../api/client';
import type { SablonPregledDto, SablonVerzijaDto, UredjajDto } from '../../api/types';
import { odgovor, renderSaProviderima } from '../../test/utils';
import { SabloniPage } from './SabloniPage';
import { UredjajiPage } from './UredjajiPage';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

let pristup = { roles: ['Admin'], prava: ['sabloni.nacrt'] };
vi.mock('../auth/AuthProvider', () => ({
  useAuth: () => ({ me: { user: { id: 'u', userName: 'u', fullName: 'U', email: null }, ...pristup } }),
}));

const SABLONI: SablonPregledDto[] = ['DP1', 'DP2', 'PZ1', 'PZ2', 'PZ3', 'PZ4'].map((tip, i) => ({
  tip,
  vazecaVerzija: i < 2 ? 1 : null,
  vaziOd: i < 2 ? '2026-11-04' : null,
  brojStavki: i < 2 ? 155 : 0,
  brojUredjaja: i < 2 ? 67 : 0,
}));

const DP1: SablonVerzijaDto = {
  tip: 'DP1',
  broj: 1,
  status: 'objavljena',
  vaziOd: '2026-11-04',
  sekcije: [
    {
      sifra: '10',
      uredjaji: [
        {
          uredjajId: 7,
          naziv: 'Merna vaga Sautelma',
          oznaka: '10-C-07C',
          stavke: [
            {
              stavkaId: 1, kljuc: 'k', aktivnostId: 3, aktivnost: 'Pregled rolni',
              odgovorIspravan: 'Okreće', odgovorNeispravan: 'Ne okreće', polja: ['Broj rolni koje ne okreću'],
            },
          ],
        },
      ],
    },
  ],
};

const UREDJAJ: UredjajDto = {
  id: 7, naziv: 'Merna vaga Sautelma', oznaka: '10-C-07C', sekcijaId: 1, sekcija: '10',
  vrsta: 'Merna', status: 'aktivan', sabloni: ['DP1', 'DP2'],
};

function mockGet() {
  vi.mocked(api.get).mockImplementation(async (url: string) => {
    if (url === '/sabloni') return odgovor(SABLONI);
    if (url === '/sabloni/DP1') return odgovor(DP1);
    if (url === '/uredjaji') return odgovor([UREDJAJ]);
    if (url === '/sekcije') return odgovor([{ id: 1, sifra: '10', naziv: 'Sekcija 10' }]);
    throw new Error(`neočekivan GET ${url}`);
  });
}

describe('SabloniPage', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
    pristup = { roles: ['Admin'], prava: ['sabloni.nacrt'] };
    mockGet();
  });

  it('prikazuje prvi uvezeni šablon kao na obrascu, sa odgovorima i dodatnim poljem', async () => {
    renderSaProviderima(<SabloniPage />);
    expect(await screen.findByText('Sekcija 10')).toBeInTheDocument();
    expect(screen.getByText('Merna vaga Sautelma')).toBeInTheDocument();
    expect(screen.getByText('Okreće / Ne okreće')).toBeInTheDocument();
    expect(screen.getByText('Broj rolni koje ne okreću')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /PZ1/ })).toBeDisabled();
  });

  it('dugme za uvoz vidi samo Admin', async () => {
    pristup = { roles: ['Direktor'], prava: ['sabloni.nacrt'] };
    renderSaProviderima(<SabloniPage />);
    await screen.findByText('Sekcija 10');
    expect(screen.queryByRole('button', { name: 'Uvezi šablone' })).not.toBeInTheDocument();
  });

  it('Admin vidi dugme za uvoz', async () => {
    renderSaProviderima(<SabloniPage />);
    expect(await screen.findByRole('button', { name: 'Uvezi šablone' })).toBeInTheDocument();
  });
});

describe('UredjajiPage', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
    pristup = { roles: ['Direktor'], prava: ['sabloni.nacrt'] };
    mockGet();
  });

  it('kartica pokazuje sekciju i šablone, a novi uređaj šalje POST', async () => {
    vi.mocked(api.post).mockResolvedValue(odgovor({ ...UREDJAJ, id: 8 }));
    renderSaProviderima(<UredjajiPage />);
    expect(await screen.findByText('10-C-07C')).toBeInTheDocument();
    expect(screen.getByText('DP2')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Novi uređaj' }));
    const dijalog = screen.getByRole('dialog', { name: 'Novi uređaj' });
    await userEvent.type(within(dijalog).getByLabelText('Naziv'), 'Pumpa');
    await userEvent.type(within(dijalog).getByLabelText(/^Oznaka/), '21-P-09');
    await userEvent.click(within(dijalog).getByLabelText('Sekcija'));
    await userEvent.click(await screen.findByRole('option', { name: 'Sekcija 10' }));
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Sačuvaj' }));

    expect(api.post).toHaveBeenCalledWith('/uredjaji', { naziv: 'Pumpa', oznaka: '21-P-09', sekcijaId: 1, vrsta: 'Pumpa' });
  });

  it('bez prava „sabloni.nacrt“ nema dugmadi za izmene', async () => {
    pristup = { roles: ['Inzenjer'], prava: ['pregledi.popunjavanje'] };
    renderSaProviderima(<UredjajiPage />);
    await screen.findByText('10-C-07C');
    expect(screen.queryByRole('button', { name: 'Novi uređaj' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Izmeni' })).not.toBeInTheDocument();
  });
});
