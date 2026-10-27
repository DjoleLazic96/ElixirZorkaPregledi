import { forwardRef, type InputHTMLAttributes } from 'react';
import { cn } from '../../lib/cn';

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  /** Ako je <c>true</c>, polje dobija crveni okvir (validation error). */
  hasError?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ className, hasError, ...rest }, ref) => (
    <input
      ref={ref}
      className={cn(
        'block w-full rounded-md border bg-white px-3 py-2 text-sm',
        'placeholder:text-gray-400 focus:outline-none focus:ring-2',
        hasError
          ? 'border-red-500 focus:ring-red-500'
          : 'border-gray-300 focus:border-primary-500 focus:ring-primary-500',
        'disabled:bg-gray-50 disabled:text-gray-500',
        className
      )}
      {...rest}
    />
  )
);
Input.displayName = 'Input';
