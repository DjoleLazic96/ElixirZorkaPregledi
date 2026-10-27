/**
 * Verzija frontenda — jedini izvor je `VERSION` fajl u korenu repoa (ili
 * `APP_VERSION`/`GIT_SHA` env u Docker build-u), ubačen kroz `vite.config.ts`.
 * Nikad se ne upisuje ručno (blueprint 11).
 */
export const VERZIJA: string = __APP_VERSION__;
export const GIT_SHA: string = __GIT_SHA__;
