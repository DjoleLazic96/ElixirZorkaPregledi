import type { ReactNode } from 'react';
import { cn } from '../../lib/cn';

interface PageHeaderProps {
  title: string;
  description?: string;
  /** Dugmad desno (npr. „Nova kategorija", „Preuzmi Excel"). */
  actions?: ReactNode;
  className?: string;
}

/** Naslov stranice sa opcionim opisom i akcijama desno (blueprint 14 §1). */
export function PageHeader({ title, description, actions, className }: PageHeaderProps) {
  return (
    // Akcije prelaze u novi red ispod naslova kad ih je više nego što staje (§8.1: ništa ne izlazi iz ekrana).
    <div className={cn('flex flex-wrap items-start justify-between gap-4', className)}>
      <div className="min-w-0">
        <h1 className="text-xl font-semibold text-gray-900">{title}</h1>
        {description && <p className="mt-1 text-sm text-gray-500">{description}</p>}
      </div>
      {actions && <div className="flex max-w-full flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}
