import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { api } from '../../api/client';
import { odgovor, renderSaProviderima } from '../../test/utils';
import type { PregledKalendarDto, RasporedDto } from './hooks';
import { bojaPregleda, stavkeDana } from './kalendar';
import { PregledKartica } from './KalendarPage';
import { RasporedPage, SvaOdsustva } from './RasporedPage';
import { MojaOdsustvaPage } from './OdsustvaINeradniPages';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }));

let pristup = { user: { id: 'ana', userName: 'ana', fullName: 'Ana', email: null }, roles: ['Inzenjer'], prava: ['pregledi.popunjavanje'] };
vi.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ me: pristup }) }));

const P: PregledKalendarDto = {
  id: 1, broj: 'DP1-2026-0001', tip: 'DP1', planiraniDatum: '2026-11-10', prvobitniDatum: '2026-11-09', status: 'planiran',
  dodeljenId: 'ana', dodeljen: 'Ana', ucesnici: [], napomena: null,
};

describe('kalendar — boje i dani', () => {
  it('boje po R3.22', () => {
    expect(bojaPregleda({ ...P, status: 'overen' }, '2026-11-10')).toContain('blue');
    expect(bojaPregleda({ ...P, status: 'ceka_overu' }, '2026-11-10')).toContain('green');
    expect(bojaPregleda({ ...P, status: 'u_radu' }, '2026-11-10')).toContain('amber');
    expect(bojaPregleda(P, '2026-11-11')).toContain('red');
    expect(bojaPregleda(P, '2026-11-10')).toContain('bg-white');
  });

  it('prvobitni dan prenetog pregleda pokazuje „prenet na“', () => {
    const r: RasporedDto = { pregledi: [P], neradniDani: [{ datum: '2026-11-11', naziv: 'Praznik' }], odsustva: [] };
    expect(stavkeDana(r, '2026-11-09').preneti).toHaveLength(1);
    expect(stavkeDana(r, '2026-11-10').pregledi).toHaveLength(1);
    expect(stavkeDana(r, '2026-11-11').neradni?.naziv).toBe('Praznik');
  });
});

describe('PregledKartica', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
    vi.mocked(api.get).mockResolvedValue(odgovor([{ id: 'bora', ime: 'Bora' }]));
  });

  it('inženjer za svoj planiran pregled šalje zahtev za prebacivanje', async () => {
    vi.mocked(api.post).mockResolvedValue(odgovor({}));
    renderSaProviderima(<PregledKartica pregled={P} onClose={() => {}} />);
    expect(screen.getByText(/prenet sa 09.11.2026/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Promeni u rasporedu' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Zatraži prebacivanje' }));
    await userEvent.type(screen.getByLabelText('Razlog'), 'Lekar');
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji zahtev' }));
    expect(api.post).toHaveBeenCalledWith('/zahtevi-prebacivanja', { pregledId: 1, razlog: 'Lekar', predlozenaZamenaId: null });
  });

  it('supervizor dodaje drugog inženjera na PZ sa njegovim sekcijama (R3.14)', async () => {
    pristup = { ...pristup, user: { ...pristup.user, id: 'sup' }, roles: ['Supervizor'], prava: ['raspored.uredjivanje'] };
    const sablon = { tip: 'PZ1', broj: 1, status: 'objavljena', vaziOd: null, sekcije: [{ sifra: '10', uredjaji: [] }, { sifra: '20', uredjaji: [] }, { sifra: '30', uredjaji: [] }] };
    vi.mocked(api.get).mockImplementation(async (url: string) => odgovor(url.startsWith('/sabloni/') ? sablon : [{ id: 'bora', ime: 'Bora' }]));
    vi.mocked(api.post).mockResolvedValue(odgovor({}));
    renderSaProviderima(<PregledKartica pregled={{ ...P, tip: 'PZ1', broj: 'PZ1-2026-0001' }} onClose={() => {}} />);

    await userEvent.click(screen.getByRole('button', { name: 'Dodaj inženjera' }));
    await userEvent.click(await screen.findByRole('combobox', { name: 'Drugi inženjer' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Bora' }));
    await userEvent.click(screen.getByRole('button', { name: 'Označi sve sekcije' }));
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Ostavite bar jednu sekciju');
    expect(api.post).not.toHaveBeenCalled();

    await userEvent.click(screen.getByLabelText('Sekcija 10'));
    await userEvent.click(screen.getByRole('button', { name: 'Sačuvaj' }));
    expect(api.post).toHaveBeenCalledWith('/pregledi/1/ucesnici', { korisnikId: 'bora', sekcije: ['20', '30'] });
    pristup = { user: { id: 'ana', userName: 'ana', fullName: 'Ana', email: null }, roles: ['Inzenjer'], prava: ['pregledi.popunjavanje'] };
  });

  it('DP nema „Dodaj inženjera“', () => {
    pristup = { ...pristup, roles: ['Supervizor'], prava: ['raspored.uredjivanje'] };
    renderSaProviderima(<PregledKartica pregled={P} onClose={() => {}} />);
    expect(screen.queryByRole('button', { name: 'Dodaj inženjera' })).not.toBeInTheDocument();
    pristup = { user: { id: 'ana', userName: 'ana', fullName: 'Ana', email: null }, roles: ['Inzenjer'], prava: ['pregledi.popunjavanje'] };
  });

  it('dok zahtev čeka odluku, umesto dugmeta piše „Zahtev poslat — čeka odluku“ (zamerka 9)', () => {
    renderSaProviderima(<PregledKartica pregled={{ ...P, zahtevNaCekanju: 'prebacivanje' }} onClose={() => {}} />);
    expect(screen.queryByRole('button', { name: 'Zatraži prebacivanje' })).not.toBeInTheDocument();
    expect(screen.getByText('Zahtev poslat — čeka odluku')).toBeInTheDocument();
    expect(screen.getByText(/čeka prebacivanje/)).toBeInTheDocument();
  });

  it('tuđi pregled nema „Zatraži prebacivanje“', () => {
    renderSaProviderima(<PregledKartica pregled={{ ...P, dodeljenId: 'bora' }} onClose={() => {}} />);
    expect(screen.queryByRole('button', { name: 'Zatraži prebacivanje' })).not.toBeInTheDocument();
  });
});

describe('RasporedPage', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
    pristup = { ...pristup, roles: ['Supervizor'], prava: ['raspored.uredjivanje', 'odsustva.odobravanje'] };
    vi.mocked(api.get).mockImplementation(async (url: string) => {
      if (url === '/raspored') return odgovor({ pregledi: [], neradniDani: [], odsustva: [] });
      if (url === '/raspored/inzenjeri') return odgovor([{ id: 'ana', ime: 'Ana' }, { id: 'bora', ime: 'Bora' }]);
      return odgovor([]);
    });
  });

  it('predlog se potvrđuje sa izabranim inženjerima', async () => {
    vi.mocked(api.post).mockImplementation(async (url: string) =>
      url === '/raspored/predlog'
        ? odgovor([{ datum: '2026-11-09', tip: 'DP1', inzenjerId: 'ana', inzenjer: 'Ana' }])
        : odgovor([{ ...P, broj: 'DP1-2026-0001' }]),
    );
    renderSaProviderima(<RasporedPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'Predloži raspored' }));
    const dijalog = await screen.findByRole('dialog', { name: 'Predlog rasporeda' });
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Potvrdi raspored' }));

    expect(api.post).toHaveBeenCalledWith('/raspored/potvrdi', { stavke: [{ datum: '2026-11-09', tip: 'DP1', inzenjerId: 'ana' }] });
    expect(await screen.findByText(/Raspored je potvrđen: DP1-2026-0001/)).toBeInTheDocument();
  });
});

