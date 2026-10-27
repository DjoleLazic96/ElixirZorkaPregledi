import type { ReactNode } from 'react';
import { Inbox, type LucideIcon } from 'lucide-react';
import { cn } from '../../lib/cn';

interface EmptyStateProps {
  title: string;
  description?: string;
  icon?: LucideIcon;
  /** Opciono dugme (npr. „Dodaj prvu kategoriju"). */
  action?: ReactNode;
  className?: string;
}

/**
 * Prazno stanje liste (blueprint 14 §4): filteri daju 0 → „Nema zapisa za
 * zadate filtere."; prazan šifarnik → „Još nema … Dodajte prvi." + akcija.
 */
export function EmptyState({ title, description, icon: Icon = Inbox, action, className }: EmptyStateProps) {
  return (
    <div className={cn('flex flex-col items-center py-10 text-center', className)}>
      <Icon size={32} className="text-gray-300" aria-hidden />
      <p className="mt-3 text-sm font-medium text-gray-700">{title}</p>
      {description && <p className="mt-1 text-xs text-gray-500">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
