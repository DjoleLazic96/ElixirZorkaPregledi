import { describe, it, expect, vi, afterEach } from 'vitest';
import { imeFajlaIzDispozicije, preuzmiFajl, sacuvajBlob } from './download';
import { api } from '../api/client';
import { odgovor } from '../test/utils';

vi.mock('../api/client', () => ({ api: { get: vi.fn() } }));

describe('lib/download — imeFajlaIzDispozicije', () => {
  it('parsira quoted i unquoted filename', () => {
    expect(imeFajlaIzDispozicije('attachment; filename="Kategorije_2026-07.xlsx"')).toBe(
      'Kategorije_2026-07.xlsx',
    );
    expect(imeFajlaIzDispozicije('attachment; filename=Kategorije_2026-07.xlsx')).toBe(
      'Kategorije_2026-07.xlsx',
    );
  });

  it('daje prednost RFC 5987 filename*= i dekodira ga', () => {
    expect(
      imeFajlaIzDispozicije("attachment; filename=plain.xlsx; filename*=UTF-8''Izvoz%20kategorija.xlsx"),
    ).toBe('Izvoz kategorija.xlsx');
  });

  it('vraća fallback kad nema headera', () => {
    expect(imeFajlaIzDispozicije(null, 'fallback.xlsx')).toBe('fallback.xlsx');
    expect(imeFajlaIzDispozicije(undefined)).toBe('preuzimanje.bin');
  });
});

describe('lib/download — preuzimanje', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('sacuvajBlob klikne privremeni <a download> i odloženo oslobodi URL', () => {
    vi.useFakeTimers();
    const createObjectURL = vi.fn(() => 'blob:x');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
    const klik = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    sacuvajBlob(new Blob(['x']), 'test.xlsx');

    expect(createObjectURL).toHaveBeenCalledOnce();
    expect(klik).toHaveBeenCalledOnce();
    expect(revokeObjectURL).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1_000);
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:x');
    vi.unstubAllGlobals();
  });

  it('preuzmiFajl traži blob sa dugim timeout-om i koristi ime iz headera', async () => {
    vi.stubGlobal('URL', { ...URL, createObjectURL: () => 'blob:y', revokeObjectURL: () => {} });
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    const r = odgovor(new Blob(['x']));
    r.headers = { 'content-disposition': 'attachment; filename="Izvoz.xlsx"' };
    vi.mocked(api.get).mockResolvedValueOnce(r);

    await preuzmiFajl('/kategorije/excel', { pretraga: 'a' }, 'fallback.xlsx');

    expect(api.get).toHaveBeenCalledWith('/kategorije/excel', {
      params: { pretraga: 'a' },
      responseType: 'blob',
      timeout: 120_000,
    });
    vi.unstubAllGlobals();
  });
});
