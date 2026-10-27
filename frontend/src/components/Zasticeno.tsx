import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';

interface ZasticenoProps {
  /** Rezultat `permissions.ts` funkcije za rolu ulogovanog korisnika. */
  allowed: boolean;
  /** Kuda preusmeriti kad nema prava (landing po roli). */
  landing: string;
  children: ReactNode;
}

/**
 * Guard na SVAKOJ ruti (blueprint 13 §4), ne samo na `/admin`: korisnik bez
 * prava koji ukuca URL biva tiho vraćen na svoj landing. Backend ostaje
 * autoritativan — ovo je samo UX.
 */
export function Zasticeno({ allowed, landing, children }: ZasticenoProps) {
  return allowed ? <>{children}</> : <Navigate to={landing} replace />;
}
