import { describe, it, expect, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { api } from '../../api/client';
import { odgovor, renderSaProviderima } from '../../test/utils';
import { primeniLokalno } from '../../offline/lokalno';
import type { PopunjavanjeDto } from '../popunjavanje/hooks';
import type { AkcijaDto } from './hooks';
import { AkcijePage } from './AkcijePages';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

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
});
