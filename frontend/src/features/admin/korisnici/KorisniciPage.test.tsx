import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { api } from '../../../api/client';
import type { KorisnikDto } from '../../../api/types';
import { apiGreska, odgovor, renderSaProviderima } from '../../../test/utils';
import { KorisniciPage } from './KorisniciPage';

vi.mock('../../../api/client', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

const KORISNICI: KorisnikDto[] = [
  {
    id: 'u1',
    korisnickoIme: 'admin',
    ime: 'Ana Anić',
    email: 'ana@elixirgroup.rs',
    rola: 'Admin',
    aktivan: true,
    poslednjaPrijava: new Date(Date.now() - 5 * 60_000).toISOString(),
  },
  {
    id: 'u2',
    korisnickoIme: 'pera',
    ime: 'Pera Perić',
    email: null,
    rola: 'Korisnik',
    aktivan: false,
    poslednjaPrijava: null,
  },
];

describe('KorisniciPage', () => {
  beforeEach(() => {
    vi.mocked(api.get).mockReset();
    vi.mocked(api.post).mockReset();
  });

  it('prikazuje listu sa labelama rola, statusom i poslednjom prijavom', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor(KORISNICI));
    renderSaProviderima(<KorisniciPage />);

    expect(await screen.findByText('Ana Anić')).toBeInTheDocument();
    expect(screen.getByText('Administrator')).toBeInTheDocument();
    expect(screen.getByText('Korisnik')).toBeInTheDocument();
    expect(screen.getByText('Neaktivan')).toBeInTheDocument();
    expect(screen.getByText('pre 5 min')).toBeInTheDocument();
    expect(screen.getByText('nikad')).toBeInTheDocument();
    // Podrazumevano se traže samo aktivni.
    expect(api.get).toHaveBeenCalledWith('/korisnici', { params: { aktivan: true } });
  });

  it('„Prikaži i neaktivne" uklanja filter aktivan', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor(KORISNICI));
    renderSaProviderima(<KorisniciPage />);
    await screen.findByText('Ana Anić');
    await userEvent.click(screen.getByRole('checkbox', { name: 'Prikaži i neaktivne' }));
    expect(api.get).toHaveBeenLastCalledWith('/korisnici', { params: {} });
  });

  it('prazna lista → prazno stanje; greška → poruka sa „Pokušaj ponovo"', async () => {
    vi.mocked(api.get).mockResolvedValueOnce(odgovor([]));
    const prvi = renderSaProviderima(<KorisniciPage />);
    expect(await screen.findByText('Nema korisnika za zadate filtere.')).toBeInTheDocument();
    prvi.unmount();

    vi.mocked(api.get).mockRejectedValueOnce(new Error('mreža'));
    renderSaProviderima(<KorisniciPage />);
    expect(await screen.findByText('Učitavanje korisnika nije uspelo.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pokušaj ponovo' })).toBeInTheDocument();
  });

  it('„Novi korisnik" otvara formu, a uspešno kreiranje šalje POST i prikazuje toast', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor(KORISNICI));
    vi.mocked(api.post).mockResolvedValue(odgovor(KORISNICI[0]));
    renderSaProviderima(<KorisniciPage />);
    await screen.findByText('Ana Anić');

    await userEvent.click(screen.getByRole('button', { name: 'Novi korisnik' }));
    const dijalog = screen.getByRole('dialog', { name: 'Novi korisnik' });
    await userEvent.type(within(dijalog).getByLabelText('Korisničko ime'), 'mika');
    await userEvent.type(within(dijalog).getByLabelText('Ime i prezime'), 'Mika Mikić');
    await userEvent.type(within(dijalog).getByLabelText('Lozinka'), 'Lozinka12');
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Kreiraj' }));

    expect(api.post).toHaveBeenCalledWith('/korisnici', {
      korisnickoIme: 'mika',
      ime: 'Mika Mikić',
      email: null,
      lozinka: 'Lozinka12',
      rola: 'Korisnik',
    });
    expect(await screen.findByText('Korisnik je kreiran.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'Novi korisnik' })).not.toBeInTheDocument();
  });

  it('422 iz guarda ostaje u formi kao poruka', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor(KORISNICI));
    vi.mocked(api.put).mockRejectedValue(apiGreska(422, { greske: ['Ne možete promeniti sopstvenu rolu.'] }));
    renderSaProviderima(<KorisniciPage />);
    await screen.findByText('Ana Anić');

    await userEvent.click(screen.getAllByRole('button', { name: 'Izmeni' })[0]);
    const dijalog = screen.getByRole('dialog', { name: 'Izmena korisnika' });
    await userEvent.click(within(dijalog).getByRole('radio', { name: /^Korisnik/ }));
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Sačuvaj' }));

    expect(await within(dijalog).findByText('Ne možete promeniti sopstvenu rolu.')).toBeInTheDocument();
  });

  it('deaktivacija traži potvrdu i tek onda šalje POST /deaktiviraj', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor(KORISNICI));
    vi.mocked(api.post).mockResolvedValue(odgovor({ ...KORISNICI[0], aktivan: false }));
    renderSaProviderima(<KorisniciPage />);
    await screen.findByText('Ana Anić');

    await userEvent.click(screen.getByRole('button', { name: 'Deaktiviraj' }));
    expect(api.post).not.toHaveBeenCalled();
    const dijalog = screen.getByRole('dialog', { name: 'Deaktivacija korisnika' });
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Deaktiviraj' }));

    expect(api.post).toHaveBeenCalledWith('/korisnici/u1/deaktiviraj');
    expect(await screen.findByText('Korisnik je deaktiviran.')).toBeInTheDocument();
  });

  it('reset lozinke otvara modal i šalje novu lozinku', async () => {
    vi.mocked(api.get).mockResolvedValue(odgovor(KORISNICI));
    vi.mocked(api.post).mockResolvedValue(odgovor(undefined));
    renderSaProviderima(<KorisniciPage />);
    await screen.findByText('Ana Anić');

    await userEvent.click(screen.getByRole('button', { name: 'Reset lozinke — pera' }));
    const dijalog = screen.getByRole('dialog', { name: 'Reset lozinke — pera' });
    await userEvent.type(within(dijalog).getByLabelText('Nova lozinka'), 'NovaLozinka1');
    await userEvent.click(within(dijalog).getByRole('button', { name: 'Resetuj lozinku' }));

    expect(api.post).toHaveBeenCalledWith('/korisnici/u2/reset-lozinka', { novaLozinka: 'NovaLozinka1' });
    expect(await screen.findByText('Lozinka je resetovana.')).toBeInTheDocument();
  });
});
