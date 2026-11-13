import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { api } from '../../api/client';
import { odgovor, renderSaProviderima } from '../../test/utils';
import type { PregledKalendarDto } from './hooks';
import { oznakaPomeranja, preracunajTipove } from './kalendar';
import { PregledKartica } from './KalendarPage';
import { RasporedPage } from './RasporedPage';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }));

const pristup = { user: { id: 'sup', userName: 'sup', fullName: 'Supervizor', email: null }, roles: ['Supervizor'], prava: ['raspored.uredjivanje'] };
vi.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ me: pristup }) }));

const DP: PregledKalendarDto = {
  id: 7, broj: 'DP2-2026-0004', tip: 'DP2', planiraniDatum: '2026-11-17', prvobitniDatum: '2026-11-17', status: 'planiran',
  dodeljenId: 'ana', dodeljen: 'Ana', ucesnici: [], napomena: null,
};

beforeEach(() => {
  vi.mocked(api.get).mockReset();
  vi.mocked(api.post).mockReset();
  vi.mocked(api.put).mockReset();
  vi.mocked(api.get).mockImplementation(async (url: string) => {
    if (url === '/raspored') return odgovor({ pregledi: [], neradniDani: [], odsustva: [] });
    if (url === '/raspored/inzenjeri') return odgovor([{ id: 'ana', ime: 'Ana' }, { id: 'bora', ime: 'Bora' }]);
    return odgovor([]);
  });
});

describe('raspored — tipovi i oznake (zamerke 29, 30)', () => {
  it('odštiklirani dan se preskače, a tip ide od poslednjeg DP-a pre dana (postojećeg ili štikliranog)', () => {
    const stavke = [
      { datum: '2026-11-17', tip: 'DP2', datumPre: '2026-11-16', tipPre: 'DP1' },
      { datum: '2026-11-18', tip: 'DP1', datumPre: '2026-11-16', tipPre: 'DP1' },
      { datum: '2026-11-20', tip: 'DP2', datumPre: '2026-11-19', tipPre: 'DP2' },
    ];
    expect(preracunajTipove(stavke, [true, true, true])).toEqual(['DP2', 'DP1', 'DP1']);
    expect(preracunajTipove(stavke, [false, true, true])).toEqual(['—', 'DP2', 'DP1']);
  });

  it('crveno „nije urađen“ samo za propušten pregled, inače sivo „pomeren na“', () => {
    expect(oznakaPomeranja({ propusten: true }).tekst).toBe('nije urađen – prenet na');
    expect(oznakaPomeranja({ propusten: false }).tekst).toBe('pomeren na');
  });
});

describe('otkazivanje i promena DP-a (zamerke 27, 31)', () => {
  it('DP se otkazuje sa „Pomeri raspored“ ili „Otkaži, zadrži raspored“, uz obavezan razlog', async () => {
    vi.mocked(api.post).mockResolvedValue(odgovor({ ...DP, status: 'ponisten' }));
    renderSaProviderima(<PregledKartica pregled={DP} onClose={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Otkaži pregled' }));
    await userEvent.click(screen.getByRole('button', { name: 'Otkaži, zadrži raspored' }));
    expect(await screen.findByText('Razlog je obavezan.')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();

    await userEvent.type(screen.getByLabelText('Razlog'), 'Fabrika ne radi');
    await userEvent.click(screen.getByRole('button', { name: 'Otkaži, zadrži raspored' }));
    expect(api.post).toHaveBeenCalledWith('/pregledi/7/otkazi', { razlog: 'Fabrika ne radi', pomeri: false });
  });

  it('otkazan PZ koji oslobodi dan nudi „Povuci raspored“', async () => {
    const pz = { ...DP, id: 9, broj: 'PZ1-2026-0001', tip: 'PZ1' };
    vi.mocked(api.post).mockImplementation(async (url: string) =>
      url === '/raspored/povuci' ? odgovor({ pomereno: 3 }) : odgovor({ ...pz, status: 'ponisten', slobodanDan: '2026-11-17' }));
    renderSaProviderima(<PregledKartica pregled={pz} onClose={() => {}} />);
    expect(screen.queryByRole('button', { name: 'Promeni DP' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Otkaži pregled' }));
    await userEvent.type(screen.getByLabelText('Razlog'), 'Nema delova');
    await userEvent.click(screen.getByRole('button', { name: 'Otkaži pregled' }));

    const dijalog = await screen.findByRole('dialog', { name: 'Dan je ostao bez DP-a' });
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Povuci raspored' }));
    expect(api.post).toHaveBeenCalledWith('/raspored/povuci', { datum: '2026-11-17' });
  });

  it('„Promeni DP“ pita da li se usklađuju naredni', async () => {
    vi.mocked(api.put).mockResolvedValue(odgovor({ ...DP, tip: 'DP1', broj: 'DP1-2026-0009' }));
    renderSaProviderima(<PregledKartica pregled={DP} onClose={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Promeni DP' }));
    await userEvent.click(screen.getByRole('button', { name: 'Da, uskladi naredne' }));
    expect(api.put).toHaveBeenCalledWith('/pregledi/7/tip', { tip: 'DP1', uskladi: true });
  });
});

describe('predlog sa kvačicama (zamerka 30)', () => {
  it('odštikliran dan se ne šalje, a rupa usred niza traži izbor „Umetni i pomeri naredne“', async () => {
    vi.mocked(api.post).mockImplementation(async (url: string) =>
      url === '/raspored/predlog'
        ? odgovor([
          { datum: '2026-11-17', tip: 'DP2', inzenjerId: 'ana', inzenjer: 'Ana', rupa: true, datumPre: '2026-11-16', tipPre: 'DP1' },
          { datum: '2026-11-20', tip: 'DP1', inzenjerId: 'bora', inzenjer: 'Bora', rupa: false, datumPre: '2026-11-19', tipPre: 'DP2' },
        ])
        : odgovor([]));
    renderSaProviderima(<RasporedPage />);
    await userEvent.click(await screen.findByRole('button', { name: 'Predloži raspored' }));
    const dijalog = await screen.findByRole('dialog', { name: 'Predlog rasporeda' });
    const kvacice = within(dijalog).getAllByRole('checkbox', { name: 'Zakaži' });
    expect(kvacice.every((k) => (k as HTMLInputElement).checked)).toBe(true);
    await userEvent.click(kvacice[1]);
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Potvrdi raspored' }));

    const pitanje = await screen.findByRole('dialog', { name: 'Dan usred rasporeda' });
    await userEvent.click(within(pitanje).getByRole('button', { name: 'Umetni i pomeri naredne' }));
    expect(api.post).toHaveBeenCalledWith('/raspored/potvrdi', { stavke: [{ datum: '2026-11-17', tip: 'DP2', inzenjerId: 'ana' }], uskladi: true });
  });
});
