import { api, obnoviSesiju } from '../../api/client';
import type { CurrentUserResponse, LoginResponse, PromenaLozinkeInput } from '../../api/types';

/**
 * Auth API pozivi — tanak omotač oko axios-a, odvojen od `client.ts` da bi se
 * lako mock-ovao u testovima `AuthProvider`-a.
 */

export async function login(userName: string, password: string, zapamtiMe = false): Promise<LoginResponse> {
  const r = await api.post<LoginResponse>('/auth/login', { userName, password, zapamtiMe });
  return r.data;
}

/** Ide kroz zajedničku obnovu (`obnoviSesiju`), da se tajmer, start i interceptor ne preklope (rotacija tokena). */
export function refresh(): Promise<LoginResponse> {
  return obnoviSesiju();
}

export async function logout(): Promise<void> {
  await api.post('/auth/logout', {});
}

export async function getMe(): Promise<CurrentUserResponse> {
  const r = await api.get<CurrentUserResponse>('/auth/me');
  return r.data;
}

export async function promeniLozinku(input: PromenaLozinkeInput): Promise<void> {
  await api.post('/auth/change-password', input);
}
