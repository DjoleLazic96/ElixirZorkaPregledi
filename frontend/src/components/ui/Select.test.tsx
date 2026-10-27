import { useState } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Select, type SelectOption } from './Select';

/**
 * Kontrolisani harness — Select je controlled komponenta (`value`/`onChange`),
 * pa test-u treba lokalni state da bi selekcija bila vidljiva u UI-u. `onChange`
 * spy proverava sa kojom vrednošću je pozvan.
 */
function Harness<T extends string | number>({
  options,
  initial = null,
  nullable = false,
  disabled = false,
  onChange,
}: {
  options: ReadonlyArray<SelectOption<T>>;
  initial?: T | null;
  nullable?: boolean;
  disabled?: boolean;
  onChange?: (v: T | null) => void;
}) {
  const [value, setValue] = useState<T | null>(initial);
  return (
    <Select
      aria-label="Test select"
      value={value}
      options={options}
      nullable={nullable}
      disabled={disabled}
      onChange={(v) => {
        setValue(v);
        onChange?.(v);
      }}
    />
  );
}

const voce: SelectOption<string>[] = [
  { value: 'a', label: 'Jabuka' },
  { value: 'b', label: 'Kruška' },
  { value: 'c', label: 'Šljiva' },
];

// Duga lista (>= 8) da se pojavi polje za pretragu.
const dugacka: SelectOption<number>[] = Array.from({ length: 12 }, (_, i) => ({
  value: i + 1,
  label: `Opcija ${i + 1}`,
}));

