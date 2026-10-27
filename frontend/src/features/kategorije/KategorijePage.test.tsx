import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { api } from '../../api/client';
import type { KategorijaDto, StranicaDto } from '../../api/types';
import { apiGreska, odgovor, renderSaProviderima } from '../../test/utils';
import { KategorijePage } from './KategorijePage';

vi.mock('../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

let roles: string[] = ['Admin'];
vi.mock('../auth/AuthProvider', () => ({
  useAuth: () => ({
    me: { user: { id: 'a1', userName: 'admin', fullName: 'Admin', email: null }, roles },
    initializing: false,
    sesijaIstekla: false,
    login: vi.fn(),
    logout: vi.fn(),
  }),
}));

const STRANICA: StranicaDto<KategorijaDto> = {
  stranica: 1,
  velicina: 50,
  ukupno: 2,
  stavke: [
    { id: 1, sifra: 'KAT-01', naziv: 'Sirovine', opis: 'Ulazne sirovine', aktivan: true, createdAt: '', updatedAt: null },
    { id: 2, sifra: 'KAT-02', naziv: 'Ambalaža', opis: null, aktivan: false, createdAt: '', updatedAt: null },
  ],
};

type Params = Record<string, string | number | boolean>;
function poslednjiParams(): Params {
  const poziv = vi.mocked(api.get).mock.calls.at(-1)!;
  return (poziv[1] as { params: Params }).params;
}

describe('KategorijePage — stanja ekrana', () => {
  beforeEach(() => {
    roles = ['Admin'];
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
  });

  it('učitavanje: indikator dok odgovor ne stigne', () => {
    vi.mocked(api.get).mockReturnValue(new Promise(() => {}));
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    expect(screen.getByRole('status')).toHaveTextContent('Učitavanje…');
  });

  it('greška: inline poruka sa „Pokušaj ponovo" koje ponavlja upit', async () => {
    vi.mocked(api.get).mockRejectedValueOnce(apiGreska(500)).mockResolvedValueOnce(odgovor(STRANICA));
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    expect(await screen.findByText('Učitavanje kategorija nije uspelo.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Pokušaj ponovo' }));
    expect(await screen.findByText('Sirovine')).toBeInTheDocument();
  });

  it('prazno bez filtera: „Još nema kategorija." sa akcijom za Admina', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor({ ...STRANICA, stavke: [], ukupno: 0 }));
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    expect(await screen.findByText('Još nema kategorija.')).toBeInTheDocument();
    expect(screen.getByText('Dodajte prvu kategoriju.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Nova kategorija' })).toHaveLength(2);
  });

  it('prazno sa filterima: „Nema kategorija za zadate filtere."', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor({ ...STRANICA, stavke: [], ukupno: 0 }));
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije?pretraga=xyz' });
    expect(await screen.findByText('Nema kategorija za zadate filtere.')).toBeInTheDocument();
    expect(screen.queryByText('Dodajte prvu kategoriju.')).not.toBeInTheDocument();
  });

  it('podaci: tabela sa bedžom „Neaktivna" na neaktivnom redu i paginacijom', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor(STRANICA));
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    expect(await screen.findByText('Sirovine')).toBeInTheDocument();
    const red = screen.getByText('Ambalaža').closest('tr')!;
    expect(within(red).getByText('Neaktivna')).toBeInTheDocument();
    expect(screen.getByText(/Prikaz/).textContent).toBe('Prikaz 1–2 od 2 kategorije');
  });
});

