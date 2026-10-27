import { api } from '../../api/client';
import type { CurrentUserResponse, LoginResponse, PromenaLozinkeInput } from '../../api/types';

/**
 * Auth API pozivi — tanak omotač oko axios-a, odvojen od `client.ts` da bi se
 * lako mock-ovao u testovima `AuthProvider`-a.
 */

export async function login(userName: string, password: string): Promise<LoginResponse> {
  const r = await api.post<LoginResponse>('/auth/login', { userName, password });
  return r.data;
}

export async function refresh(): Promise<LoginResponse> {
  const r = await api.post<LoginResponse>('/auth/refresh', {});
  return r.data;
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
