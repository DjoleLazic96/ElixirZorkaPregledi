import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EmptyState } from './EmptyState';
import { Button } from './Button';

describe('EmptyState', () => {
  it('prikazuje naslov, opis i akciju', () => {
    render(
      <EmptyState
        title="Još nema kategorija."
        description="Dodajte prvu kategoriju."
        action={<Button>Nova kategorija</Button>}
      />,
    );
    expect(screen.getByText('Još nema kategorija.')).toBeInTheDocument();
    expect(screen.getByText('Dodajte prvu kategoriju.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Nova kategorija' })).toBeInTheDocument();
  });

  it('radi i samo sa naslovom', () => {
    render(<EmptyState title="Nema zapisa za zadate filtere." />);
    expect(screen.getByText('Nema zapisa za zadate filtere.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
