import { describe, it, expect } from 'vitest';
import { cn } from './cn';

describe('cn', () => {
  it('spaja klase i rešava Tailwind konflikt (poslednja pobeđuje)', () => {
    expect(cn('px-2 py-1', 'px-4')).toBe('py-1 px-4');
  });
  it('preskače falsy vrednosti', () => {
    expect(cn('a', false, undefined, 'b')).toBe('a b');
  });
});
