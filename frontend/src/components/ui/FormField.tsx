import type { ReactNode } from 'react';
import { cn } from '../../lib/cn';

interface FormFieldProps {
  label: string;
  /** `id` kontrole unutar polja — labela se vezuje preko `htmlFor`. */
  htmlFor: string;
  /** Greška za ovo polje (lokalna validacija ili `errors` mapa sa servera). */
  error?: string | null;
  /** Pomoćni tekst ispod polja; sakriva se dok je prikazana greška. */
  hint?: string;
  children: ReactNode;
  className?: string;
}

/**
 * Jedino mesto gde se slaže labela + kontrola + pomoćni tekst + greška, da sve
 * forme izgledaju isto i da svaka kontrola ima pridruženu `<label>`.
 */
export function FormField({ label, htmlFor, error, hint, children, className }: FormFieldProps) {
  return (
    <div className={cn('space-y-1', className)}>
      <label htmlFor={htmlFor} className="block text-xs font-medium text-gray-600">
        {label}
      </label>
      {children}
      {error ? (
        <p id={`${htmlFor}-greska`} className="text-xs text-red-600">
          {error}
        </p>
      ) : (
        hint && <p className="text-xs text-gray-500">{hint}</p>
      )}
    </div>
  );
}
