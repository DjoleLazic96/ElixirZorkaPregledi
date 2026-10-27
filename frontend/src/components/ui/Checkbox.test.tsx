import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Checkbox } from './Checkbox';

describe('Checkbox', () => {
  it('labela je pristupačni naziv i klik na nju menja stanje', async () => {
    const onChange = vi.fn();
    render(<Checkbox label="Prikaži i neaktivne" description="Uključuje deaktivirane zapise." onChange={onChange} />);
    const box = screen.getByRole('checkbox', { name: /Prikaži i neaktivne/ });
    expect(box).not.toBeChecked();
    await userEvent.click(screen.getByText('Prikaži i neaktivne'));
    expect(onChange).toHaveBeenCalledOnce();
    expect(screen.getByText('Uključuje deaktivirane zapise.')).toBeInTheDocument();
  });
});
