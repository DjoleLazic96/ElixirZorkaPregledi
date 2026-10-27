/* Fajl izvozi i <AuthProvider> komponentu i useAuth() hook (standardni React
 * context obrazac). Fast-refresh upozorenje je ovde bezopasno. */
/* eslint-disable react-refresh/only-export-components */
import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useState,
} from 'react';
import type { AxiosError } from 'axios';
import { setAccessToken, setOnSessionExpired, setOnTokenRefreshed } from '../../api/client';
import type { CurrentUserResponse } from '../../api/types';
import { getMe, login as apiLogin, logout as apiLogout, refresh as apiRefresh } from './auth-api';

/**
 * Koliko pre isteka access tokena pokrećemo tihu obnovu sesije.
 *
 * Access token traje 15 min, refresh token (httpOnly cookie) 7 dana. Umesto da
 * korisnika odjavimo kad istekne access token, 60 s pre isteka tiho razmenimo
 * refresh token za nov par. Sesija tako traje koliko i refresh token, uz klizni
 * prozor; korisnik se odjavljuje samo kad on to traži ili kad refresh padne.
 */
const REFRESH_LEAD_MS = 60_000;

interface AuthState {
  /** `null` = još nije poznato, `undefined` = nije ulogovan, objekat = ulogovan. */
  me: CurrentUserResponse | null | undefined;
  login: (userName: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  /** `true` dok proveravamo postojeću sesiju pri pokretanju. */
  initializing: boolean;
  /** Sesija je istekla (refresh odbijen) — LoginPage prikazuje objašnjenje. */
  sesijaIstekla: boolean;
}

const AuthContext = createContext<AuthState | null>(null);

/** Refresh odbijen od servera (401/403) = sesija je mrtva; mreža/timeout nisu. */
function jeOdbijenRefresh(err: unknown): boolean {
  const status = (err as AxiosError | undefined)?.response?.status;
  return status === 401 || status === 403;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<CurrentUserResponse | null | undefined>(null);
  const [initializing, setInitializing] = useState(true);
  const [sesijaIstekla, setSesijaIstekla] = useState(false);
  /**
   * ISO trenutak isteka tekućeg access tokena, ili `null` kad nema sesije.
   * Promena re-pokreće effect ispod koji zakazuje sledeću tihu obnovu — obnova
   * se tako sama ulančava dok sesija traje.
   */
  const [tokenExpiresAt, setTokenExpiresAt] = useState<string | null>(null);

  const ocistiSesiju = useCallback((istekla: boolean) => {
    setAccessToken(null);
    setTokenExpiresAt(null); // gasi tajmer obnove
    setMe(undefined);
    setSesijaIstekla(istekla);
  }, []);

  const logout = useCallback(async () => {
    try {
      await apiLogout();
    } finally {
      ocistiSesiju(false);
    }
  }, [ocistiSesiju]);

  // Interceptor u `api/client.ts` javlja dve stvari: da je refresh odbijen (sesija
  // istekla → očisti UI, inače bi ekran ostao „ulogovan" sa mrtvim tokenom) i da
  // je sam obnovio token (re-armiraj tajmer na novi rok).
  useEffect(() => {
    setOnSessionExpired(() => ocistiSesiju(true));
    setOnTokenRefreshed((expiresAt) => setTokenExpiresAt(expiresAt));
    return () => {
      setOnSessionExpired(null);
      setOnTokenRefreshed(null);
    };
  }, [ocistiSesiju]);

  // Tiha obnova: dok imamo aktivan token, zakaži refresh REFRESH_LEAD_MS pre isteka.
  // Odbijen refresh = kraj sesije. Mrežna greška se ignoriše — sledeći API poziv
  // dobije 401 i interceptor pokuša ponovo (pa re-armira tajmer kroz onTokenRefreshed).
  useEffect(() => {
    if (tokenExpiresAt === null) return;

    const delay = Math.max(0, new Date(tokenExpiresAt).getTime() - Date.now() - REFRESH_LEAD_MS);

    const timerId = window.setTimeout(() => {
      void (async () => {
        try {
          const r = await apiRefresh();
          setAccessToken(r.accessToken);
          setTokenExpiresAt(r.expiresAt);
        } catch (err) {
          if (jeOdbijenRefresh(err)) ocistiSesiju(true);
        }
      })();
    }, delay);

    return () => window.clearTimeout(timerId);
  }, [tokenExpiresAt, ocistiSesiju]);

  // Pri startu: ako postoji važeći refresh cookie, dovuci sebe. Neuspeh ovde nije
  // „istekla sesija" (korisnik možda nikad nije bio ulogovan) — bez poruke.
  useEffect(() => {
    (async () => {
      try {
        const r = await apiRefresh();
        setAccessToken(r.accessToken);
        setTokenExpiresAt(r.expiresAt);
        setMe(await getMe());
      } catch {
        setAccessToken(null);
        setMe(undefined);
      } finally {
        setInitializing(false);
      }
    })();
  }, []);

  const login = useCallback(async (userName: string, password: string) => {
    const r = await apiLogin(userName, password);
    setAccessToken(r.accessToken);
    setTokenExpiresAt(r.expiresAt);
    setMe({ user: r.user, roles: r.roles });
    setSesijaIstekla(false);
  }, []);

  return (
    <AuthContext.Provider value={{ me, login, logout, initializing, sesijaIstekla }}>
      {children}
    </AuthContext.Provider>
  );
}

/** Pristup auth stanju; baca ako je komponenta van `AuthProvider`-a. */
export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (ctx === null) {
    throw new Error('useAuth mora biti unutar <AuthProvider>.');
  }
  return ctx;
}
