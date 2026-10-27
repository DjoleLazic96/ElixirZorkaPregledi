import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SortableTh } from './SortableTh';

function renderTh(current: 'sifra' | 'naziv' | undefined, direction: 'asc' | 'desc', onSort = vi.fn()) {
  render(
    <table>
      <thead>
        <tr>
          <SortableTh column="sifra" current={current} direction={direction} onSort={onSort}>
            Šifra
          </SortableTh>
        </tr>
      </thead>
    </table>,
  );
  return onSort;
}

describe('SortableTh', () => {
  it('aria-sort na <th> prati stanje sortiranja', () => {
    const { unmount } = render(
      <table>
        <thead>
          <tr>
            <SortableTh column="sifra" current="naziv" direction="asc" onSort={vi.fn()}>
              Šifra
            </SortableTh>
          </tr>
        </thead>
      </table>,
    );
    expect(screen.getByRole('columnheader')).toHaveAttribute('aria-sort', 'none');
    unmount();

    renderTh('sifra', 'desc');
    expect(screen.getByRole('columnheader')).toHaveAttribute('aria-sort', 'descending');
  });

  it('klik: neaktivna → asc, aktivna asc → desc, aktivna desc → asc', async () => {
    const onSort = renderTh(undefined, 'asc');
    await userEvent.click(screen.getByRole('button', { name: /Šifra/ }));
    expect(onSort).toHaveBeenLastCalledWith('sifra', 'asc');

    const onSort2 = renderTh('sifra', 'asc');
    await userEvent.click(screen.getAllByRole('button', { name: /Šifra/ })[1]);
    expect(onSort2).toHaveBeenLastCalledWith('sifra', 'desc');

    const onSort3 = renderTh('sifra', 'desc');
    await userEvent.click(screen.getAllByRole('button', { name: /Šifra/ })[2]);
    expect(onSort3).toHaveBeenLastCalledWith('sifra', 'asc');
  });
});
