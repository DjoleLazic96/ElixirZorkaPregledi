import { NavLink } from 'react-router-dom';
import { Activity, ScrollText, Settings, Tag, type LucideIcon } from 'lucide-react';
import { cn } from '../../lib/cn';
import { canSeeAdminSection, isAdmin } from '../../lib/permissions';
import { GIT_SHA, VERZIJA } from '../../lib/verzija';

interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Ako je undefined, stavka je vidljiva svim ulogovanim korisnicima. */
  visibleIf?: (roles: readonly string[]) => boolean;
}

const NAV: NavItem[] = [
  { to: '/kategorije', label: 'Kategorije', icon: Tag },
  { to: '/admin', label: 'Administracija', icon: Settings, visibleIf: canSeeAdminSection },
  // Dijagnostika — zasebne stavke, ne pod Administracijom: nisu šifarnici i
  // otvaraju se povremeno, pa im je mesto u glavnom meniju, ne u pod-tabovima.
  { to: '/audit-log', label: 'Audit log', icon: ScrollText, visibleIf: isAdmin },
  { to: '/stanje-servera', label: 'Stanje servera', icon: Activity, visibleIf: isAdmin },
];

interface SidebarProps {
  roles: readonly string[];
}

/**
 * Levi meni. Stavke bez prava se uopšte ne renderuju (ne `disabled`) — isti
 * `permissions.ts` filtrira i rute. Na dnu verzija (blueprint 11 §4), `title` = git SHA.
 */
export function Sidebar({ roles }: SidebarProps) {
  return (
    <aside className="flex w-60 shrink-0 flex-col border-r border-gray-200 bg-white">
      <div className="flex h-14 items-center border-b border-gray-200 px-5">
        <span className="text-base font-semibold text-primary-600">TPM pregledi</span>
      </div>

      <nav className="flex-1 overflow-y-auto p-3" aria-label="Glavna navigacija">
        <ul className="space-y-1">
          {NAV.filter((i) => i.visibleIf === undefined || i.visibleIf(roles)).map((item) => (
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
