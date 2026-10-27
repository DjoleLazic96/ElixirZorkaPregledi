import { forwardRef, useId, type InputHTMLAttributes } from 'react';
import { cn } from '../../lib/cn';

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  /** Tekst desno od kvadratića — ujedno i pristupačni naziv. */
  label: string;
  /** Sitniji opis ispod labele. */
  description?: string;
}

/** Checkbox sa labelom desno; cela labela je klikabilna. */
export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(
  ({ label, description, id, className, disabled, ...rest }, ref) => {
    const autoId = useId();
    const inputId = id ?? autoId;
    return (
      <label
        htmlFor={inputId}
        className={cn(
          'flex items-start gap-2 text-sm',
          disabled ? 'cursor-not-allowed text-gray-400' : 'cursor-pointer text-gray-800',
          className,
        )}
      >
        <input
          ref={ref}
          id={inputId}
          type="checkbox"
          disabled={disabled}
          className="mt-0.5 h-4 w-4 shrink-0 rounded border-gray-300 accent-primary-500 focus:ring-2 focus:ring-primary-500"
          {...rest}
        />
        <span>
          <span>{label}</span>
          {description && <span className="block text-xs text-gray-500">{description}</span>}
        </span>
      </label>
    );
  },
);
Checkbox.displayName = 'Checkbox';
