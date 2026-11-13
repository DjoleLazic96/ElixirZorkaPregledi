import { describe, it, expect, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router-dom';
import { api } from '../../api/client';
import { odgovor, renderSaProviderima } from '../../test/utils';
import { primeniLokalno } from '../../offline/lokalno';
import type { PopunjavanjeDto } from '../popunjavanje/hooks';
import type { AkcijaDto } from './hooks';
import { AkcijePage, AkcijaPage } from './AkcijePages';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

const pristup = { user: { id: 'sup', userName: 'sup', fullName: 'Supervizor Sima', email: null }, roles: ['Supervizor'], prava: ['akcije.zatvaranje'] };
vi.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ me: pristup }) }));

const A: AkcijaDto = {
  id: 1, broj: 'A-2026-0001', pregledId: 5, pregled: 'DP1-2026-0001', uredjajId: 2, uredjaj: 'Transporter 10-C-08', sekcija: '10',
  aktivnost: 'Pregled rolni', vrsta: 'zameniti', hitnost: 'hitno', opis: 'Rolne', rok: '2026-11-03', rokIstekao: true, status: 'otvorena',
  zaduzen: 'Mašinska', prijavio: 'Ana', kreiranoAt: '2026-11-02T08:00:00Z', razlog: null, stavkaSadaIspravna: true, kvarBroj: 'K-2026-0001', kvarOtvoren: false,
};

describe('Potrebne akcije', () => {
  it('lista pokazuje broj, hitnost, istekao rok, zaduženog, kvar i „stavka sada ispravna“', async () => {
    vi.mocked(api.get).mockImplementation(async (url: string) => (url === '/akcije' ? odgovor([A]) : odgovor([])));
    renderSaProviderima(<AkcijePage />);
    expect(await screen.findByText('A-2026-0001')).toBeInTheDocument();
    expect(screen.getByText('Hitno')).toBeInTheDocument();
    expect(screen.getByText('stavka sada ispravna')).toBeInTheDocument();
    expect(screen.getByText(/zadužen: Mašinska/)).toBeInTheDocument();
    expect(screen.getByText('03.11.2026')).toHaveClass('text-red-700');
  });

  it('odgovor na pop-up lokalno skida akciju sa uređaja (radi i bez interneta)', () => {
    const p = {
      id: 1, broj: 'x', tip: 'DP1', status: 'u_radu', planiraniDatum: '2026-11-09', zapocetAt: null, verzijaId: 1,
      sekcije: [{ sifra: '10', moja: true, radi: 'Ana', uredjaji: [{ uredjajId: 2, naziv: 'T', oznaka: '', vanFunkcije: false, prosliPutVanFunkcije: false, stavke: [],
        otvoreneAkcije: [{ id: 1, broj: 'A-2026-0001', aktivnost: 'Pregled rolni', stavkaId: null, vrsta: 'zameniti', hitnost: 'hitno', opis: null }] }] }],
    } as PopunjavanjeDto;
    const posle = primeniLokalno(p, { vrsta: 'akcija_odgovor', podaci: { akcijaId: 1, uradjeno: true } }, 'x');
    expect(posle.sekcije[0].uredjaji[0].otvoreneAkcije).toHaveLength(0);
  });

  it('nadređeni odobrava zahtev za zatvaranje, a sam zatvara uz šta, kada i ko (zamerka 49)', async () => {
    const saZahtevom: AkcijaDto = {
      ...A, id: 7, zahtevZatvaranja: { id: 4, podneoId: 'ana', podneo: 'Ana', uradjeno: 'Zamenjene rolne', datum: '2026-11-12', izvrsilac: 'Ana', imaSliku: false, kreiranoAt: '2026-11-12T10:00:00Z' },
    };
    vi.mocked(api.get).mockImplementation(async (url: string) =>
      url === '/akcije/7' ? odgovor({ akcija: saZahtevom, istorija: [] }) : odgovor([{ id: 'ana', ime: 'Ana' }]));
    vi.mocked(api.post).mockResolvedValue(odgovor({ akcija: { ...saZahtevom, zahtevZatvaranja: null, status: 'zatvorena' }, istorija: [] }));
    renderSaProviderima(<Routes><Route path="/akcije/:id" element={<AkcijaPage />} /></Routes>, { ruta: '/akcije/7' });

    expect(await screen.findByText(/Zahtev za zatvaranje \(Ana\)/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Odobri i zatvori' }));
    expect(api.post).toHaveBeenCalledWith('/akcije/zahtevi-zatvaranja/4/odluka', { odobreno: true });

    await userEvent.click(screen.getByRole('button', { name: 'Zatvori' }));
    const forma = await screen.findByRole('dialog', { name: /Zatvori akciju/ });
    await userEvent.click(within(forma).getByRole('button', { name: 'Zatvori akciju' }));
    expect(await within(forma).findByText('Upišite šta je urađeno.')).toBeInTheDocument();
    await userEvent.type(within(forma).getByLabelText('Šta je urađeno'), 'Zamenjene rolne');
    await userEvent.click(within(forma).getByRole('button', { name: 'Zatvori akciju' }));
    expect(api.post).toHaveBeenCalledWith('/akcije/7/zatvori', expect.objectContaining({ uradjeno: 'Zamenjene rolne', izvrsilac: 'Supervizor Sima' }));
  });
});
