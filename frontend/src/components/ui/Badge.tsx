import type { HTMLAttributes } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../../lib/cn';

const badgeVariants = cva(
  'inline-flex items-center gap-1 rounded px-2 py-0.5 text-xs font-medium',
  {
    variants: {
      tone: {
        gray:    'bg-gray-100 text-gray-700',
        primary: 'bg-primary-100 text-primary-700',
        amber:   'bg-amber-100 text-amber-800',
        red:     'bg-red-100 text-red-700',
      },
    },
    defaultVariants: { tone: 'gray' },
  }
);

export interface BadgeProps
  extends HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {}

export function Badge({ className, tone, ...rest }: BadgeProps) {
  return <span className={cn(badgeVariants({ tone }), className)} {...rest} />;
}
