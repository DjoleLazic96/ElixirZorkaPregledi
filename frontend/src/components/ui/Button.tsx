import { forwardRef, type ButtonHTMLAttributes } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../../lib/cn';

/**
 * Dugme po mockup-u (`.btn`): primary — puna zelena; secondary — belo sa zelenim okvirom i tekstom; ghost — bez podloge, sivi okvir;
 * danger — crvena. Sve varijante iste visine po veličini (md 40 px, sm 32 px; na telefonu 44 px) i istog zaobljenja (8 px), da
 * dugmad u istom redu budu poravnata.
 */
const buttonVariants = cva(
  // bazne klase za sve varijante
  'inline-flex shrink-0 items-center justify-center gap-1.5 whitespace-nowrap rounded-lg border font-semibold ' +
    'transition-colors focus:outline-none focus:ring-2 focus:ring-offset-1 ' +
    'disabled:opacity-50 disabled:cursor-not-allowed',
  {
    variants: {
      variant: {
        primary:
          'border-primary-500 bg-primary-500 text-white hover:border-primary-600 hover:bg-primary-600 focus:ring-primary-500',
        secondary:
          'border-primary-500 bg-white text-primary-500 hover:bg-primary-50 focus:ring-primary-500',
        ghost:
          'border-gray-200 bg-transparent font-medium text-gray-800 hover:bg-gray-100 focus:ring-gray-400',
        danger:
          'border-red-500 bg-red-500 text-white hover:border-red-800 hover:bg-red-800 focus:ring-red-500',
      },
      size: {
        // Na telefonu dugme je najmanje 44 × 44 px (spec §8.1 t. 6); od md širine kompaktno kao u blueprintu.
        sm: 'h-11 min-w-11 px-3 text-xs md:h-8 md:min-w-0',
        md: 'h-11 px-4 text-sm md:h-10',
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
