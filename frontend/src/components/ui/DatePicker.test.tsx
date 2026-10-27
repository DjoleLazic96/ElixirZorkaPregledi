import { describe, it, expect, vi } from 'vitest';
import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DatePicker } from './DatePicker';

function Harness({ initial = '', min, max, onChange }: { initial?: string; min?: string; max?: string; onChange?: (v: string) => void }) {
  const [v, setV] = useState(initial);
  return (
    <DatePicker
      aria-label="Datum"
      value={v}
      min={min}
      max={max}
      onChange={(x) => {
        setV(x);
        onChange?.(x);
      }}
    />
  );
}

describe('DatePicker', () => {
  it('prikazuje vrednost kao dd.MM.yyyy, a bez vrednosti „Odaberite datum"', () => {
    const { rerender } = render(<DatePicker value="2026-10-12" onChange={vi.fn()} aria-label="Datum" />);
    expect(screen.getByRole('button', { name: 'Datum' })).toHaveTextContent('12.10.2026');
    rerender(<DatePicker value="" onChange={vi.fn()} aria-label="Datum" />);
    expect(screen.getByRole('button', { name: 'Datum' })).toHaveTextContent('Odaberite datum');
  });

  it('otvara kalendar na mesecu izabranog datuma i izbor dana vraća YYYY-MM-DD', async () => {
    const onChange = vi.fn();
    render(<Harness initial="2026-10-12" onChange={onChange} />);
    await userEvent.click(screen.getByRole('button', { name: 'Datum' }));
    expect(screen.getByText('Oktobar 2026')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: '05.10.2026' }));
    expect(onChange).toHaveBeenCalledWith('2026-10-05');
    // Panel se zatvara posle izbora.
    expect(screen.queryByText('Oktobar 2026')).not.toBeInTheDocument();
  });

  it('dani van min/max su onemogućeni', async () => {
    render(<Harness initial="2026-10-12" min="2026-10-10" max="2026-10-15" />);
    await userEvent.click(screen.getByRole('button', { name: 'Datum' }));
    expect(screen.getByRole('button', { name: '09.10.2026' })).toBeDisabled();
    expect(screen.getByRole('button', { name: '10.10.2026' })).toBeEnabled();
    expect(screen.getByRole('button', { name: '15.10.2026' })).toBeEnabled();
    expect(screen.getByRole('button', { name: '16.10.2026' })).toBeDisabled();
  });

  it('Esc zatvara kalendar', async () => {
    render(<Harness initial="2026-10-12" />);
    await userEvent.click(screen.getByRole('button', { name: 'Datum' }));
    expect(screen.getByText('Oktobar 2026')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByText('Oktobar 2026')).not.toBeInTheDocument();
  });

  it('„Obriši" prazni vrednost', async () => {
    const onChange = vi.fn();
    render(<Harness initial="2026-10-12" onChange={onChange} />);
    await userEvent.click(screen.getByRole('button', { name: 'Obriši datum' }));
    expect(onChange).toHaveBeenCalledWith('');
    expect(screen.getByRole('button', { name: 'Datum' })).toHaveTextContent('Odaberite datum');
  });

  it('prikazani mesec prati vrednost promenjenu spolja', async () => {
    const { rerender } = render(<DatePicker value="2026-10-12" onChange={vi.fn()} aria-label="Datum" />);
    rerender(<DatePicker value="2027-02-01" onChange={vi.fn()} aria-label="Datum" />);
    await userEvent.click(screen.getByRole('button', { name: 'Datum' }));
    expect(screen.getByText('Februar 2027')).toBeInTheDocument();
  });
});
