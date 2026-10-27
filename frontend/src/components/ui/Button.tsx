import { forwardRef, type ButtonHTMLAttributes } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../../lib/cn';

/**
 * Varijante za <Button> — primary (akcent), secondary (siva), ghost (transparent),
 * danger (crvena). Velicine: sm | md.
 */
const buttonVariants = cva(
  // bazne klase za sve varijante
  'inline-flex items-center justify-center gap-2 rounded-md font-medium ' +
    'transition-colors focus:outline-none focus:ring-2 focus:ring-offset-1 ' +
    'disabled:opacity-50 disabled:cursor-not-allowed',
  {
    variants: {
      variant: {
        primary:
          'bg-primary-500 text-white hover:bg-primary-600 focus:ring-primary-500',
        secondary:
          'bg-gray-100 text-gray-900 hover:bg-gray-200 focus:ring-gray-400 ' +
          'border border-gray-200',
        ghost:
          'bg-transparent text-gray-700 hover:bg-gray-100 focus:ring-gray-400',
        danger:
          'bg-red-500 text-white hover:bg-red-600 focus:ring-red-500',
      },
      size: {
        sm: 'h-8 px-3 text-xs',
        md: 'h-10 px-4 text-sm',
      },
    },
    defaultVariants: { variant: 'primary', size: 'md' },
  }
);

export interface ButtonProps
  extends ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, type = 'button', ...rest }, ref) => (
    <button
      ref={ref}
      type={type}
      className={cn(buttonVariants({ variant, size }), className)}
      {...rest}
    />
  )
);
Button.displayName = 'Button';
