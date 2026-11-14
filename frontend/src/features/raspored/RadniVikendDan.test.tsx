import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { api } from '../../api/client';
import { odgovor, renderSaProviderima } from '../../test/utils';
import { danSaDatumom, danasYmd, dodajDane, ponedeljakNedelje } from '../../lib/datum';
import { DodajDpDijalog } from './IzmeneRasporeda';
import { RasporedPage } from './RasporedPage';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }));

const pristup = { user: { id: 'dir', userName: 'dir', fullName: 'Direktor', email: null }, roles: ['Direktor'], prava: ['raspored.uredjivanje'] };
vi.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ me: pristup }) }));

const SUBOTA = '2026-11-21';
// Nedelja ove nedelje nikad nije u prošlosti, pa dugme „Dodaj DP“ uvek sme da se pokaže.
const NEDELJA = dodajDane(ponedeljakNedelje(danasYmd()), 6);

function predlog(rupa: boolean) {
  vi.mocked(api.get).mockImplementation(async (url: string) => {
    if (url === '/raspored/predlog-dana') return odgovor({ datum: SUBOTA, tip: 'DP2', inzenjerId: 'bora', inzenjer: 'Bora', rupa });
    if (url === '/raspored/inzenjeri') return odgovor([{ id: 'ana', ime: 'Ana' }, { id: 'bora', ime: 'Bora' }]);
    if (url === '/raspored') return odgovor({ pregledi: [], neradniDani: [], odsustva: [], radniDani: [{ datum: NEDELJA, naziv: 'Remont' }] });
    return odgovor([]);
  });
}

beforeEach(() => {
  vi.mocked(api.get).mockReset();
  vi.mocked(api.post).mockReset();
  vi.mocked(api.post).mockResolvedValue(odgovor([]));
});

describe('radni vikend dan (zamerka 68)', () => {
  it('naslov kaže dan u nedelji', () => {
    expect(danSaDatumom(SUBOTA)).toBe('Subota 21.11.2026');
  });

  it('predlaže sledeći DP i inženjera po rotaciji; bez narednih DP-ova odmah dodaje sa izabranim tipom', async () => {
    predlog(false);
    renderSaProviderima(<DodajDpDijalog datum={SUBOTA} onClose={() => {}} />);
    expect(await screen.findByText('DP2 (sledeći po redu)')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Dodaj DP' }));
    expect(api.post).toHaveBeenCalledWith('/raspored/potvrdi', {
      stavke: [{ datum: SUBOTA, tip: 'DP2', inzenjerId: 'bora' }], uskladi: false, postujTip: true,
    });
  });

  it('kad posle dana postoje DP-ovi, pita da li utiče na naredne', async () => {
    predlog(true);
    renderSaProviderima(<DodajDpDijalog datum={SUBOTA} onClose={() => {}} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Dodaj DP' }));
    expect(await screen.findByText('Da li utiče na naredne DP-ove?')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Umetni i pomeri naredne' }));
    expect(api.post).toHaveBeenCalledWith('/raspored/potvrdi', expect.objectContaining({ uskladi: true, postujTip: false }));
  });

  it('Raspored pokazuje radni dan i nudi „Dodaj DP“', async () => {
    predlog(false);
    renderSaProviderima(<RasporedPage />);
    expect(await screen.findByText('Radni dan · Remont')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: '+ Dodaj DP' }));
    expect(await screen.findByText(`Dodaj DP — ${danSaDatumom(NEDELJA)}`)).toBeInTheDocument();
  });
});