describe('KategorijePage — filteri u URL-u', () => {
  beforeEach(() => {
    roles = ['Admin'];
    vi.mocked(api.get).mockReset();
    vi.mocked(api.get).mockResolvedValue(odgovor(STRANICA));
  });

  it('podrazumevano traži samo aktivne, sortirano po šifri rastuće', async () => {
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    await screen.findByText('Sirovine');
    expect(poslednjiParams()).toEqual({ stranica: 1, velicina: 50, sort: 'sifra', smer: 'asc', aktivan: true });
  });

  it('pretraga resetuje stranicu na 1', async () => {
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije?stranica=3' });
    await screen.findByText('Sirovine');
    expect(poslednjiParams().stranica).toBe(3);

    await userEvent.type(screen.getByRole('textbox', { name: 'Pretraga' }), 'a');
    expect(poslednjiParams()).toMatchObject({ pretraga: 'a', stranica: 1 });
  });

  it('„Prikaži i neaktivne" uklanja filter aktivan', async () => {
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    await screen.findByText('Sirovine');
    await userEvent.click(screen.getByRole('checkbox', { name: 'Prikaži i neaktivne' }));
    expect(poslednjiParams()).not.toHaveProperty('aktivan');
  });

  it('klik na zaglavlje „Naziv" sortira po nazivu, drugi klik menja smer', async () => {
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    await screen.findByText('Sirovine');
    await userEvent.click(screen.getByRole('button', { name: /Naziv/ }));
    expect(poslednjiParams()).toMatchObject({ sort: 'naziv', smer: 'asc' });
    await userEvent.click(screen.getByRole('button', { name: /Naziv/ }));
    expect(poslednjiParams()).toMatchObject({ sort: 'naziv', smer: 'desc' });
    expect(screen.getByRole('columnheader', { name: /Naziv/ })).toHaveAttribute('aria-sort', 'descending');
  });
});

describe('KategorijePage — prava i akcije', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
    vi.mocked(api.get).mockResolvedValue(odgovor(STRANICA));
  });

  it('Korisnik ne vidi dugme „Nova kategorija" ni kolonu sa akcijama', async () => {
    roles = ['Korisnik'];
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    await screen.findByText('Sirovine');
    expect(screen.queryByRole('button', { name: 'Nova kategorija' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Izmeni' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Akcije' })).not.toBeInTheDocument();
  });

  it('Admin: kreiranje šalje POST, zatvara formu i prikazuje toast', async () => {
    roles = ['Admin'];
    vi.mocked(api.post).mockResolvedValue(odgovor(STRANICA.stavke[0]));
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    await screen.findByText('Sirovine');

    await userEvent.click(screen.getByRole('button', { name: 'Nova kategorija' }));
    const dijalog = screen.getByRole('dialog', { name: 'Nova kategorija' });
    await userEvent.type(within(dijalog).getByLabelText('Šifra'), 'KAT-03');
    await userEvent.type(within(dijalog).getByLabelText('Naziv'), 'Gotovi proizvodi');
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Kreiraj' }));

    expect(api.post).toHaveBeenCalledWith('/kategorije', { sifra: 'KAT-03', naziv: 'Gotovi proizvodi', opis: null });
    expect(await screen.findByText('Kategorija je kreirana.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'Nova kategorija' })).not.toBeInTheDocument();
  });

  it('duplikat šifre (422) ostaje u formi kao poruka', async () => {
    roles = ['Admin'];
    vi.mocked(api.post).mockRejectedValue(apiGreska(422, { greske: ['Kategorija sa ovom šifrom već postoji.'] }));
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    await screen.findByText('Sirovine');

    await userEvent.click(screen.getByRole('button', { name: 'Nova kategorija' }));
    const dijalog = screen.getByRole('dialog', { name: 'Nova kategorija' });
    await userEvent.type(within(dijalog).getByLabelText('Šifra'), 'KAT-01');
    await userEvent.type(within(dijalog).getByLabelText('Naziv'), 'Duplikat');
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Kreiraj' }));

    expect(await within(dijalog).findByText('Kategorija sa ovom šifrom već postoji.')).toBeInTheDocument();
  });

  it('deaktivacija traži potvrdu, pa šalje POST /deaktiviraj i prikazuje toast', async () => {
    roles = ['Admin'];
    vi.mocked(api.post).mockResolvedValue(odgovor({ ...STRANICA.stavke[0], aktivan: false }));
    renderSaProviderima(<KategorijePage />, { ruta: '/kategorije' });
    await screen.findByText('Sirovine');

    await userEvent.click(screen.getByRole('button', { name: 'Deaktiviraj' }));
    expect(api.post).not.toHaveBeenCalled();
    const dijalog = screen.getByRole('dialog', { name: 'Deaktivacija kategorije' });
    expect(dijalog).toHaveTextContent('Sirovine');
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Deaktiviraj' }));

    expect(api.post).toHaveBeenCalledWith('/kategorije/1/deaktiviraj');
    expect(await screen.findByText('Kategorija je deaktivirana.')).toBeInTheDocument();
  });
});
