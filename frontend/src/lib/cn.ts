import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * `cn` helper — spaja Tailwind klase i resava konflikte (poslednja pobedjuje).
 *
 * Primer:
 *   cn('px-2 py-1', 'px-4', condition && 'bg-red-500') === 'py-1 px-4 bg-red-500'
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
