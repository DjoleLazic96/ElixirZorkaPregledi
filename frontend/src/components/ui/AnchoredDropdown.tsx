import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type CSSProperties,
  type ReactNode,
} from 'react';
import { createPortal } from 'react-dom';

interface AnchoredDropdownProps {
  /**
   * Input/element ispod kojeg se otvara dropdown. Pozicija se računa iz
   * njegovog <c>getBoundingClientRect()</c>-a.
   */
  anchor: HTMLElement | null;
  open: boolean;
  /**
   * Klik van dropdown-a (ili anchor-a) zatvara dropdown. Roditelj odlučuje
   * da li onChange takođe zatvara.
   */
  onRequestClose: () => void;
  children: ReactNode;
  /** Tailwind klase za sam dropdown (npr. boja, max-height). Default: bela kartica. */
  className?: string;
  /**
   * Kako se određuje širina dropdown-a:
   *
   * <c>'anchor'</c> (default): širina je tačno koliko i anchor — pogodno
   *   za select/autocomplete liste gde se vizuelno nadovezuju na input.
   *
   * <c>'auto'</c>: širinu određuje sam content/className (npr. <c>w-80</c>);
   *   pogodno za kalendare/menije koji moraju da budu širi od input-a koji
   *   ih otvara.
   *
   * <c>'min-anchor'</c>: minimalna širina = anchor, ali može da bude šira
   *   ako sadržaj zahteva (kompromis između prethodne dve).
   */
  widthMode?: 'anchor' | 'auto' | 'min-anchor';
}

/** Razmak (px) izmedju popup-a i ivica viewport-a — da popup ne lepi za rub ekrana. */
const VIEWPORT_MARGIN = 8;
/** Razmak (px) izmedju popup-a i anchor-a (gore/dole). */
const ANCHOR_GAP = 4;

/**
 * Dropdown koji se renderuje u <c>document.body</c> preko React portal-a.
 *
 * <para><b>Klipovanje:</b> pošto se dropdown ne nalazi u DOM podstablu
 * roditelja sa <c>overflow:auto/hidden</c>, ne može biti isečen.</para>
 *
 * <para><b>Flip / clamping:</b> ako bi popup izašao van vidljivog prozora,
 * automatski se pomera:</para>
 * <list type="bullet">
 *   <item>Desno prekoračenje → poravna se desna ivica popup-a sa desnim rubom anchor-a (ili viewport-a)</item>
 *   <item>Donje prekoračenje → flipuje se iznad anchor-a (ako gore ima prostora)</item>
 * </list>
 *
 * <para>Pozicija se osvežava na scroll/resize, tako da se ponašanje održava
 * čak i kad korisnik skroluje stranu dok je dropdown otvoren.</para>
 */
