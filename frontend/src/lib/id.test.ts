import { describe, it, expect, vi, afterEach } from 'vitest';
import { noviId } from './id';

describe('noviId', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('bez crypto.randomUUID (http adresa u lokalnoj mreži) i dalje daje ispravan UUID v4', () => {
    const pravi = globalThis.crypto;
    vi.stubGlobal('crypto', { getRandomValues: (a: Uint8Array<ArrayBuffer>) => pravi.getRandomValues(a) });
    const id = noviId();
    expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(noviId()).not.toBe(id);
  });
});
