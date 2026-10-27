import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Tabs } from './Tabs';

const ITEMS = [
  { key: 'korisnici', label: 'Korisnici' },
  { key: 'podesavanja', label: 'Podešavanja' },
] as const;

describe('Tabs', () => {
  it('ima tablist semantiku i označava aktivan tab', () => {
    render(<Tabs items={ITEMS} value="korisnici" onChange={vi.fn()} aria-label="Administracija" />);
    expect(screen.getByRole('tablist', { name: 'Administracija' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Korisnici' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Podešavanja' })).toHaveAttribute('aria-selected', 'false');
  });

  it('klik na tab poziva onChange sa ključem', async () => {
    const onChange = vi.fn();
    render(<Tabs items={ITEMS} value="korisnici" onChange={onChange} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Podešavanja' }));
    expect(onChange).toHaveBeenCalledWith('podesavanja');
  });

  it('strelica desno bira sledeći tab (kružno)', async () => {
    const onChange = vi.fn();
    render(<Tabs items={ITEMS} value="podesavanja" onChange={onChange} />);
    screen.getByRole('tab', { name: 'Podešavanja' }).focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(onChange).toHaveBeenCalledWith('korisnici');
  });
});
