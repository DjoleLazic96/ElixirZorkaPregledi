import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { Menu, X } from 'lucide-react';
import { cn } from '../../lib/cn';
import type { Pristup } from '../../lib/permissions';
import { vidljiveStavke } from './nav';
import { GIT_SHA, VERZIJA } from '../../lib/verzija';

interface SidebarProps {
  pristup: Pristup;
}

/**
 * Levi meni. Stavke bez prava se uopšte ne renderuju (ne `disabled`) — isti
 * `permissions.ts` filtrira i rute. Na dnu verzija (blueprint 11 §4), `title` = git SHA.
 */
export function Sidebar({ pristup }: SidebarProps) {
  return (
    <aside className="hidden w-60 shrink-0 flex-col border-r border-gray-200 bg-white md:flex">
      <div className="flex h-14 items-center border-b border-gray-200 px-5">
        <span className="text-base font-semibold text-primary-600">TPM pregledi</span>
      </div>

      <nav className="flex-1 overflow-y-auto p-3" aria-label="Glavna navigacija">
        <ul className="space-y-1">
          {vidljiveStavke(pristup).map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors',
                    isActive
                      ? 'bg-primary-50 font-medium text-primary-700'
                      : 'text-gray-700 hover:bg-gray-100',
                  )
                }
              >
                <item.icon size={16} aria-hidden />
                <span>{item.label}</span>
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <div className="border-t border-gray-200 px-5 py-3 text-xs text-gray-400" title={GIT_SHA}>
        v{VERZIJA}
      </div>
    </aside>
  );
}

/**
 * Donji meni na telefonu (< 768 px, odluka „mobilni prikaz“): prve 4 stavke + „Više“ sa ostalima. Dugmad su najmanje
 * 44 × 44 px (spec §8.1).
 */
export function DonjiMeni({ pristup }: SidebarProps) {
  const [vise, setVise] = useState(false);
  const stavke = vidljiveStavke(pristup);
  const glavne = stavke.slice(0, 4);
  const ostale = stavke.slice(4);
  const klasa = ({ isActive }: { isActive: boolean }) =>
    cn('flex min-h-11 min-w-11 flex-1 flex-col items-center justify-center gap-0.5 px-1 text-[11px]', isActive ? 'text-primary-700' : 'text-gray-600');

  return (
    <>
      {vise && (
        <div className="fixed inset-0 z-30 bg-black/20 md:hidden" onClick={() => setVise(false)} aria-hidden />
      )}
      {vise && (
        <ul className="fixed inset-x-0 bottom-16 z-40 mx-3 rounded-lg border border-gray-200 bg-white p-2 shadow-lg md:hidden" aria-label="Više">
          {ostale.map((item) => (
            <li key={item.to}>
              <NavLink to={item.to} onClick={() => setVise(false)}
                className={({ isActive }) => cn('flex min-h-11 items-center gap-3 rounded-md px-3 text-sm', isActive ? 'bg-primary-50 text-primary-700' : 'text-gray-700')}>
                <item.icon size={16} aria-hidden />
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      )}
      <nav className="fixed inset-x-0 bottom-0 z-40 flex h-16 border-t border-gray-200 bg-white md:hidden" aria-label="Donji meni">
        {glavne.map((item) => (
          <NavLink key={item.to} to={item.to} className={klasa}>
            <item.icon size={20} aria-hidden />
            <span className="max-w-full truncate">{item.label}</span>
          </NavLink>
        ))}
        {ostale.length > 0 && (
          <button type="button" onClick={() => setVise((v) => !v)} aria-expanded={vise}
            className="flex min-h-11 min-w-11 flex-1 flex-col items-center justify-center gap-0.5 text-[11px] text-gray-600">
            {vise ? <X size={20} aria-hidden /> : <Menu size={20} aria-hidden />}
            Više
          </button>
        )}
      </nav>
    </>
  );
}
