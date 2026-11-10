import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router-dom';
import { api } from '../../api/client';
import { odgovor, renderSaProviderima } from '../../test/utils';
import type { PregledDetaljDto } from './hooks';
import { PregledDetaljPage } from './PregledDetaljPage';
import { PreglediPage } from './PreglediPage';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn() } }));

let pristup = { user: { id: 'sup', userName: 'sup', fullName: 'Sup', email: null }, roles: ['Supervizor'], prava: ['overa'] };
vi.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ me: pristup }) }));

const DETALJ: PregledDetaljDto = {
  id: 5, broj: 'DP1-2026-0001', tip: 'DP1', status: 'ceka_overu', dorada: 0, planiraniDatum: '2026-11-09', prvobitniDatum: '2026-11-09',
  dodeljenId: 'ana', inzenjer: 'Ana', verzijaBroj: 1, zapocetAt: '2026-11-09T07:00:00Z', predatAt: '2026-11-09T08:00:00Z',
  aktivnoSekundi: 2400, delova: 2, overio: null, overenoAt: null, overioDirektor: false, ponistenRazlog: null,
  upozorenja: ['Pregled je rađen u 2 dela.'],
  sekcije: [{ sifra: '10', uredjaji: [{ uredjajId: 1, naziv: 'Transporter 10-C-08', vanFunkcije: false, stavke: [
    { stavkaId: 9, aktivnost: 'Pregled rolni', odgovor: 'Ne okreće', neispravan: true, napomena: '5 rolni', polja: ['Broj rolni koje ne okreću: 5 kom.'], fotografije: [] },
  ] }] }],
  istorija: [{ vrsta: 'predaja', opis: 'Predat', korisnik: 'Ana', at: '2026-11-09T08:00:00Z' }],
  sporniUnosi: [], zahteviDorade: [],
};

function renderDetalj() {
  renderSaProviderima(<Routes><Route path="/pregledi/:id" element={<PregledDetaljPage />} /></Routes>, { ruta: '/pregledi/5' });
}

describe('PregledDetaljPage', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
    pristup = { ...pristup, user: { ...pristup.user, id: 'sup' }, roles: ['Supervizor'], prava: ['overa'] };
    vi.mocked(api.get).mockImplementation(async (url: string) => (url === '/pregledi/5' ? odgovor(DETALJ) : odgovor([{ id: 'ana', ime: 'Ana' }])));
  });

  it('kontrolor vidi vreme, upozorenja i rezime problematičnih, a „Overio“ šalje overu', async () => {
    vi.mocked(api.post).mockResolvedValue(odgovor({ ...DETALJ, status: 'overen', overio: 'Sup' }));
    renderDetalj();
    expect(await screen.findByText('Pregled je rađen u 2 dela.')).toBeInTheDocument();
    expect(screen.getByText(/40 min · 2 dela/)).toBeInTheDocument();
    expect(screen.getByText(/Problematične stavke \(1\)/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Overio' }));
    expect(api.post).toHaveBeenCalledWith('/pregledi/5/overi', {});
  });

  it('„Vrati na doradu“ traži razlog i šalje izabranog inženjera', async () => {
    vi.mocked(api.post).mockResolvedValue(odgovor({ ...DETALJ, status: 'vracen_na_doradu', dorada: 1 }));
    renderDetalj();
    await userEvent.click(await screen.findByRole('button', { name: 'Vrati na doradu' }));
    const d = screen.getByRole('dialog', { name: 'Vrati na doradu' });
    await userEvent.type(within(d).getByLabelText('Razlog'), 'Dopuni napomenu');
    await userEvent.click(within(d).getByRole('button', { name: 'Vrati na doradu' }));
    expect(api.post).toHaveBeenCalledWith('/pregledi/5/vrati-na-doradu', { inzenjerId: 'ana', razlog: 'Dopuni napomenu' });
  });

  it('inženjer bez prava overe ne vidi odluke i upozorenja, ali vidi „Zatraži doradu“ za svoj pregled', async () => {
    pristup = { ...pristup, user: { ...pristup.user, id: 'ana' }, roles: ['Inzenjer'], prava: ['pregledi.popunjavanje'] };
    renderDetalj();
    expect(await screen.findByRole('button', { name: 'Zatraži doradu' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Overio' })).not.toBeInTheDocument();
    expect(screen.queryByText('Pregled je rađen u 2 dela.')).not.toBeInTheDocument();
  });
});

describe('PreglediPage', () => {
  it('arhiva prikazuje broj, status i broj neispravnih', async () => {
    vi.mocked(api.get).mockImplementation(async (url: string) =>
      url === '/pregledi'
        ? odgovor({ stavke: [{ id: 5, broj: 'DP1-2026-0001', tip: 'DP1', planiraniDatum: '2026-11-09', status: 'ceka_overu', dorada: 0, inzenjer: 'Ana', predatAt: null, overio: null, neispravnih: 1 }], ukupno: 1, stranica: 1, velicina: 50 })
        : odgovor([]),
    );
    renderSaProviderima(<PreglediPage />);
    expect((await screen.findAllByText('DP1-2026-0001')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('Čeka overu').length).toBeGreaterThan(0);
  });
});
