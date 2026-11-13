import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderSaProviderima } from '../../test/utils';
import { brojSlika } from '../../lib/broj';
import { ivicaPregleda, oznakaPregleda } from '../raspored/kalendar';
import type { PopunjavanjeDto } from './hooks';
import { NavigacijaSekcija, PredlogIzPregleda, RezimePredaje } from './PopunjavanjePage';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn() } }));
vi.mock('../auth/AuthProvider', () => ({ useAuth: () => ({ me: { user: { id: 'ana' }, roles: ['Inzenjer'], prava: ['pregledi.popunjavanje'] } }) }));

const sekcija = (sifra: string, odgovoreno: boolean) => ({
  sifra, moja: true, radi: 'Ana', uredjaji: [{
    uredjajId: 1, naziv: 'Elevator', oznaka: '10-C-13', vanFunkcije: false, prosliPutVanFunkcije: false, otvoreneAkcije: [],
    stavke: [{ stavkaId: Number(sifra), aktivnost: 'A', odgovorIspravan: 'Da', odgovorNeispravan: 'Ne', polja: [], fotografije: [], aktivnostId: 1, uputstvo: null, uputstvoSlika: 0,
      odgovor: odgovoreno ? { vrednost: 0 as const, napomena: null, polja: {} } : null }],
  }],
});

const P = { id: 1, broj: 'PZ1-2026-0001', tip: 'PZ1', status: 'u_radu', planiraniDatum: '2026-11-10', zapocetAt: null, verzijaId: 1,
  sekcije: [sekcija('10', true), sekcija('20', false), sekcija('30', false)] } as unknown as PopunjavanjeDto;

describe('Zamerke 6, 7, 3, 10', () => {
  it('broj slika ide u drugi red sa srpskom množinom (zamerka 6)', () => {
    expect([1, 2, 4, 5, 11, 21, 22].map(brojSlika)).toEqual(['1 slika', '2 slike', '4 slike', '5 slika', '11 slika', '21 slika', '22 slike']);
  });

  it('dugmad za prethodnu i sledeću sekciju; na prvoj nema „prethodne“ (zamerka 7)', async () => {
    const idi = vi.fn();
    const { unmount } = renderSaProviderima(<NavigacijaSekcija p={P} aktivna="10" idi={idi} />);
    expect(screen.queryByRole('button', { name: /← Sekcija/ })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Sekcija 20 →' }));
    expect(idi).toHaveBeenCalledWith('20');
    unmount();
    renderSaProviderima(<NavigacijaSekcija p={P} aktivna="30" idi={idi} />);
    await userEvent.click(screen.getByRole('button', { name: '← Sekcija 20' }));
    expect(idi).toHaveBeenLastCalledWith('20');
    expect(screen.queryByRole('button', { name: /→/ })).not.toBeInTheDocument();
  });

  it('pregled odsutnog inženjera je crven, a onaj sa zahtevom na čekanju žut isprekidan (zamerke 3 i 10)', () => {
    expect(ivicaPregleda({ inzenjerOdsutan: true })).toContain('border-red-500');
    expect(oznakaPregleda({ inzenjerOdsutan: true })?.tekst).toBe('inženjer odsutan');
    expect(ivicaPregleda({ zahtevNaCekanju: 'prebacivanje' })).toContain('border-dashed');
    expect(oznakaPregleda({ zahtevNaCekanju: 'raniji_pocetak' })?.tekst).toBe('čeka raniji početak');
    expect(oznakaPregleda({})).toBeNull();
  });
});

describe('Zamerke 34 i 35', () => {
  it('rezime pre predaje broji stavke i nabraja neispravne; „Vrati se“ ne predaje', async () => {
    const neispravna = sekcija('20', false);
    (neispravna.uredjaji[0].stavke[0] as { odgovor: unknown }).odgovor = { vrednost: 1, napomena: 'curi', polja: {} };
    const p = { ...P, sekcije: [sekcija('10', true), neispravna] } as unknown as PopunjavanjeDto;
    const predaj = vi.fn();
    const nazad = vi.fn();
    renderSaProviderima(<RezimePredaje p={p} saljem={false} onPredaj={predaj} onNazad={nazad} />);
    expect(screen.getByText('Neispravne stavke')).toBeInTheDocument();
    expect(screen.getByText(/20 · Elevator 10-C-13 · A/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Vrati se' }));
    expect(nazad).toHaveBeenCalled();
    expect(predaj).not.toHaveBeenCalled();
  });

  it('predlog iz pregleda upisuje sekciju u opis i ide kao operacija pregleda', async () => {
    const posalji = vi.fn().mockResolvedValue(true);
    renderSaProviderima(<PredlogIzPregleda tip="PZ1" sekcija="30" onClose={() => {}} onPosalji={posalji} />);
    await userEvent.type(screen.getByLabelText('Šta treba promeniti'), 'Fali ventilator 30-B-05');
    await userEvent.type(screen.getByLabelText('Razlog'), 'Ugrađen prošle nedelje');
    await userEvent.click(screen.getByRole('button', { name: 'Pošalji predlog' }));
    expect(posalji).toHaveBeenCalledWith({ vrsta: 'dodavanje', opis: 'Sekcija 30: Fali ventilator 30-B-05', razlog: 'Ugrađen prošle nedelje' });
  });
});
