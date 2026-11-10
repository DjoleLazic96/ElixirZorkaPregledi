import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router-dom';
import { api } from '../../api/client';
import type { SablonVerzijaDto } from '../../api/types';
import { apiGreska, odgovor, renderSaProviderima } from '../../test/utils';
import { NacrtPage, PredlogDijalog, PredloziTab } from './IzmenePages';
import type { NacrtDto, PredlogDto } from './izmene';
import { UputstvoDugme } from './UputstvoDijalog';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }));

let pristup = { user: { id: 'd', userName: 'dir', fullName: 'Direktor', email: null }, roles: ['Direktor'], prava: ['sabloni.nacrt', 'sabloni.objava'] };
vi.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ me: pristup }) }));

const VERZIJA: SablonVerzijaDto = {
  tip: 'DP1', broj: 2, status: 'nacrt', vaziOd: null,
  sekcije: [{ sifra: '10', uredjaji: [{ uredjajId: 1, naziv: 'Transporter', oznaka: '10-C-08', stavke: [
    { stavkaId: 21, kljuc: 'k1', aktivnostId: 5, aktivnost: 'Pregled rolni', odgovorIspravan: 'Okreće', odgovorNeispravan: 'Ne okreće', polja: [] },
  ] }] }],
};
const NACRT: NacrtDto = {
  verzija: VERZIJA, osnovnaVerzija: 1, zakazana: null,
  razlike: [{ vrsta: 'izmenjeno', sekcija: '10', uredjaj: 'Transporter 10-C-08', aktivnost: 'Pregled rolni', bilo: 'Pregled valjaka' }],
  predlozi: [],
};

function nacrtStranica() {
  renderSaProviderima(<Routes><Route path="/sabloni/:tip/nacrt" element={<NacrtPage />} /></Routes>, { ruta: '/sabloni/dp1/nacrt' });
}

describe('Nacrt šablona', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
    vi.mocked(api.delete).mockReset();
    pristup = { ...pristup, prava: ['sabloni.nacrt', 'sabloni.objava'] };
  });

  it('bez nacrta nudi „Otvori nacrt“', async () => {
    vi.mocked(api.get).mockResolvedValue({ ...odgovor(''), status: 204 });
    vi.mocked(api.post).mockResolvedValue(odgovor(NACRT));
    nacrtStranica();
    await userEvent.click(await screen.findByRole('button', { name: 'Otvori nacrt' }));
    expect(vi.mocked(api.post)).toHaveBeenCalledWith('/sabloni/DP1/nacrt', undefined);
    expect(await screen.findByText('Nacrt DP1 · verzija 2')).toBeInTheDocument();
  });

  it('pokazuje razlike, a izbacivanje i pomeranje idu na prave rute', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor(NACRT));
    vi.mocked(api.delete).mockResolvedValue(odgovor(NACRT));
    vi.mocked(api.post).mockResolvedValue(odgovor(NACRT));
    nacrtStranica();
    expect(await screen.findByText('Izmenjeno')).toBeInTheDocument();
    expect(screen.getByText('Pregled valjaka').tagName).toBe('S');
    await userEvent.click(screen.getByRole('button', { name: 'Izbaci — Pregled rolni' }));
    expect(vi.mocked(api.delete)).toHaveBeenCalledWith('/sabloni/DP1/nacrt/stavke/21');
    await userEvent.click(screen.getByRole('button', { name: 'Gore — Pregled rolni' }));
    expect(vi.mocked(api.post)).toHaveBeenCalledWith('/sabloni/DP1/nacrt/stavke/21/pomeri', { gore: true });
  });

  it('„Objavi“ vidi samo ko ima pravo objave (Direktor)', async () => {
    pristup = { ...pristup, prava: ['sabloni.nacrt'] };
    vi.mocked(api.get).mockResolvedValue(odgovor(NACRT));
    nacrtStranica();
    expect(await screen.findByRole('button', { name: 'Dodaj stavku' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Objavi' })).not.toBeInTheDocument();
  });
});

describe('Predlozi izmena', () => {
  it('predlog traži opis i razlog i šalje izabranu stavku', async () => {
    vi.mocked(api.post).mockReset();
    vi.mocked(api.post).mockResolvedValue(odgovor({}));
    renderSaProviderima(<PredlogDijalog v={{ ...VERZIJA, status: 'objavljena' }} onClose={() => {}} />);
    const posalji = screen.getByRole('button', { name: 'Pošalji predlog' });
    expect(posalji).toBeDisabled();
    await userEvent.type(screen.getByLabelText('Šta treba promeniti'), 'Preimenovati u „Pregled valjaka“');
    await userEvent.type(screen.getByLabelText('Razlog'), 'Tako piše na uređaju');
    await userEvent.click(posalji);
    expect(vi.mocked(api.post)).toHaveBeenCalledWith('/predlozi-izmena',
      { tip: 'DP1', vrsta: 'ispravka', stavkaId: null, opis: 'Preimenovati u „Pregled valjaka“', razlog: 'Tako piše na uređaju' });
  });

  it('Direktor odobrava predlog koji čeka odluku', async () => {
    const p: PredlogDto = { id: 4, sablon: 'DP1', vrsta: 'izbacivanje', stavkaId: 21, stavka: 'Transporter 10-C-08 · Pregled rolni', opis: 'Izbaciti', razlog: 'Nema rolne',
      podneo: 'Ana', kreiranoAt: '2026-11-04T10:00:00Z', status: 'zahtev', odlucio: null, odgovor: null, verzija: null };
    vi.mocked(api.get).mockResolvedValue(odgovor([p]));
    vi.mocked(api.post).mockResolvedValue(odgovor({ ...p, status: 'odobren' }));
    renderSaProviderima(<PredloziTab />);
    const red = (await screen.findByText('Izbaciti')).closest('li')!;
    expect(within(red).getByText('čeka odluku')).toBeInTheDocument();
    await userEvent.click(within(red).getByRole('button', { name: 'Odobri' }));
    expect(vi.mocked(api.post)).toHaveBeenCalledWith('/predlozi-izmena/4/odluka', { odobren: true, odgovor: null });
  });
});

describe('Uputstvo', () => {
  it('bez interneta pokazuje tekst iz paketa', async () => {
    vi.mocked(api.get).mockRejectedValue(apiGreska(503));
    renderSaProviderima(<UputstvoDugme aktivnostId={5} aktivnost="Pregled rolni" smeDaMenja={false} offlineTekst="Proveriti svaku rolnu." />);
    await userEvent.click(screen.getByRole('button', { name: 'Uputstvo — Pregled rolni' }));
    expect(await screen.findByText('Proveriti svaku rolnu.')).toBeInTheDocument();
    expect(await screen.findByText('Slike uputstva se vide kad ima interneta.')).toBeInTheDocument();
  });

  it('bez uputstva piše „Uputstvo još nije uneto“, a ko sme nudi unos', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor({ aktivnostId: 5, aktivnost: 'Pregled rolni', tekst: null, slike: [], azurirao: null, azuriranoAt: null }));
    renderSaProviderima(<UputstvoDugme aktivnostId={5} aktivnost="Pregled rolni" smeDaMenja />);
    await userEvent.click(screen.getByRole('button', { name: 'Uputstvo — Pregled rolni' }));
    expect(await screen.findByText('Uputstvo još nije uneto.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Unesi uputstvo' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Dodaj sliku (0/3)' })).toBeInTheDocument();
  });
});
