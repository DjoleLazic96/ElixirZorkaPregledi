import { forwardRef, type TextareaHTMLAttributes } from 'react';
import { cn } from '../../lib/cn';

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  /** Crveni okvir (validaciona greška) — poruku prikazuje `FormField`. */
  hasError?: boolean;
}

/** Višeredno polje, vizuelno usklađeno sa `Input`-om. */
export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(
  ({ className, hasError, rows = 3, ...rest }, ref) => (
    <textarea
      ref={ref}
      rows={rows}
      className={cn(
        'block w-full rounded-md border bg-white px-3 py-2 text-sm',
        'placeholder:text-gray-400 focus:outline-none focus:ring-2',
        hasError
          ? 'border-red-500 focus:ring-red-500'
          : 'border-gray-300 focus:border-primary-500 focus:ring-primary-500',
        'disabled:bg-gray-50 disabled:text-gray-500',
        className,
      )}
      {...rest}
    />
  ),
);
Textarea.displayName = 'Textarea';