export function AnchoredDropdown({
  anchor,
  open,
  onRequestClose,
  children,
  className,
  widthMode = 'anchor',
}: AnchoredDropdownProps) {
  // Pozicija + flag "ready" — pre measurement-a popup je nevidljiv (opacity:0)
  // da korisnik ne vidi flash neusagljašene pozicije.
  const [layout, setLayout] = useState<{
    top: number;
    left: number;
    anchorWidth: number;
    ready: boolean;
  } | null>(null);

  // Ref ka samom popup elementu — koristimo callback ref da ulovimo
  // mount i tada izmerimo i pozicioniramo.
  const popupNodeRef = useRef<HTMLDivElement | null>(null);

  // Funkcija koja računa pravu poziciju (sa flip-om i clamping-om).
  // Razdvojena izvan effect-a jer je zovemo i iz callback ref-a.
  function recalc() {
    if (!anchor) return;

    const r = anchor.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    // Default pozicija: ispod anchor-a, levo poravnato.
    let top = r.bottom + ANCHOR_GAP;
    let left = r.left;

    const popup = popupNodeRef.current;
    if (popup) {
      // Stvarne dimenzije izmerene posle render-a; koristimo ih za clamping/flip.
      const popupRect = popup.getBoundingClientRect();
      const popupW = popupRect.width;
      const popupH = popupRect.height;

      // ─── Horizontalno: ako popup izlazi desno, poravnaj desnu ivicu sa anchor-om
      if (left + popupW > vw - VIEWPORT_MARGIN) {
        // Probaj prvo desnu ivicu poravnati sa anchor.right — vizuelno deluje povezano.
        left = r.right - popupW;
        // Ako i to izlazi (anchor je preuzak ili daleko levo), klampuj na ivicu viewport-a.
        if (left + popupW > vw - VIEWPORT_MARGIN) {
          left = vw - popupW - VIEWPORT_MARGIN;
        }
      }
      // Levo prekoračenje — clamp na ivicu
      if (left < VIEWPORT_MARGIN) left = VIEWPORT_MARGIN;

      // ─── Vertikalno: ako popup izlazi dole, pokušaj iznad anchor-a
      if (top + popupH > vh - VIEWPORT_MARGIN) {
        const aboveTop = r.top - popupH - ANCHOR_GAP;
        if (aboveTop >= VIEWPORT_MARGIN) {
          top = aboveTop;
        } else {
          // Nema dovoljno prostora ni gore ni dole — clamp na donju ivicu.
          // (retko, ako je popup veći od viewport-a; korisnik može da skroluje sadržaj popup-a)
          top = Math.max(VIEWPORT_MARGIN, vh - popupH - VIEWPORT_MARGIN);
        }
      }
    }

    setLayout({ top, left, anchorWidth: r.width, ready: popup !== null });
  }

  // Pri otvaranju i kad se anchor promeni — postavi inicijalnu poziciju.
  useLayoutEffect(() => {
    if (!open || !anchor) {
      // Namerno: reset pozicije kad se dropdown zatvori/promeni anchor. Ne
      // uzrokuje kaskadu jer `layout` postaje null i komponenta rano izlazi.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setLayout(null);
      return;
    }

    recalc();
    const onWin = () => recalc();
    window.addEventListener('scroll', onWin, true);
    window.addEventListener('resize', onWin);
    return () => {
      window.removeEventListener('scroll', onWin, true);
      window.removeEventListener('resize', onWin);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, anchor]);

  // Klik van anchor-a / dropdown-a zatvara — ali pažljivo da ne zatvori sam sebe
  // pri kliku na stavku (dropdown sadržaj prima click-event preko mousedown).
  useEffect(() => {
    if (!open) return;

    function onMouseDown(e: MouseEvent) {
      const target = e.target as Node;
      if (anchor && anchor.contains(target)) return; // klik u sam anchor — ne zatvaraj
      const dropdownEl = popupNodeRef.current;
      if (dropdownEl && dropdownEl.contains(target)) return; // klik u dropdown — ne zatvaraj
      onRequestClose();
    }

    document.addEventListener('mousedown', onMouseDown);
    return () => document.removeEventListener('mousedown', onMouseDown);
  }, [open, anchor, onRequestClose]);

  // Esc zatvara svaki dropdown (meni, kalendar, select). Roditelj koji već
  // obrađuje Esc (Select) dobija dupli `onRequestClose` — bezopasno.
  useEffect(() => {
    if (!open) return;

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onRequestClose();
    }

    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [open, onRequestClose]);

  if (!open || !layout) return null;

  // Inline stil — pozicija + opacity (sakriveno dok ne izmerimo posle prvog render-a).
  const style: CSSProperties = {
    position: 'fixed',
    top: layout.top,
    left: layout.left,
    zIndex: 60,
    opacity: layout.ready ? 1 : 0, // sprečava flash neusaglašene pozicije
  };
  if (widthMode === 'anchor')          style.width = layout.anchorWidth;
  else if (widthMode === 'min-anchor') style.minWidth = layout.anchorWidth;
  // 'auto' — bez width override-a, content/className upravljaju širinom

  return createPortal(
    <div
      ref={(node) => {
        popupNodeRef.current = node;
        if (node && layout && !layout.ready) {
          // Tek smo mount-ovali — recalc da uključi pravu izmerenu veličinu
          // (korisnik će videti popup tek u sledećem frame-u, već u correct poziciji).
          recalc();
        }
      }}
      style={style}
      className={
        className ??
        'max-h-56 overflow-y-auto rounded-md border border-gray-200 bg-white shadow-lg'
      }
    >
      {children}
    </div>,
    document.body
  );
}
