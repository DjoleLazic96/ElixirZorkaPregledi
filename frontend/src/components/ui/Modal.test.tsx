import { describe, it, expect, vi } from 'vitest';
import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Modal } from './Modal';

/**
 * Modal je zajednički za sve dijaloge u app-u (šifarnici, korisnici, dnevnik,
 * ConfirmDialog), pa se ovde čuva njegovo ponašanje kao pravog dijaloga —
 * ARIA, zamka fokusa i povratak fokusa. Ako neko sutra vrati „običan div",
 * ovi testovi padaju.
 */
function Fixture({ onClose = vi.fn() }: { onClose?: () => void }) {
  return (
    <Modal open title="Naslov dijaloga" onClose={onClose}>
      <input aria-label="Prvo polje" />
      <input aria-label="Drugo polje" />
      <button type="button">Sačuvaj</button>
    </Modal>
  );
}

describe('Modal', () => {
  it('izložen je kao dijalog i povezan sa svojim naslovom', () => {
    render(<Fixture />);

    const dijalog = screen.getByRole('dialog');
    expect(dijalog).toHaveAttribute('aria-modal', 'true');
    // aria-labelledby mora da pokazuje na element koji stvarno nosi naslov.
    const naslovId = dijalog.getAttribute('aria-labelledby');
    expect(naslovId).toBeTruthy();
    expect(document.getElementById(naslovId!)).toHaveTextContent('Naslov dijaloga');
  });

  it('renderuje se kroz portal na body (ne unutar roditeljskog čvora)', () => {
    const { container } = render(<Fixture />);
    // Roditeljski kontejner ostaje prazan — sadržaj je u portalu.
    expect(container).toBeEmptyDOMElement();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('fokus pri otvaranju ide na prvo polje u telu, ne na „Zatvori"', () => {
    render(<Fixture />);
    expect(screen.getByLabelText('Prvo polje')).toHaveFocus();
  });

  it('Esc poziva onClose', async () => {
    const onClose = vi.fn();
    render(<Fixture onClose={onClose} />);

    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalled();
  });

  it('klik na pozadinu zatvara, klik unutar panela ne', async () => {
    const onClose = vi.fn();
    render(<Fixture onClose={onClose} />);

    await userEvent.click(screen.getByLabelText('Prvo polje'));
    expect(onClose).not.toHaveBeenCalled();

    // Pozadina je roditelj panela (overlay).
    await userEvent.click(screen.getByRole('dialog').parentElement!);
    expect(onClose).toHaveBeenCalled();
  });

  it('Tab sa poslednjeg elementa vraća fokus na prvi (zamka fokusa)', async () => {
    render(<Fixture />);

    const sacuvaj = screen.getByRole('button', { name: 'Sačuvaj' });
    sacuvaj.focus();
    await userEvent.tab();

    // Bez zamke bi fokus odlutao na stranicu iza dijaloga.
    expect(screen.getByLabelText('Zatvori')).toHaveFocus();
  });

  it('Shift+Tab sa prvog elementa ide na poslednji', async () => {
    render(<Fixture />);

    screen.getByLabelText('Zatvori').focus();
    await userEvent.tab({ shift: true });

    expect(screen.getByRole('button', { name: 'Sačuvaj' })).toHaveFocus();
  });

  it('vraća fokus na element koji ga je otvorio', async () => {
    function Host() {
      const [open, setOpen] = useState(false);
      return (
        <>
          <button type="button" onClick={() => setOpen(true)}>
            Otvori
          </button>
          <Modal open={open} title="Naslov" onClose={() => setOpen(false)}>
            <input aria-label="Polje" />
          </Modal>
        </>
      );
    }

    render(<Host />);
    const otvori = screen.getByRole('button', { name: 'Otvori' });
    await userEvent.click(otvori);
    expect(screen.getByLabelText('Polje')).toHaveFocus();

    await userEvent.keyboard('{Escape}');
    expect(otvori).toHaveFocus();
  });

  it('zaključava skrol pozadine dok je otvoren', () => {
    const { unmount } = render(<Fixture />);
    expect(document.body.style.overflow).toBe('hidden');

    unmount();
    expect(document.body.style.overflow).not.toBe('hidden');
  });
});
