import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MesecPicker } from './MesecPicker';

describe('MesecPicker', () => {
  it('prikazuje izabrani mesec imenom i godinu', () => {
    render(<MesecPicker year={2026} month={10} onChange={vi.fn()} />);
    expect(screen.getByRole('combobox', { name: 'Mesec' })).toHaveTextContent('Oktobar');
    expect(screen.getByRole('combobox', { name: 'Godina' })).toHaveTextContent('2026');
  });

  it('izbor meseca poziva onChange(godina, mesec)', async () => {
    const onChange = vi.fn();
    render(<MesecPicker year={2026} month={10} onChange={onChange} />);
    await userEvent.click(screen.getByRole('combobox', { name: 'Mesec' }));
    await userEvent.click(screen.getByRole('option', { name: 'Mart' }));
    expect(onChange).toHaveBeenCalledWith(2026, 3);
  });

  it('godinu van ponuđenog opsega ipak prikazuje (dodaje je u opcije)', () => {
    render(<MesecPicker year={2019} month={1} onChange={vi.fn()} yearOptions={[{ value: 2026, label: '2026' }]} />);
    expect(screen.getByRole('combobox', { name: 'Godina' })).toHaveTextContent('2019');
  });
});
