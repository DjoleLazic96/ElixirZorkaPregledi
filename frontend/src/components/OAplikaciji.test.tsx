import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen } from '@testing-library/react';
import { api } from '../api/client';
import { VERZIJA } from '../lib/verzija';
import { odgovor, renderSaProviderima } from '../test/utils';
import { OAplikaciji } from './OAplikaciji';

vi.mock('../api/client', () => ({ api: { get: vi.fn() } }));

const SERVER = { gitSha: 'ab12cd3', buildUtc: '2026-10-12T09:30:00Z', okruzenje: 'Production' };

describe('OAplikaciji', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockReset();
  });

  it('prikazuje naziv, verziju frontenda i verziju servera iz /version', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor({ ...SERVER, verzija: VERZIJA }));
    renderSaProviderima(<OAplikaciji open onClose={vi.fn()} />);

    expect(screen.getByRole('dialog', { name: 'O aplikaciji' })).toBeInTheDocument();
    expect(screen.getByText('TPM pregledi')).toBeInTheDocument();
    expect(await screen.findByText('Production')).toBeInTheDocument();
    expect(screen.getByText('12.10.2026 09:30 (UTC)')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/version');
    // Iste verzije — bez upozorenja.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('upozorava kad se verzija frontenda i servera razlikuju', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor({ ...SERVER, verzija: `${VERZIJA}-novija` }));
    renderSaProviderima(<OAplikaciji open onClose={vi.fn()} />);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Učitana je starija verzija. Osvežite stranicu (Ctrl+F5).',
    );
  });

  it('kad /version ne radi, prikazuje „nedostupno" umesto da pukne', async () => {
    vi.mocked(api.get).mockRejectedValue(new Error('mreža'));
    renderSaProviderima(<OAplikaciji open onClose={vi.fn()} />);
    expect((await screen.findAllByText('nedostupno')).length).toBeGreaterThan(0);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('zatvoren dijalog ne poziva /version', () => {
    renderSaProviderima(<OAplikaciji open={false} onClose={vi.fn()} />);
    expect(api.get).not.toHaveBeenCalled();
  });
});
