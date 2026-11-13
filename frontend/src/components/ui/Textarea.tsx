import { forwardRef, type TextareaHTMLAttributes } from 'react';
import { cn } from '../../lib/cn';

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  /** Crveni okvir (validaciona greška) — poruku prikazuje `FormField`. */
  hasError?: boolean;
}

/**
 * Višeredno polje, vizuelno usklađeno sa `Input`-om. Uz `maxLength` se pri kraju (80%) vidi brojač, da duži tekst
 * ne bude tiho odsečen (zamerka 38).
 */
export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(
  ({ className, hasError, rows = 3, ...rest }, ref) => {
    const max = rest.maxLength;
    const duzina = typeof rest.value === 'string' ? rest.value.length : 0;
    return (
      <>
        <textarea
          ref={ref}
          rows={rows}
          className={cn(
            'block w-full rounded-lg border bg-white px-3 py-2 text-sm',
            'placeholder:text-gray-400 focus:outline-none focus:ring-2',
            hasError
              ? 'border-red-500 focus:ring-red-500'
              : 'border-gray-200 focus:border-primary-500 focus:ring-primary-500/30',
            'disabled:bg-gray-50 disabled:text-gray-500',
            className,
          )}
          {...rest}
        />
        {max !== undefined && max > 0 && duzina >= max * 0.8 && (
          <p className={cn('mt-0.5 text-right text-xs', duzina >= max ? 'text-red-600' : 'text-gray-500')}>
            {duzina} / {max}
          </p>
        )}
      </>
    );
  },
);
Textarea.displayName = 'Textarea';
