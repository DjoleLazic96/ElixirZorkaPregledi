import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { cn } from '../../lib/cn';
import type { Pristup } from '../../lib/permissions';
import { vidljiveStavke } from './nav';
import { GIT_SHA, VERZIJA } from '../../lib/verzija';

interface SidebarProps {
  pristup: Pristup;
}

/**
 * Levi meni po mockup-u (`nav.side`): zelena traka 230 px, aktivna stavka tamnija sa svetlo zelenom linijom levo; na dnu grupa
 * „Prijavljen“ sa odjavom i verzijom (blueprint 11 §4, `title` = git SHA). Stavke bez prava se ne renderuju — isti `permissions.ts`
 * filtrira i rute.
 */
export function Sidebar({ pristup, onOdjava }: SidebarProps & { onOdjava: () => void }) {
  return (
    <aside className="hidden w-[230px] shrink-0 flex-col overflow-y-auto bg-primary-500 py-3 text-sidebar-text md:flex">
      <nav aria-label="Glavna navigacija">
        <ul>
          {vidljiveStavke(pristup).map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to} end={item.to === '/'}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-2.5 border-l-4 px-[18px] py-2.5 text-sm transition-colors',
                    isActive ? 'border-accent-500 bg-primary-600 font-semibold text-white' : 'border-transparent hover:bg-primary-600',
                  )
                }
              >
                <span aria-hidden className="w-5 text-center">{item.ikona}</span>
                <span>{item.label}</span>
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
      <div className="px-[18px] pb-1 pt-3.5 text-[11px] uppercase tracking-wider text-sidebar-group">Prijavljen</div>
      <button type="button" onClick={onOdjava}
        className="flex items-center gap-2.5 border-l-4 border-transparent px-[18px] py-2.5 text-left text-sm transition-colors hover:bg-primary-600">
        <span aria-hidden className="w-5 text-center">↩</span>
        <span>Odjava</span>
      </button>
      <div className="mt-auto px-[18px] pt-4 text-xs text-sidebar-group" title={GIT_SHA}>
        v{VERZIJA}
      </div>
    </aside>
  );
}

/**
 * Donji meni na telefonu po mockup-u (`nav.bottom`): prve 4 stavke + „Više“, ikonica iznad naziva, aktivna zelena. Dugmad su
 * najmanje 44 × 44 px (spec §8.1).
 */
export function DonjiMeni({ pristup }: SidebarProps) {
  const [vise, setVise] = useState(false);
  const stavke = vidljiveStavke(pristup);
  const glavne = stavke.slice(0, 4);
  const ostale = stavke.slice(4);
  const klasa = ({ isActive }: { isActive: boolean }) =>
    cn('flex min-h-11 min-w-[60px] flex-1 flex-col items-center justify-center gap-0.5 px-1 text-[11px]', isActive ? 'font-semibold text-primary-500' : 'text-gray-500');

  return (
    <>
      {vise && (
        <div className="fixed inset-0 z-30 bg-black/20 md:hidden" onClick={() => setVise(false)} aria-hidden />
      )}
      {vise && (
        <ul className="fixed inset-x-0 bottom-16 z-40 mx-3 rounded-[10px] border border-gray-200 bg-white p-2 shadow-lg md:hidden" aria-label="Više">
          {ostale.map((item) => (
            <li key={item.to}>
              <NavLink to={item.to} end={item.to === '/'} onClick={() => setVise(false)}
                className={({ isActive }) => cn('flex min-h-11 items-center gap-3 rounded-lg px-3 text-sm', isActive ? 'bg-primary-50 font-semibold text-primary-500' : 'text-gray-800')}>
                <span aria-hidden className="w-5 text-center">{item.ikona}</span>
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      )}
      <nav className="fixed inset-x-0 bottom-0 z-40 flex h-16 justify-around border-t border-gray-200 bg-white pb-1 md:hidden" aria-label="Donji meni">
        {glavne.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.to === '/'} className={klasa}>
            <span aria-hidden className="text-[19px] leading-none">{item.ikona}</span>
            <span className="max-w-full truncate">{item.label}</span>
          </NavLink>
        ))}
        {ostale.length > 0 && (
          <button type="button" onClick={() => setVise((v) => !v)} aria-expanded={vise}
            className={cn('flex min-h-11 min-w-[60px] flex-1 flex-col items-center justify-center gap-0.5 text-[11px]', vise ? 'font-semibold text-primary-500' : 'text-gray-500')}>
            <span aria-hidden className="text-[19px] leading-none">☰</span>
            Više
          </button>
        )}
      </nav>
    </>
  );
}
