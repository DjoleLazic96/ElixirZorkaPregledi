import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ServerStanje } from '../../api/types';
import { StanjeServeraPage } from './StanjeServeraPage';

const useServerStanjeMock = vi.fn();
vi.mock('./hooks', () => ({ useServerStanje: () => useServerStanjeMock() }));

const STANJE: ServerStanje = {
  aplikacija: {
    verzija: '1.2.3',
    gitSha: 'ab12cd3',
    dotnetVerzija: '10.0.0',
    okruzenje: 'Production',
    uptimeSekundi: 90_061, // 1 d 1 h 1 min
    vremeUtc: '2026-07-24T10:00:00Z',
  },
  baza: {
    veza: true,
    postgresVerzija: '16.2',
    velicinaBazeBytes: 3 * 1024 ** 3,
    aktivneKonekcije: 3,
    odzivMs: 4,
    poslednjiBackupUtc: '2026-07-24T02:00:00Z',
    greska: null,
  },
  host: {
    osOpis: 'Linux 6.1',
    cpuProcenat: 42.5,
    ramUkupnoBytes: 8 * 1024 ** 3,
    ramIskoriscenoBytes: 4 * 1024 ** 3,
    diskUkupnoBytes: 100 * 1024 ** 3,
    diskSlobodnoBytes: 50 * 1024 ** 3,
  },
};

function upit(prepis: Partial<ReturnType<typeof useServerStanjeMock>> & { data?: ServerStanje | undefined } = {}) {
  return {
    data: STANJE,
    isPending: false,
    isError: false,
    isFetching: false,
    dataUpdatedAt: Date.now() - 7_000,
    refetch: vi.fn(),
    ...prepis,
  };
}

describe('StanjeServeraPage', () => {
  beforeEach(() => {
    useServerStanjeMock.mockReset();
    useServerStanjeMock.mockReturnValue(upit());
  });

  it('renderuje tri kartice i osnovne podatke aplikacije', () => {
    render(<StanjeServeraPage />);
    ['Aplikacija', 'Baza', 'Host'].forEach((n) => expect(screen.getByText(n)).toBeInTheDocument());
    expect(screen.getByText('1.2.3')).toBeInTheDocument();
    expect(screen.getByText('ab12cd3')).toBeInTheDocument();
    expect(screen.getByText('Production')).toBeInTheDocument();
    expect(screen.getByText('1 d 1 h 1 min')).toBeInTheDocument();
  });

  it('vreme servera je u UTC sa oznakom, ne u lokalnoj zoni', () => {
    render(<StanjeServeraPage />);
    expect(screen.getByText('24.07.2026 10:00 (UTC)')).toBeInTheDocument();
    expect(screen.getByText('24.07.2026 02:00 (UTC)')).toBeInTheDocument();
  });

  it('bajtovi se formatiraju srpski (zarez, jedna decimala)', () => {
    render(<StanjeServeraPage />);
    expect(screen.getByText('3,0 GB')).toBeInTheDocument();
    expect(screen.getByText(/100,0 GB/)).toBeInTheDocument();
  });

  it('trake napretka dobijaju boju po pragovima (CPU 70/90, RAM 75/90, disk 20/10)', () => {
    render(<StanjeServeraPage />);
    expect(screen.getByRole('progressbar', { name: 'Procesor (CPU)' })).toHaveAttribute('data-ton', 'ok');
    expect(screen.getByRole('progressbar', { name: 'Memorija (RAM)' })).toHaveAttribute('data-ton', 'ok');
    expect(screen.getByRole('progressbar', { name: 'Disk (slobodno)' })).toHaveAttribute('data-ton', 'ok');
  });

  it('visok CPU i RAM i malo slobodnog diska daju žuto/crveno', () => {
    useServerStanjeMock.mockReturnValue(
      upit({
        data: {
          ...STANJE,
          host: {
            ...STANJE.host!,
            cpuProcenat: 75,
            ramIskoriscenoBytes: 7.5 * 1024 ** 3, // 93,75 %
            diskSlobodnoBytes: 5 * 1024 ** 3, // 5 %
          },
        },
      }),
    );
    render(<StanjeServeraPage />);
    expect(screen.getByRole('progressbar', { name: 'Procesor (CPU)' })).toHaveAttribute('data-ton', 'upozorenje');
    expect(screen.getByRole('progressbar', { name: 'Memorija (RAM)' })).toHaveAttribute('data-ton', 'kriticno');
    expect(screen.getByRole('progressbar', { name: 'Disk (slobodno)' })).toHaveAttribute('data-ton', 'kriticno');
  });

  it('host null → „nedostupno", kartica ostaje', () => {
    useServerStanjeMock.mockReturnValue(upit({ data: { ...STANJE, host: null } }));
    render(<StanjeServeraPage />);
    expect(screen.getByText('Host')).toBeInTheDocument();
    expect(screen.getByText(/Metrike hosta su nedostupno/)).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('baza bez veze: crvena kartica sa tipom greške; backup null → „nepoznato"', () => {
    useServerStanjeMock.mockReturnValue(
      upit({
        data: {
          ...STANJE,
          baza: {
            veza: false,
            postgresVerzija: null,
            velicinaBazeBytes: null,
            aktivneKonekcije: null,
            odzivMs: null,
            poslednjiBackupUtc: null,
            greska: 'NpgsqlException',
          },
        },
      }),
    );
    render(<StanjeServeraPage />);
    expect(screen.getByText('Nije povezano')).toBeInTheDocument();
    expect(screen.getByText('NpgsqlException')).toBeInTheDocument();
    expect(screen.getByText('nepoznato')).toBeInTheDocument();
  });

  it('dugme „Osveži" poziva refetch, a tekst kaže pre koliko sekundi je osveženo', async () => {
    const u = upit();
    useServerStanjeMock.mockReturnValue(u);
    render(<StanjeServeraPage />);
    expect(screen.getByText(/Osveženo pre \d+ s/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /Osveži/ }));
    expect(u.refetch).toHaveBeenCalledOnce();
  });

  it('prvo učitavanje prikazuje skeleton kartice', () => {
    useServerStanjeMock.mockReturnValue(upit({ data: undefined, isPending: true, dataUpdatedAt: 0 }));
    render(<StanjeServeraPage />);
    expect(screen.getByRole('status', { name: 'Učitavanje…' })).toBeInTheDocument();
  });

  it('greška bez podataka prikazuje poruku i „Pokušaj ponovo"', () => {
    useServerStanjeMock.mockReturnValue(upit({ data: undefined, isError: true, dataUpdatedAt: 0 }));
    render(<StanjeServeraPage />);
    expect(screen.getByText('Učitavanje stanja servera nije uspelo.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pokušaj ponovo' })).toBeInTheDocument();
  });
});
