import type { HTMLAttributes } from 'react';
import { cn } from '../../lib/cn';

/**
 * Kartica po mockup-u (`.card`): bela, tanak okvir, zaobljenje 10 px, bez senke; unutrašnji razmak 14 × 16 px.
 * Naslov i sadržaj dele isti levi rub, pa je sve u kartici poravnato.
 */
export function Card({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('rounded-[10px] border border-gray-200 bg-white', className)} {...rest} />;
}

export function CardHeader({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('flex flex-wrap items-center justify-between gap-2 px-4 pt-3.5', className)} {...rest} />;
}

export function CardTitle({ className, ...rest }: HTMLAttributes<HTMLHeadingElement>) {
  return <h2 className={cn('text-base font-semibold text-gray-800', className)} {...rest} />;
}

export function CardBody({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('px-4 py-3.5', className)} {...rest} />;
}
