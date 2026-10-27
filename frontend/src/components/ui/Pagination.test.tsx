import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Pagination } from './Pagination';

const LABELA = { jednina: 'zapis', paukal: 'zapisa', mnozina: 'zapisa' };

describe('Pagination', () => {
  it('prikazuje opseg i množinu na srpskom', () => {
    render(<Pagination page={2} pageSize={50} total={120} onPageChange={vi.fn()} onPageSizeChange={vi.fn()} itemLabel={LABELA} />);
    expect(screen.getByText(/Prikaz/).textContent).toBe('Prikaz 51–100 od 120 zapisa');
    expect(screen.getByText(/Stranica/).textContent).toBe('Stranica 2 od 3');
  });

  it('na prvoj stranici su „prva" i „prethodna" onemogućene, „sledeća" vodi na 2', async () => {
    const onPageChange = vi.fn();
    render(<Pagination page={1} pageSize={50} total={120} onPageChange={onPageChange} onPageSizeChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Prva stranica' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Prethodna stranica' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Sledeća stranica' }));
    expect(onPageChange).toHaveBeenCalledWith(2);
    await userEvent.click(screen.getByRole('button', { name: 'Poslednja stranica' }));
    expect(onPageChange).toHaveBeenCalledWith(3);
  });

  it('prazna lista prikazuje „0–0 od 0" i jednu stranicu', () => {
    render(<Pagination page={1} pageSize={50} total={0} onPageChange={vi.fn()} onPageSizeChange={vi.fn()} />);
    expect(screen.getByText(/Prikaz/).textContent).toBe('Prikaz 0–0 od 0 stavki');
    expect(screen.getByRole('button', { name: 'Sledeća stranica' })).toBeDisabled();
  });

  it('promena veličine stranice poziva onPageSizeChange', async () => {
    const onPageSizeChange = vi.fn();
    render(<Pagination page={1} pageSize={50} total={120} onPageChange={vi.fn()} onPageSizeChange={onPageSizeChange} />);
    await userEvent.click(screen.getByRole('combobox', { name: 'Broj stavki po stranici' }));
    await userEvent.click(screen.getByRole('option', { name: '100' }));
    expect(onPageSizeChange).toHaveBeenCalledWith(100);
  });
});
