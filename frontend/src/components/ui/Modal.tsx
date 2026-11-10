import type { ReactNode } from 'react';
import { useEffect, useId, useRef } from 'react';
import { createPortal } from 'react-dom';
import { cn } from '../../lib/cn';
import { X } from 'lucide-react';

interface ModalProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  /** Sirina dijaloga (Tailwind klasa). Default: max-w-md. */
  widthClass?: string;
}

/**
 * CSS selektor elemenata koji mogu da prime fokus. Namerno bez provere
 * vidljivosti (`offsetParent`) — u jsdom-u je ona uvek `null`, pa bi testovi
 * videli prazan skup i zamka fokusa bi izgledala kao da ne radi.
 */
const FOKUSABILNI =
  'a[href], button:not([disabled]), textarea:not([disabled]), ' +
  'input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Lagan modal bez Radix-a/Headless UI — kratko ime u stack-u, lako se
 * zamenjuje shadcn-ovim Dialog-om kad bude trebalo.
 *
 * Renderuje se kroz portal na `document.body` da ga `overflow`/`z-index`
 * roditeljske kartice ne bi odsekli, i ponaša se kao pravi dijalog: Esc i klik
 * van zatvaraju, fokus ulazi u dijalog i ostaje zarobljen u njemu dok je otvoren,
 * a po zatvaranju se vraća na element koji ga je otvorio (npr. dugme „Izmeni").
 */
export function Modal({ open, title, onClose, children, widthClass = 'max-w-md' }: ModalProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const teloRef = useRef<HTMLDivElement>(null);
  const naslovId = useId();

  // Početni fokus + povratak fokusa po zatvaranju. Fokus ciljamo na prvo polje
  // U TELU dijaloga, ne na prvi fokusabilan element uopšte — inače bi ga uvek
  // pokupilo dugme „Zatvori" iz zaglavlja, pa bi korisnik morao da tabuje do forme.
  useEffect(() => {
    if (!open) return;

    const prethodniFokus = document.activeElement as HTMLElement | null;
    const prvoPolje = teloRef.current?.querySelector<HTMLElement>(FOKUSABILNI);
    (prvoPolje ?? panelRef.current)?.focus();

    return () => prethodniFokus?.focus?.();
  }, [open]);

  // Pozadina ne sme da skroluje dok je dijalog otvoren.
  useEffect(() => {
    if (!open) return;

    const prethodni = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      document.body.style.overflow = prethodni;
    };
  }, [open]);

  // Esc zatvara; Tab kruži unutar dijaloga (zamka fokusa).
  useEffect(() => {
    if (!open) return;

    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        onClose();
        return;
      }
      if (e.key !== 'Tab') return;

      const panel = panelRef.current;
      if (!panel) return;

      const fokusabilni = Array.from(panel.querySelectorAll<HTMLElement>(FOKUSABILNI));
      if (fokusabilni.length === 0) {
        e.preventDefault();
        return;
      }

      const prvi = fokusabilni[0];
      const poslednji = fokusabilni[fokusabilni.length - 1];
      const aktivni = document.activeElement;

      // Fokus van panela (npr. posle klika na pozadinu) tretiramo kao „pre prvog",
      // pa Tab vraća u dijalog umesto da odluta na stranicu iza.
      if (e.shiftKey && (aktivni === prvi || !panel.contains(aktivni))) {
        e.preventDefault();
        poslednji.focus();
      } else if (!e.shiftKey && (aktivni === poslednji || !panel.contains(aktivni))) {
        e.preventDefault();
        prvi.focus();
      }
    }

    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-[rgba(20,20,20,0.45)] p-4"
      onClick={onClose}
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={naslovId}
        // tabIndex da panel može da primi fokus kad u telu nema nijednog polja.
        tabIndex={-1}
        className={cn('flex max-h-[88vh] w-full flex-col overflow-hidden rounded-xl bg-white shadow-xl outline-none', widthClass)}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3 px-[18px] pt-[18px]">
          <h2 id={naslovId} className="min-w-0 break-words pt-0.5 text-lg font-semibold text-gray-800">
            {title}
          </h2>
          <button
            type="button"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded text-gray-500 hover:bg-gray-100 md:h-auto md:w-auto md:p-1"
            onClick={onClose}
            aria-label="Zatvori"
          >
            <X size={18} />
          </button>
        </div>
        <div ref={teloRef} className="overflow-y-auto px-[18px] pb-[18px] pt-2.5">
          {children}
        </div>
      </div>
    </div>,
    document.body,
  );
}
