/** Zamena za `virtual:pwa-register/react` u testovima (virtuelni modul postoji samo u Vite build-u / dev serveru). */
export function useRegisterSW() {
  return { needRefresh: [false, () => {}], offlineReady: [false, () => {}], updateServiceWorker: async () => {} };
}
