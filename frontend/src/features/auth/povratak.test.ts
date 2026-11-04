import { describe, it, expect } from 'vitest';
import { adresaPovratka } from './povratak';

describe('Povratak posle prijave', () => {
  it('vraća na unutrašnju adresu koju je korisnik otvorio (npr. QR nalepnica)', () => {
    expect(adresaPovratka({ od: '/qr/6f1c0f3e?x=1' })).toBe('/qr/6f1c0f3e?x=1');
  });

  it('spoljna adresa, /login i prazno stanje vode na Početnu', () => {
    expect(adresaPovratka({ od: '//zlo.example' })).toBe('/');
    expect(adresaPovratka({ od: 'https://zlo.example' })).toBe('/');
    expect(adresaPovratka({ od: '/login' })).toBe('/');
    expect(adresaPovratka(null)).toBe('/');
    expect(adresaPovratka(undefined)).toBe('/');
  });
});
