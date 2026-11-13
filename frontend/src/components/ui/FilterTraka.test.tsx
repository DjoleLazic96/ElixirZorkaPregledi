import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FilterTraka } from './FilterTraka';
import { Textarea } from './Textarea';

describe('FilterTraka (zamerka 51)', () => {
  it('dugme „Filteri“ broji aktivne, × skida filter, „Obriši sve“ briše sve', async () => {
    const skiniStatus = vi.fn();
    const obrisiSve = vi.fn();
    render(
      <FilterTraka onObrisiSve={obrisiSve} aktivni={[
        { kljuc: 'status', labela: 'Otvorene', ukloni: skiniStatus },
        { kljuc: 'hitnost', labela: 'Hitno', ukloni: () => {} },
      ]}>
        <input aria-label="Polje" />
      </FilterTraka>,
    );
    expect(screen.getByRole('button', { name: /Filteri \(2\)/ })).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(screen.getAllByRole('button', { name: 'Ukloni filter Otvorene' })[0]);
    expect(skiniStatus).toHaveBeenCalled();
    await userEvent.click(screen.getAllByRole('button', { name: 'Obriši sve' })[0]);
    expect(obrisiSve).toHaveBeenCalled();
  });
});

describe('Textarea sa maxLength (zamerka 38)', () => {
  it('pri kraju pokazuje brojač', () => {
    render(<Textarea aria-label="Napomena" maxLength={10} value="123456789" onChange={() => {}} />);
    expect(screen.getByText('9 / 10')).toBeInTheDocument();
  });
});
