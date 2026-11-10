import type { HTMLAttributes } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../../lib/cn';

/**
 * Oznaka po mockup-u (`.badge`): pilula u boji po značenju — siva (neutralno), zelena (u redu / čeka overu), žuta (u toku / čeka),
 * plava (overeno / informacija), crvena (problem / hitno), narandžasta (upozorenje / 7 dana). `primary` i `amber` su stari nazivi
 * za zelenu i žutu.
 */
const badgeVariants = cva(
  'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap',
  {
    variants: {
      tone: {
        gray:    'bg-gray-100 text-gray-700',
        primary: 'bg-green-50 text-green-700',
        green:   'bg-green-50 text-green-700',
        amber:   'bg-amber-50 text-amber-800',
        yellow:  'bg-amber-50 text-amber-800',
        blue:    'bg-blue-50 text-blue-700',
        red:     'bg-red-50 text-red-700',
        orange:  'bg-orange-50 text-orange-700',
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