describe('Select', () => {
  it('trigger ima combobox semantiku i aria-expanded se menja', async () => {
    render(<Harness options={voce} />);
    const trigger = screen.getByRole('combobox', { name: 'Test select' });
    expect(trigger).toHaveAttribute('aria-haspopup', 'listbox');
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(trigger);
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
  });

  it('otvara se klikom i bira opciju (onChange dobija value)', async () => {
    const onChange = vi.fn();
    render(<Harness options={voce} onChange={onChange} />);
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(screen.getByRole('option', { name: 'Kruška' }));
    expect(onChange).toHaveBeenCalledWith('b');
    // Panel se zatvara posle izbora.
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('otvara se sa ArrowDown i bira sa Enter', async () => {
    const onChange = vi.fn();
    render(<Harness options={voce} onChange={onChange} />);
    const trigger = screen.getByRole('combobox');
    trigger.focus();
    await userEvent.keyboard('{ArrowDown}'); // otvori + highlight prva
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await userEvent.keyboard('{ArrowDown}'); // pomeri na drugu (Kruška)
    await userEvent.keyboard('{Enter}');
    expect(onChange).toHaveBeenCalledWith('b');
  });

  it('Esc zatvara i vraća fokus na trigger', async () => {
    render(<Harness options={voce} />);
    const trigger = screen.getByRole('combobox');
    await userEvent.click(trigger);
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });

  it('type-ahead: kucanje slova highlight-uje odgovarajuću opciju (aria-activedescendant)', async () => {
    render(<Harness options={voce} />);
    const trigger = screen.getByRole('combobox');
    await userEvent.click(trigger);
    // „k" -> Kruška.
    await userEvent.keyboard('k');
    const kruska = screen.getByRole('option', { name: 'Kruška' });
    expect(trigger).toHaveAttribute('aria-activedescendant', kruska.id);
  });

  it('type-ahead je dijakritik-neosetljiv: „s" skače na „Šljiva"', async () => {
    render(<Harness options={voce} />);
    const trigger = screen.getByRole('combobox');
    await userEvent.click(trigger);
    // Kucano ASCII „s" pogađa „Šljiva" (fold uklanja dijakritiku).
    await userEvent.keyboard('s');
    const sljiva = screen.getByRole('option', { name: 'Šljiva' });
    expect(trigger).toHaveAttribute('aria-activedescendant', sljiva.id);
  });

  it('polje za pretragu se NE prikazuje ispod praga', async () => {
    render(<Harness options={voce} />);
    await userEvent.click(screen.getByRole('combobox'));
    expect(screen.queryByPlaceholderText('Pretraži…')).not.toBeInTheDocument();
  });

  it('polje za pretragu se prikazuje za dugu listu i filtrira (dijakritik-neosetljivo)', async () => {
    render(<Harness options={dugacka} />);
    await userEvent.click(screen.getByRole('combobox'));
    const search = screen.getByPlaceholderText('Pretraži…');
    expect(search).toBeInTheDocument();
    // Sve opcije prisutne pre filtriranja.
    expect(screen.getAllByRole('option')).toHaveLength(12);
    await userEvent.type(search, '11');
    const listbox = screen.getByRole('listbox');
    expect(within(listbox).getAllByRole('option')).toHaveLength(1);
    expect(within(listbox).getByRole('option', { name: 'Opcija 11' })).toBeInTheDocument();
  });

  it('pretraga bez rezultata prikazuje „Nema rezultata"', async () => {
    render(<Harness options={dugacka} />);
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.type(screen.getByPlaceholderText('Pretraži…'), 'zzz');
    expect(screen.getByText('Nema rezultata')).toBeInTheDocument();
    expect(screen.queryAllByRole('option')).toHaveLength(0);
  });

  it('nullable nudi placeholder stavku koja postavlja null', async () => {
    const onChange = vi.fn();
    render(<Harness options={voce} initial="a" nullable onChange={onChange} />);
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(screen.getByRole('option', { name: '— odaberite —' }));
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it('disabled se ne otvara', async () => {
    render(<Harness options={voce} disabled />);
    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeDisabled();
    await userEvent.click(trigger);
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('izabrana opcija ima aria-selected=true', async () => {
    render(<Harness options={voce} initial="c" />);
    await userEvent.click(screen.getByRole('combobox'));
    const sljiva = screen.getByRole('option', { name: 'Šljiva' });
    expect(sljiva).toHaveAttribute('aria-selected', 'true');
    const jabuka = screen.getByRole('option', { name: 'Jabuka' });
    expect(jabuka).toHaveAttribute('aria-selected', 'false');
  });

  it('Tab/gubitak fokusa van widgeta zatvara panel (focus-loss)', async () => {
    render(
      <>
        <Harness options={voce} />
        <button type="button">Spolja</button>
      </>
    );
    const trigger = screen.getByRole('combobox');
    await userEvent.click(trigger);
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    // Fokus odlazi na spoljno dugme (simulira Tab-away) → panel se zatvara,
    // BEZ vraćanja fokusa na trigger (korisnik svesno pomera fokus dalje).
    const spolja = screen.getByRole('button', { name: 'Spolja' });
    act(() => {
      spolja.focus();
      fireEvent.focusIn(spolja);
    });
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    expect(spolja).toHaveFocus();
  });

  it('fokus na polje za pretragu (unutar panela) NE zatvara panel', async () => {
    render(<Harness options={dugacka} />);
    const trigger = screen.getByRole('combobox');
    await userEvent.click(trigger);
    const search = screen.getByPlaceholderText('Pretraži…');
    // Fokus je već u search polju (unutar portala/panela); kucanje ne zatvara.
    await userEvent.type(search, '1');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
  });

  it('type-ahead otvara zatvoreni trigger i skače na poklapanje', async () => {
    render(<Harness options={voce} />);
    const trigger = screen.getByRole('combobox');
    trigger.focus();
    // Zatvoreno: štampani znak „k" otvara panel i highlight-uje „Kruška".
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    await userEvent.keyboard('k');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    const kruska = screen.getByRole('option', { name: 'Kruška' });
    // Skok se primenjuje kroz requestAnimationFrame (posle effect-a na otvaranju).
    await waitFor(() =>
      expect(trigger).toHaveAttribute('aria-activedescendant', kruska.id)
    );
  });

  it('štampani znak na zatvorenom trigger-u sa dugom listom samo otvara (fokus u pretragu)', async () => {
    render(<Harness options={dugacka} />);
    const trigger = screen.getByRole('combobox');
    trigger.focus();
    await userEvent.keyboard('x');
    // Otvoreno; za search-varijantu je otvaranje dovoljno (bez type-ahead skoka).
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Pretraži…')).toBeInTheDocument();
  });

  it('nullable: placeholder stavka nestaje iz liste dok traje pretraga', async () => {
    // Duga lista + nullable → ima i pretragu i placeholder pseudo-opciju.
    render(<Harness options={dugacka} nullable />);
    await userEvent.click(screen.getByRole('combobox'));
    // Bez upita: placeholder je prisutan.
    expect(screen.getByRole('option', { name: '— odaberite —' })).toBeInTheDocument();
    // Sa upitom: placeholder se sakriva.
    await userEvent.type(screen.getByPlaceholderText('Pretraži…'), 'Opcija 3');
    expect(screen.queryByRole('option', { name: '— odaberite —' })).not.toBeInTheDocument();
  });
});
