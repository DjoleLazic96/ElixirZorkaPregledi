import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router-dom';
import { api } from '../../api/client';
import { odgovor, renderSaProviderima } from '../../test/utils';
import { DashboardPage } from './DashboardPage';
import { deloviPoklapanja, type DashboardDto, type UredjajEkranDto } from './hooks';
import { Pretraga } from './Pretraga';
import { UredjajEkranPage } from './UredjajEkranPage';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn() } }));
vi.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ me: { user: { id: 'a', userName: 'ana', fullName: 'Ana Anić', email: null }, roles: ['Inzenjer'], prava: [] } }) }));

const EKRAN: UredjajEkranDto = {
  id: 16, naziv: 'Transporter', oznaka: '10-C-08', prikaz: 'Transporter 10-C-08', sekcija: '10', vrsta: 'Transporter', statusRegistra: 'aktivan',
  status: 'kvar', danaOdPregleda: 1, sabloni: ['DP1', 'PZ1'], od: '2026-11-03', do: '2026-11-05',
  poslednji: { pregledId: 5, broj: 'DP1-2026-0003', tip: 'DP1', datum: '2026-11-04', inzenjer: 'Ana', stanje: 'neispravan', neispravnih: 1, status: 'ceka_overu' },
  istorija: [{ dan: '2026-11-03', stanje: null }, { dan: '2026-11-04', stanje: 'neispravan' }, { dan: '2026-11-05', stanje: 'neispravan' }],
  analitika: { danaBezKvaraSada: 0, najduzeBezKvara: 12, danaOdPoslednjegKvara: null, brojKvarova: 1, prosekTrajanja: 2, trajanja: [2] },
  najcescaStavka: 'Pregled rolni',
  pregledi: [
    { pregledId: 5, broj: 'DP1-2026-0003', tip: 'DP1', datum: '2026-11-04', inzenjer: 'Ana', stanje: 'neispravan', neispravnih: 1, status: 'ceka_overu' },
    { pregledId: 2, broj: 'PZ1-2026-0001', tip: 'PZ1', datum: '2026-10-20', inzenjer: 'Petar', stanje: 'ispravan', neispravnih: 0, status: 'overen' },
  ],
  dosije: [{ pregledId: 5, broj: 'DP1-2026-0003', datum: '2026-11-04', aktivnost: 'Pregled rolni', neispravan: true, napomena: '7 rolni ne okreće', fotografije: [] }],
  akcije: [], kvarovi: [], qrToken: '6f1c0f3e-0000-4000-8000-000000000001',
};

describe('Ekran uređaja', () => {
  beforeEach(() => vi.mocked(api.get).mockReset());

  function prikazi() {
    vi.mocked(api.get).mockResolvedValue(odgovor(EKRAN));
    renderSaProviderima(<Routes><Route path="/uredjaji/:id" element={<UredjajEkranPage />} /></Routes>, { ruta: '/uredjaji/16' });
  }

  it('pokazuje status, šablone, analitiku i vremensku liniju sa legendom', async () => {
    prikazi();
    expect(await screen.findByRole('heading', { name: 'Transporter 10-C-08' })).toBeInTheDocument();
    expect(screen.getByText('Otvoren kvar')).toBeInTheDocument();
    expect(screen.getByText(/šabloni: DP1, PZ1/)).toBeInTheDocument();
    expect(screen.getByText('najčešće: Pregled rolni')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Istorija stanja po danima' }).children).toHaveLength(3);
    expect(screen.getByTitle('04.11.2026: Neispravan')).toBeInTheDocument();
    expect(vi.mocked(api.get)).toHaveBeenCalledWith('/uredjaji/16/ekran', { params: { dana: 90 } });
  });

  it('Svi pregledi se filtriraju po tipu, link otvara pregled sa uređajem na vrhu, Dosije ima napomene', async () => {
    prikazi();
    const tabela = await screen.findByRole('table');
    expect(within(tabela).getByRole('link', { name: 'PZ1-2026-0001' })).toHaveAttribute('href', '/pregledi/2?uredjaj=16');
    await userEvent.click(screen.getByRole('tab', { name: 'Dosije' }));
    expect(screen.getByText('7 rolni ne okreće')).toBeInTheDocument();
  });
});

describe('Početna', () => {
  it('inženjer vidi „Moje aktivnosti“ sa „Zatraži prebacivanje“, bez „Čeka moju odluku“', async () => {
    const d: DashboardDto = {
      moje: {
        danas: [{ id: 7, broj: 'DP1-2026-0010', tip: 'DP1', datum: '2026-11-04', status: 'planiran', inzenjer: 'Ana', overio: null, moj: true }],
        zapoceti: [], narednih7: [{ id: 8, broj: 'DP1-2026-0011', tip: 'DP1', datum: '2026-11-05', status: 'planiran', inzenjer: 'Ana', overio: null, moj: true }],
        zahtevi: [], akcije: [],
      },
      cekaOdluku: null,
      kpi: { procenatNaDan: 92, procenatURoku: 80, cekaOveru: 2, otvorenihAkcija: 1, otvorenihKvarova: 1, vanFunkcije: 0 },
      danas: [], sutra: [], cekajuOveru: [], akcijePoHitnosti: { hitno: 1 }, akcije: [], kvarovi: [], vanFunkcije: [],
    };
    vi.mocked(api.get).mockResolvedValue(odgovor(d));
    renderSaProviderima(<DashboardPage />);
    expect(await screen.findByText('Moje aktivnosti')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'DP1-2026-0010' })).toHaveAttribute('href', '/pregled/7/popuni');
    expect(screen.getByRole('link', { name: 'Zatraži prebacivanje' })).toBeInTheDocument();
    expect(screen.queryByText(/Čeka moju odluku/)).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(/^(Dobro jutro|Dobar dan|Dobro veče), Ana$/);
    expect(screen.getByText('hitnih akcija').previousElementSibling).toHaveTextContent('1');
  });
});

describe('Pretraga u zaglavlju', () => {
  it('obeležava pronađeni deo bez obzira na velika slova (S12 u S12, IS12, AS12)', () => {
    expect(deloviPoklapanja('IS12', 's12')).toEqual([{ t: 'I', pogodak: false }, { t: 'S12', pogodak: true }]);
    expect(deloviPoklapanja('S12 i AS12', 'S12').filter((d) => d.pogodak)).toHaveLength(2);
    expect(deloviPoklapanja('Elevator', '')).toEqual([{ t: 'Elevator', pogodak: false }]);
  });

  it('prikazuje rezultate sa žutim obeležavanjem', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor([{ vrsta: 'uredjaj', id: 2, tekst: 'Elevator 10-C-13', opis: 'Sekcija 10' }]));
    renderSaProviderima(<Pretraga />);
    await userEvent.click(screen.getByRole('button', { name: 'Pretraga' }));
    await userEvent.type(screen.getByRole('combobox', { name: 'Pretraga' }), 'c-13');
    const opcija = await screen.findByRole('option');
    expect(within(opcija).getByText('C-13').tagName).toBe('MARK');
    expect(vi.mocked(api.get)).toHaveBeenCalledWith('/pretraga', { params: { q: 'c-13' } });
  });
});
