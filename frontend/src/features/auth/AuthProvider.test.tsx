import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import type { LoginResponse } from '../../api/types';
import { setOnSessionExpired, setOnTokenRefreshed } from '../../api/client';
import { apiGreska } from '../../test/utils';
import { AuthProvider, useAuth } from './AuthProvider';

// `auth-api` se mock-uje da bi test kontrolisao login/refresh/logout odgovore.
vi.mock('./auth-api', () => ({
  login: vi.fn(),
  logout: vi.fn(),
  refresh: vi.fn(),
  getMe: vi.fn(),
}));
// `api/client` se mock-uje da uhvatimo callback-ove koje provider registruje.
vi.mock('../../api/client', () => ({
  setAccessToken: vi.fn(),
  setOnSessionExpired: vi.fn(),
  setOnTokenRefreshed: vi.fn(),
}));

import { login as apiLogin, logout as apiLogout, refresh as apiRefresh } from './auth-api';

const mockLogin = vi.mocked(apiLogin);
const mockLogout = vi.mocked(apiLogout);
const mockRefresh = vi.mocked(apiRefresh);

/** Access token traje 15 min (backend `Jwt:AccessTokenMinutes`). */
const ACCESS_TOKEN_MS = 15 * 60_000;

function loginResponse(inMs: number): LoginResponse {
  return {
    accessToken: `access-${Math.random()}`,
    expiresAt: new Date(Date.now() + inMs).toISOString(),
    user: { id: 'u1', userName: 'admin', fullName: 'Administrator', email: null },
    roles: ['Admin'],
  };
}

/** Sonda: izlaže auth stanje kao tekst i daje dugme za prijavu. */
function Probe() {
  const { me, login, initializing, sesijaIstekla } = useAuth();
  const state = initializing
    ? 'init'
    : me === null
      ? 'unknown'
      : me === undefined
        ? 'logged-out'
        : 'logged-in';
  return (
    <div>
      <span data-testid="state">{state}</span>
      <span data-testid="istekla">{String(sesijaIstekla)}</span>
      <button
        onClick={() => {
          void login('admin', 'pw');
        }}
      >
        do-login
      </button>
    </div>
  );
}

function authState(): string | null {
  return screen.getByTestId('state').textContent;
}

/** Startuje bez sesije i prijavljuje korisnika sa tokenom koji ističe za 15 min. */
async function prijavljenKorisnik() {
  mockRefresh.mockRejectedValueOnce(new Error('no session'));
  render(
    <AuthProvider>
      <Probe />
    </AuthProvider>,
  );
  await act(async () => {});
  expect(authState()).toBe('logged-out');

  mockLogin.mockResolvedValueOnce(loginResponse(ACCESS_TOKEN_MS));
  await act(async () => {
    fireEvent.click(screen.getByText('do-login'));
  });
  expect(authState()).toBe('logged-in');
}

describe('AuthProvider — održavanje sesije', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Fake-ujemo SAMO setTimeout/clearTimeout — `Date` ostaje realan da bi izračun
    // „koliko do isteka tokena" bio stabilan kroz advanceTimersByTime.
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('ne odjavljuje korisnika kad istekne access token — tiho obnavlja sesiju', async () => {
    await prijavljenKorisnik();

    // `mockImplementationOnce`: `expiresAt` se računa u trenutku poziva.
    mockRefresh.mockImplementationOnce(async () => loginResponse(ACCESS_TOKEN_MS));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(ACCESS_TOKEN_MS);
    });

    expect(authState()).toBe('logged-in');
    expect(mockLogout).not.toHaveBeenCalled();
    expect(mockRefresh).toHaveBeenCalledTimes(2); // startup + tiha obnova
  });

  it('odjavljuje korisnika sa porukom tek kad server odbije obnovu (401)', async () => {
    await prijavljenKorisnik();

    mockRefresh.mockRejectedValueOnce(apiGreska(401));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(ACCESS_TOKEN_MS);
    });

    expect(authState()).toBe('logged-out');
    expect(screen.getByTestId('istekla')).toHaveTextContent('true');
  });

  it('mrežna greška pri tihoj obnovi NE odjavljuje korisnika', async () => {
    await prijavljenKorisnik();

    mockRefresh.mockRejectedValueOnce(new Error('Network Error'));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(ACCESS_TOKEN_MS);
    });

    expect(authState()).toBe('logged-in');
  });

  it('kad interceptor javi da je sesija istekla, čisti `me` i pamti razlog', async () => {
    await prijavljenKorisnik();

    const callback = vi.mocked(setOnSessionExpired).mock.calls.at(-1)?.[0];
    expect(callback).toBeTypeOf('function');
    act(() => {
      callback!();
    });

    expect(authState()).toBe('logged-out');
    expect(screen.getByTestId('istekla')).toHaveTextContent('true');
  });

  it('kad interceptor obnovi token, tajmer tihe obnove se pomera na novi rok', async () => {
    await prijavljenKorisnik();

    // Interceptor je obnovio token koji ističe za 30 min — stari rok (15 min) više ne važi.
    const obnovljen = vi.mocked(setOnTokenRefreshed).mock.calls.at(-1)?.[0];
    act(() => {
      obnovljen!(new Date(Date.now() + 2 * ACCESS_TOKEN_MS).toISOString());
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(ACCESS_TOKEN_MS);
    });
    // Na stari rok refresh NIJE pozvan (samo startup poziv).
    expect(mockRefresh).toHaveBeenCalledTimes(1);
  });

  it('nova prijava briše poruku o isteku sesije', async () => {
    await prijavljenKorisnik();
    act(() => {
      vi.mocked(setOnSessionExpired).mock.calls.at(-1)![0]!();
    });
    expect(screen.getByTestId('istekla')).toHaveTextContent('true');

    mockLogin.mockResolvedValueOnce(loginResponse(ACCESS_TOKEN_MS));
    await act(async () => {
      fireEvent.click(screen.getByText('do-login'));
    });
    expect(authState()).toBe('logged-in');
    expect(screen.getByTestId('istekla')).toHaveTextContent('false');
  });
});
