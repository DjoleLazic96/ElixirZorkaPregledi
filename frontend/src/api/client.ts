import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios';
import type { ApiProblem, LoginResponse } from './types';

/**
 * Axios klijent za backend API (blueprint 13 §2).
 *
 *  - sve rute idu pod `/api/v1` (isti origin; dev proxy u vite.config.ts)
 *  - access token (JWT) živi SAMO u memoriji — postavlja ga `setAccessToken()`
 *    posle prijave/obnove; refresh token je u httpOnly cookie-ju koji browser
 *    šalje sam (`withCredentials: true`)
 *  - 401 → jedan single-flight refresh → ponovi originalni zahtev; ako refresh
 *    sam padne sa 401/403, sesija je mrtva → `onSessionExpired()` čisti UI.
 *    Mrežna greška/timeout pri refresh-u NE odjavljuje korisnika (samo odbija zahtev).
 */

let accessToken: string | null = null;
let onSessionExpired: (() => void) | null = null;
let onTokenRefreshed: ((expiresAt: string) => void) | null = null;

/** Postavi (ili obriši) access token u memoriji. */
export function setAccessToken(token: string | null): void {
  accessToken = token;
}

/** AuthProvider registruje callback koji čisti `me` kad refresh definitivno padne. */
export function setOnSessionExpired(cb: (() => void) | null): void {
  onSessionExpired = cb;
}

/**
 * AuthProvider registruje callback da sazna novi `expiresAt` kad interceptor
 * obnovi token — inače bi tajmer tihe obnove ostao naoružan na stari rok.
 */
export function setOnTokenRefreshed(cb: ((expiresAt: string) => void) | null): void {
  onTokenRefreshed = cb;
}

export const api: AxiosInstance = axios.create({
  baseURL: '/api/v1',
  withCredentials: true,
  timeout: 15_000,
});

api.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

/** Zahtev obeležen da je već jednom ponovljen posle refresh-a — nema beskonačne petlje. */
type RetriedConfig = InternalAxiosRequestConfig & { _retried?: boolean };

/** Rute na kojima 401 znači „pogrešni podaci / nema sesije", ne „istekao token". */
const BEZ_REFRESHA = ['/auth/login', '/auth/refresh'];

// Single-flight: više paralelnih 401 → jedan refresh poziv, svi čekaju isti Promise.
let refreshInFlight: Promise<LoginResponse> | null = null;

api.interceptors.response.use(undefined, async (error: AxiosError<ApiProblem>) => {
  const original = error.config as RetriedConfig | undefined;
  const status = error.response?.status;
  const bezRefresha = BEZ_REFRESHA.some((ruta) => original?.url?.includes(ruta));

  if (status !== 401 || !original || original._retried || bezRefresha) {
    throw error;
  }
  original._retried = true;

  try {
    refreshInFlight ??= axios
      .post<LoginResponse>('/api/v1/auth/refresh', {}, { withCredentials: true })
      .then((r) => r.data)
      .finally(() => {
        refreshInFlight = null;
      });
    const obnova = await refreshInFlight;
    setAccessToken(obnova.accessToken);
    onTokenRefreshed?.(obnova.expiresAt);
    return api.request(original);
  } catch (refreshErr) {
    // Samo odbijen refresh (401/403) znači kraj sesije. Mrežni prekid ili timeout
    // ne sme da izbaci korisnika — sledeći zahtev će ponovo pokušati.
    const refreshStatus = (refreshErr as AxiosError).response?.status;
    if (refreshStatus === 401 || refreshStatus === 403) {
      setAccessToken(null);
      onSessionExpired?.();
    }
    throw refreshErr;
  }
});
