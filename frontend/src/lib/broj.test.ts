import { describe, it, expect } from 'vitest';
import {
  formatirajBajte,
  formatirajBroj,
  formatirajKolicinu,
  formatirajProcenat,
  mnozina,
  parsirajBroj,
} from './broj';

describe('lib/broj', () => {
  it('formatirajBroj koristi tačku za hiljade i zarez za decimale', () => {
    expect(formatirajBroj(1234567.891)).toBe('1.234.567,89');
    expect(formatirajBroj(0.5, 1)).toBe('0,5');
    expect(formatirajBroj(42, 0)).toBe('42');
    expect(formatirajBroj(-1234.5)).toBe('-1.234,50');
  });

  it('formatirajBroj vraća „—" (ili zadati tekst) za prazno', () => {
    expect(formatirajBroj(null)).toBe('—');
    expect(formatirajBroj(undefined)).toBe('—');
    expect(formatirajBroj(Number.NaN)).toBe('—');
    expect(formatirajBroj(null, 2, 'n/a')).toBe('n/a');
  });

  it('formatirajKolicinu dodaje jedinicu sa razmakom i podrazumevane decimale po jedinici', () => {
    expect(formatirajKolicinu(1234.5, 'kWh')).toBe('1.234,50 kWh');
    expect(formatirajKolicinu(2.5, 't')).toBe('2,500 t');
    expect(formatirajKolicinu(8, 'h')).toBe('8,00 h');
    expect(formatirajKolicinu(87.4, '%')).toBe('87 %');
    expect(formatirajKolicinu(10, 'kg', 0)).toBe('10 kg');
    expect(formatirajKolicinu(null, 'kWh')).toBe('—');
  });

  it('formatirajProcenat daje „87 %" i poštuje decimale', () => {
    expect(formatirajProcenat(87)).toBe('87 %');
    expect(formatirajProcenat(42.5, 1)).toBe('42,5 %');
  });

  it('formatirajBajte bira jedinicu i daje jednu decimalu (ispod 1 KB bez decimala)', () => {
    expect(formatirajBajte(7.5 * 1024 ** 3)).toBe('7,5 GB');
    expect(formatirajBajte(3 * 1024 ** 3)).toBe('3,0 GB');
    expect(formatirajBajte(1536)).toBe('1,5 KB');
    expect(formatirajBajte(512)).toBe('512 B');
    expect(formatirajBajte(0)).toBe('0 B');
    expect(formatirajBajte(2 * 1024 ** 4)).toBe('2,0 TB');
    expect(formatirajBajte(-1)).toBe('—');
  });

  it('parsirajBroj prihvata zarez i tačku, ignoriše razmake, prazno → null', () => {
    expect(parsirajBroj('')).toBeNull();
    expect(parsirajBroj('   ')).toBeNull();
    expect(parsirajBroj('12,5')).toBe(12.5);
    expect(parsirajBroj('12.5')).toBe(12.5);
    expect(parsirajBroj('1.234,56')).toBe(1234.56);
    expect(parsirajBroj('1,234.56')).toBe(1234.56);
    expect(parsirajBroj(' 1 000 ')).toBe(1000);
    expect(parsirajBroj('-3,2')).toBe(-3.2);
    expect(parsirajBroj('abc')).toBeNull();
    expect(parsirajBroj('1,2,3')).toBeNull();
  });

  it('mnozina bira jedninu, paukal ili množinu po srpskom pravilu', () => {
    const oblici = { jednina: 'zapis', paukal: 'zapisa', mnozina: 'zapisa' };
    const z = { jednina: 'stavka', paukal: 'stavke', mnozina: 'stavki' };
    expect(mnozina(1, z)).toBe('stavka');
    expect(mnozina(2, z)).toBe('stavke');
    expect(mnozina(4, z)).toBe('stavke');
    expect(mnozina(5, z)).toBe('stavki');
    expect(mnozina(11, z)).toBe('stavki');
    expect(mnozina(12, z)).toBe('stavki');
    expect(mnozina(21, z)).toBe('stavka');
    expect(mnozina(22, z)).toBe('stavke');
    expect(mnozina(112, z)).toBe('stavki');
    expect(mnozina(0, oblici)).toBe('zapisa');
  });
});
