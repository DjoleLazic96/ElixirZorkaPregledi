import type { KeyboardEvent } from 'react';
import { cn } from '../../lib/cn';

export interface TabItem<K extends string> {
  key: K;
  label: string;
}

interface TabsProps<K extends string> {
  items: ReadonlyArray<TabItem<K>>;
  value: K;
  onChange: (key: K) => void;
  /** Pristupačni naziv grupe tabova (npr. „Administracija"). */
  'aria-label'?: string;
  className?: string;
}

/**
 * Underline tabovi (blueprint 14 §1): `border-b-2`, aktivan `border-primary-500
 * text-primary-700`. Kontrolisana komponenta — roditelj drži `value` (obično
 * izvedeno iz rute). Tastatura: ←/→ pomeraju fokus i biraju tab, Home/End skaču.
 */
export function Tabs<K extends string>({
  items,
  value,
  onChange,
  'aria-label': ariaLabel,
  className,
}: TabsProps<K>) {
  function onKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    const idx = items.findIndex((i) => i.key === value);
    let sledeci: number;
    if (e.key === 'ArrowRight') sledeci = (idx + 1) % items.length;
    else if (e.key === 'ArrowLeft') sledeci = (idx - 1 + items.length) % items.length;
    else if (e.key === 'Home') sledeci = 0;
    else if (e.key === 'End') sledeci = items.length - 1;
    else return;
    e.preventDefault();
    onChange(items[sledeci].key);
    (e.currentTarget.children[sledeci] as HTMLElement | undefined)?.focus();
  }

  return (
    <div
      role="tablist"
      aria-label={ariaLabel}
      onKeyDown={onKeyDown}
      className={cn('flex flex-wrap items-center gap-1 border-b border-gray-200', className)}
    >
      {items.map((item) => {
        const aktivan = item.key === value;
        return (
          <button
            key={item.key}
            type="button"
            role="tab"
            aria-selected={aktivan}
            tabIndex={aktivan ? 0 : -1}
            onClick={() => onChange(item.key)}
            className={cn(
              '-mb-px border-b-2 px-3 py-2 text-sm transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-500',
              aktivan
                ? 'border-primary-500 font-medium text-primary-700'
                : 'border-transparent text-gray-600 hover:text-gray-900',
            )}
          >
            {item.label}
          </button>
        );
      })}
    </div>
  );
}
