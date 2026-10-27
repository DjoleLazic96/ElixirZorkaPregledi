import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { AuditFilteri, AuditRedDto, StranicaDto } from '../../api/types';
import { renderSaProviderima } from '../../test/utils';
import { AuditLogPage } from './AuditLogPage';

// Hookovi se mock-uju: hvatamo prosleđene filtere i vraćamo fiksne podatke.
const useAuditMock = vi.fn();
vi.mock('./hooks', () => ({
  useAudit: (filteri: AuditFilteri) => useAuditMock(filteri),
  useAuditEntiteti: () => ({
    data: [
      { entitet: 'Kategorija', labela: 'Kategorija' },
      { entitet: 'Auth', labela: 'Prijava' },
    ],
  }),
}));

const STRANICA: StranicaDto<AuditRedDto> = {
  stranica: 1,
  velicina: 50,
  ukupno: 3,
  stavke: [
    {
      id: 3,
      vreme: '2026-07-24T10:15:00Z',
      korisnik: 'system',
      akcija: 'START',
      entitet: 'Sistem',
      entitetLabela: 'Sistem',
      entitetId: null,
      ip: null,
      stareVrednosti: null,
      noveVrednosti: JSON.stringify({ verzija: '1.0.0' }),
    },
    {
      id: 2,
      vreme: '2026-07-24T10:15:00Z',
      korisnik: 'admin',
      akcija: 'LOGIN',
      entitet: 'Auth',
      entitetLabela: 'Prijava',
      entitetId: null,
      ip: '10.0.0.5',
      stareVrednosti: null,
      noveVrednosti: null,
    },
    {
      id: 1,
      vreme: '2026-07-24T09:00:00Z',
      korisnik: 'admin',
      akcija: 'UPDATE',
      entitet: 'Kategorija',
      entitetLabela: 'Kategorija',
      entitetId: '7',
      ip: '10.0.0.5',
      stareVrednosti: JSON.stringify({ Naziv: 'Stari' }),
      noveVrednosti: JSON.stringify({ Naziv: 'Novi' }),
    },
  ],
};

const UPIT_OK = { data: STRANICA, isPending: false, isError: false, isFetching: false, refetch: vi.fn() };

function poslednjiFilteri(): AuditFilteri {
  return useAuditMock.mock.calls.at(-1)![0] as AuditFilteri;
}

describe('AuditLogPage', () => {
  beforeEach(() => {
    useAuditMock.mockReset();
    useAuditMock.mockReturnValue(UPIT_OK);
  });

  it('renderuje redove sa srpskim labelama akcija (uključujući START → „Pokretanje")', () => {
    renderSaProviderima(<AuditLogPage />, { ruta: '/audit-log' });
    expect(screen.getByText('Izmena')).toBeInTheDocument();
    expect(screen.getByText('Pokretanje')).toBeInTheDocument();
    expect(screen.queryByText('UPDATE')).not.toBeInTheDocument();
    expect(screen.getAllByText('10.0.0.5')).toHaveLength(2);
  });

  it('čita filtere iz URL-a i šalje ih hook-u', () => {
    renderSaProviderima(<AuditLogPage />, { ruta: '/audit-log?korisnik=admin&stranica=3&velicina=25' });
    const f = poslednjiFilteri();
    expect(f.korisnik).toBe('admin');
    expect(f.stranica).toBe(3);
    expect(f.velicina).toBe(25);
    expect(f.akcija).toBeUndefined();
  });

  it('promena filtera „Akcija" menja upit i vraća na stranicu 1', async () => {
    renderSaProviderima(<AuditLogPage />, { ruta: '/audit-log?stranica=3' });
    expect(poslednjiFilteri().stranica).toBe(3);

    await userEvent.click(screen.getByRole('combobox', { name: 'Akcija' }));
    await userEvent.click(within(screen.getByRole('listbox')).getByRole('option', { name: 'Neuspešna prijava' }));

    const f = poslednjiFilteri();
    expect(f.akcija).toBe('LOGIN_FAIL');
    expect(f.stranica).toBe(1);
  });

  it('filter „Entitet" nudi listu sa backenda', async () => {
    renderSaProviderima(<AuditLogPage />, { ruta: '/audit-log' });
    await userEvent.click(screen.getByRole('combobox', { name: 'Entitet' }));
    await userEvent.click(screen.getByRole('option', { name: 'Kategorija' }));
    expect(poslednjiFilteri().entitet).toBe('Kategorija');
  });

  it('datumski filteri se šalju kao UTC granice lokalnog dana (ISO sa „Z")', async () => {
    renderSaProviderima(<AuditLogPage />, { ruta: '/audit-log?stranica=2' });

    await userEvent.click(screen.getByRole('button', { name: 'Od datuma' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Danas' }));
    await userEvent.click(screen.getByRole('button', { name: 'Do datuma' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Danas' }));

    const f = poslednjiFilteri();
    expect(f.od).toMatch(/Z$/);
    expect(f.doDatum).toMatch(/Z$/);
    expect(new Date(f.od!).getHours()).toBe(0);
    expect(new Date(f.doDatum!).getHours()).toBe(23);
    expect(f.stranica).toBe(1);
  });

  it('klik na red otvara modal sa diff prikazom', async () => {
    renderSaProviderima(<AuditLogPage />, { ruta: '/audit-log' });
    await userEvent.click(screen.getByText('Kategorija'));
    const dijalog = screen.getByRole('dialog');
    expect(dijalog).toHaveAccessibleName('Detalji — Kategorija');
    expect(within(dijalog).getByText('Stari')).toBeInTheDocument();
    expect(within(dijalog).getByText('Novi')).toBeInTheDocument();
  });

  it('greška učitavanja prikazuje poruku i „Pokušaj ponovo" umesto stare tabele', async () => {
    const refetch = vi.fn();
    useAuditMock.mockReturnValue({ ...UPIT_OK, isError: true, refetch });
    renderSaProviderima(<AuditLogPage />, { ruta: '/audit-log' });
    expect(screen.getByText('Učitavanje audit loga nije uspelo.')).toBeInTheDocument();
    expect(screen.queryByText('Izmena')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Pokušaj ponovo' }));
    expect(refetch).toHaveBeenCalled();
  });

  it('prazan rezultat prikazuje prazno stanje', () => {
    useAuditMock.mockReturnValue({ ...UPIT_OK, data: { ...STRANICA, stavke: [], ukupno: 0 } });
    renderSaProviderima(<AuditLogPage />, { ruta: '/audit-log' });
    expect(screen.getByText('Nema zapisa za zadate filtere.')).toBeInTheDocument();
  });

  it('prvo učitavanje prikazuje indikator', () => {
    useAuditMock.mockReturnValue({ data: undefined, isPending: true, isError: false, isFetching: true, refetch: vi.fn() });
    renderSaProviderima(<AuditLogPage />, { ruta: '/audit-log' });
    expect(screen.getByRole('status')).toHaveTextContent('Učitavanje…');
  });
});
