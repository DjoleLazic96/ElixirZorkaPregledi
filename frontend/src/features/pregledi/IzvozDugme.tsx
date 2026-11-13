import { ChevronDown, FileText } from 'lucide-react';
import { useState } from 'react';
import { AnchoredDropdown } from '../../components/ui/AnchoredDropdown';
import { Button } from '../../components/ui/Button';

/**
 * PDF / Word sa izborom „Rezime“ ili „Ceo pregled“ (zamerka 62): klik otvara meni, izbor preuzima fajl. Korisnik uvek bira sam,
 * nezavisno od kartice otvorene na ekranu.
 */
export function IzvozDugme({ oznaka, disabled, onIzaberi }: {
  oznaka: string;
  disabled?: boolean;
  onIzaberi: (prikaz: 'rezime' | 'ceo') => void;
}) {
  const [anchor, setAnchor] = useState<HTMLSpanElement | null>(null);
  const [otvoreno, setOtvoreno] = useState(false);
  const izaberi = (prikaz: 'rezime' | 'ceo') => {
    setOtvoreno(false);
    onIzaberi(prikaz);
  };
  return (
    <>
      <span ref={setAnchor} className="inline-flex">
        <Button variant="secondary" disabled={disabled} aria-haspopup="menu" aria-expanded={otvoreno} onClick={() => setOtvoreno((o) => !o)}>
          <FileText size={14} aria-hidden /> {oznaka} <ChevronDown size={14} aria-hidden />
        </Button>
      </span>
      <AnchoredDropdown anchor={anchor} open={otvoreno} onRequestClose={() => setOtvoreno(false)} widthMode="min-anchor"
        className="rounded-[10px] border border-gray-200 bg-white py-1 shadow-[0_6px_20px_rgba(0,0,0,0.15)]">
        <div role="menu" aria-label={`${oznaka} — izbor`} className="text-sm">
          <button type="button" role="menuitem" className="block min-h-11 w-full px-3.5 py-2 text-left hover:bg-gray-50 md:min-h-0" onClick={() => izaberi('rezime')}>Rezime</button>
          <button type="button" role="menuitem" className="block min-h-11 w-full px-3.5 py-2 text-left hover:bg-gray-50 md:min-h-0" onClick={() => izaberi('ceo')}>Ceo pregled</button>
        </div>
      </AnchoredDropdown>
    </>
  );
}