describe('Odsustva', () => {
  const O = (id: number, korisnikId: string, ime: string, od: string, doD: string, status: string) =>
    ({ id, korisnikId, ime, od, do: doD, vrsta: 'godisnji', napomena: null, status, razlogOdbijanja: null, kreiranoAt: '2026-11-01T00:00:00Z' });

  beforeEach(() => {
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
  });

  it('Direktor vidi tekuća i buduća odsustva, a „Otkaži“ šalje otkazivanje (prošla tek uz „Prikaži i ranija“)', async () => {
    vi.mocked(api.get).mockImplementation(async (url: string) => odgovor(url === '/odsustva'
      ? [O(1, 'ana', 'Ana', '2099-01-10', '2099-01-12', 'odobreno'), O(2, 'bora', 'Bora', '2020-01-01', '2020-01-02', 'odobreno'), O(3, 'ana', 'Ana', '2099-02-01', '2099-02-01', 'otkazano')]
      : [{ id: 'ana', ime: 'Ana' }]));
    vi.mocked(api.post).mockResolvedValue(odgovor({}));
    renderSaProviderima(<SvaOdsustva />);
    expect(await screen.findByText(/10.01.2099/)).toBeInTheDocument();
    expect(screen.queryByText(/01.01.2020/)).not.toBeInTheDocument();
    expect(screen.queryByText('otkazano')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Otkaži' }));
    await userEvent.click(screen.getByRole('button', { name: 'Otkaži odsustvo' }));
    expect(api.post).toHaveBeenCalledWith('/odsustva/1/otkazi');

    await userEvent.click(screen.getByLabelText('Prikaži i ranija'));
    expect(screen.getByText(/01.01.2020/)).toBeInTheDocument();
    expect(screen.getByText('otkazano')).toBeInTheDocument();
  });

  it('„Moja odsustva“ pokazuje samo svoja; zahtev na čekanju može da se povuče', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor([O(4, 'ana', 'Ana', '2099-03-01', '2099-03-02', 'zahtev'), O(5, 'bora', 'Bora', '2099-04-01', '2099-04-01', 'odobreno')]));
    vi.mocked(api.post).mockResolvedValue(odgovor({}));
    renderSaProviderima(<MojaOdsustvaPage />);
    expect(await screen.findByText(/01.03.2099/)).toBeInTheDocument();
    expect(screen.queryByText(/01.04.2099/)).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Povuci zahtev' }));
    expect(api.post).toHaveBeenCalledWith('/odsustva/4/otkazi');
  });
});
