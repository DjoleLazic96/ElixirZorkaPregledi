import axios from 'axios';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { obnoviSesiju } from './client';

const odgovor = { data: { accessToken: 't2', expiresAt: '2026-11-13T12:00:00Z', user: {}, roles: [], prava: [] } };

describe('obnoviSesiju', () => {
  afterEach(() => vi.restoreAllMocks());

  it('istovremene obnove (tajmer, zahtev sa isteklim tokenom, start) šalju jedan refresh — rotiran token se ne troši dvaput', async () => {
    let pusti!: () => void;
    const post = vi.spyOn(axios, 'post').mockImplementation(() => new Promise((r) => { pusti = () => r(odgovor); }));
    const a = obnoviSesiju();
    const b = obnoviSesiju();
    await vi.waitFor(() => expect(post).toHaveBeenCalled());
    pusti();
    await expect(a).resolves.toEqual(odgovor.data);
    await expect(b).resolves.toEqual(odgovor.data);
    expect(post).toHaveBeenCalledTimes(1);
  });

  it('posle završene obnove sledeća ide ponovo na server', async () => {
    const post = vi.spyOn(axios, 'post').mockResolvedValue(odgovor);
    await obnoviSesiju();
    await obnoviSesiju();
    expect(post).toHaveBeenCalledTimes(2);
  });

  it('između kartica obnova ide kroz Web Locks (jedna po jedna)', async () => {
    const request = vi.fn((_ime: string, posao: () => Promise<unknown>) => posao());
    vi.stubGlobal('navigator', { ...navigator, locks: { request } });
    vi.spyOn(axios, 'post').mockResolvedValue(odgovor);
    await obnoviSesiju();
    expect(request).toHaveBeenCalledWith('tpm-pregledi.refresh', expect.any(Function));
    vi.unstubAllGlobals();
  });
});
