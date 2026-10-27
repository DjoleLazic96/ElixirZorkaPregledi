import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DiffPrikaz } from './DiffPrikaz';

describe('DiffPrikaz', () => {
  it('UPDATE prikazuje samo promenjena polja (staro → novo), sa srpskim labelama polja', () => {
    const stare = JSON.stringify({ Naziv: 'Stari', Sifra: 'K-1', Aktivan: true });
    const nove = JSON.stringify({ Naziv: 'Novi', Sifra: 'K-1', Aktivan: false });
    render(<DiffPrikaz akcija="UPDATE" stareVrednosti={stare} noveVrednosti={nove} />);

    expect(screen.getByText('Naziv')).toBeInTheDocument();
    expect(screen.getByText('Stari')).toBeInTheDocument();
    expect(screen.getByText('Novi')).toBeInTheDocument();
    expect(screen.getByText('Aktivan')).toBeInTheDocument();
    expect(screen.getByText('Da')).toBeInTheDocument();
    expect(screen.getByText('Ne')).toBeInTheDocument();
    // Nepromenjeno polje se ne prikazuje.
    expect(screen.queryByText('Šifra')).not.toBeInTheDocument();
  });

  it('CREATE prikazuje sve nove vrednosti', () => {
    const nove = JSON.stringify({ Sifra: 'K-7', Naziv: 'Nova kategorija', Opis: null });
    render(<DiffPrikaz akcija="CREATE" stareVrednosti={null} noveVrednosti={nove} />);
    expect(screen.getByText('Šifra')).toBeInTheDocument();
    expect(screen.getByText('K-7')).toBeInTheDocument();
    expect(screen.getByText('Nova kategorija')).toBeInTheDocument();
    expect(screen.getByText('(prazno)')).toBeInTheDocument();
  });

  it('DELETE prikazuje sve stare vrednosti', () => {
    const stare = JSON.stringify({ Naziv: 'Obrisana', Sifra: 'K-9' });
    render(<DiffPrikaz akcija="DELETE" stareVrednosti={stare} noveVrednosti={null} />);
    expect(screen.getByText('Obrisana')).toBeInTheDocument();
    expect(screen.getByText('K-9')).toBeInTheDocument();
  });

  it('auth događaji prikazuju samo opis, bez tabele', () => {
    const { rerender } = render(<DiffPrikaz akcija="LOGIN" stareVrednosti={null} noveVrednosti={null} />);
    expect(screen.getByText(/Uspešna prijava/)).toBeInTheDocument();
    rerender(<DiffPrikaz akcija="LOGIN_FAIL" stareVrednosti={null} noveVrednosti={null} />);
    expect(screen.getByText(/Neuspešna prijava/)).toBeInTheDocument();
    rerender(<DiffPrikaz akcija="LOGOUT" stareVrednosti={null} noveVrednosti={null} />);
    expect(screen.getByText(/Odjava/)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('START prikazuje opis i podatke o pokretanju (verzija, git SHA, okruženje)', () => {
    const nove = JSON.stringify({ verzija: '1.4.0', gitSha: 'ab12cd3', okruzenje: 'Production' });
    render(<DiffPrikaz akcija="START" stareVrednosti={null} noveVrednosti={nove} />);
    expect(screen.getByText(/Pokretanje aplikacije/)).toBeInTheDocument();
    expect(screen.getByText('Verzija')).toBeInTheDocument();
    expect(screen.getByText('1.4.0')).toBeInTheDocument();
    expect(screen.getByText('Git SHA')).toBeInTheDocument();
    expect(screen.getByText('Okruženje')).toBeInTheDocument();
  });

  it('UPDATE bez vidljivih izmena (samo zaštićena polja) prikazuje objašnjenje', () => {
    const isti = JSON.stringify({ Naziv: 'Isti' });
    render(<DiffPrikaz akcija="UPDATE" stareVrednosti={isti} noveVrednosti={isti} />);
    expect(screen.getByText('Nema prikazanih izmena (izmenjena su samo zaštićena polja).')).toBeInTheDocument();
    expect(screen.queryByText('Isti')).not.toBeInTheDocument();
  });

  it('nevalidan JSON ne obara prikaz', () => {
    render(<DiffPrikaz akcija="CREATE" stareVrednosti={null} noveVrednosti="{nije json" />);
    expect(screen.getByText('Nema podataka za prikaz.')).toBeInTheDocument();
  });
});
