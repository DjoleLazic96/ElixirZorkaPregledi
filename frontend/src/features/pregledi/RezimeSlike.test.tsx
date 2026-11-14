import { describe, it, expect } from 'vitest';
import { screen } from '@testing-library/react';
import { renderSaProviderima } from '../../test/utils';
import type { PregledDetaljDto } from './hooks';
import { RezimePregleda } from './PrikazPregleda';

const stavka = (id: number, aktivnost: string, napomena: string | null, fotografije: number[]) => ({
  stavkaId: id, aktivnost, odgovor: 'Okreće', neispravan: false, napomena, polja: [], fotografije,
});

const P = {
  id: 1, broj: 'DP1-2026-0001', tip: 'DP1', status: 'ceka_overu', dorada: 0, planiraniDatum: '2026-11-16', prvobitniDatum: '2026-11-16',
  dodeljenId: 'ana', inzenjer: 'Ana', verzijaBroj: 1, zapocetAt: null, predatAt: null, aktivnoSekundi: 0, delova: 1, overio: null,
  overenoAt: null, overioDirektor: false, ponistenRazlog: null, upozorenja: [], istorija: [], sporniUnosi: [], zahteviDorade: [], ucesnici: [],
  sekcije: [{ sifra: '10', uredjaji: [{ uredjajId: 5, naziv: 'Redler 10-C-01', vanFunkcije: false, stavke: [
    stavka(1, 'Lanac', null, [77]),
    stavka(2, 'Ležajevi', 'Počinje da se greje', []),
    stavka(3, 'Rolne', null, []),
  ] }] }],
} as PregledDetaljDto;

describe('rezime — slike uz ispravne stavke (zamerka 73)', () => {
  it('ispravna stavka sa slikom ulazi u rezime i bez napomene, a ona bez ičega ne ulazi', () => {
    renderSaProviderima(<RezimePregleda p={P} />);
    expect(screen.getByText('Napomene i slike uz ispravne stavke')).toBeInTheDocument();
    expect(screen.getByText(/Redler 10-C-01 · Lanac/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Slika' })).toBeInTheDocument();
    expect(screen.getByText('Počinje da se greje')).toBeInTheDocument();
    expect(screen.queryByText(/Rolne/)).not.toBeInTheDocument();
  });
});
