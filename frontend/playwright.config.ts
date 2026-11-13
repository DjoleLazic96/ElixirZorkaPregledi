import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { defineConfig, devices } from '@playwright/test';

/** Build za testove ide van repoa: lokalno je repo na Dropbox-u, koji zaključava `dist` dok ga sinhronizuje. */
const IZLAZ = join(tmpdir(), 'tpm-pregledi-e2e-dist');

/**
 * Testovi kroz pravi pregledač (zamerka 20): prijava, meni i zaštita ruta po rolama, i ceo tok inženjera na telefonu —
 * započni, popuni i predaj bez interneta, pa slanje kad se veza vrati, i Supervizor vidi predat pregled.
 *
 * Traže API na http://localhost:8080 (Development, prazna baza) — u CI-ju ga pokreće posao „E2E“, lokalno
 * `dotnet run --project backend/src/TpmPregledi.Api`. Testira se produkcijski build (`vite preview` na 4173, isti PWA i
 * lenje učitavanje kao na serveru), koji /api prosleđuje na API.
 * Prijava je ograničena na 10 u minuti po IP adresi, pa se svaka rola prijavi samo jednom (`priprema.setup.ts`).
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  use: {
    baseURL: 'http://localhost:4173',
    locale: 'sr-Latn-RS',
    timezoneId: 'Europe/Belgrade',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'priprema', testMatch: /priprema\.setup\.ts/ },
    { name: 'racunar', testMatch: /(prijava|role)\.spec\.ts/, dependencies: ['priprema'], use: { ...devices['Desktop Chrome'] } },
    // Telefon: inženjer popunjava pregled (Pixel 7 = Chromium, isti pregledač kao na službenim telefonima).
    { name: 'telefon', testMatch: /pregled\.spec\.ts/, dependencies: ['priprema'], use: { ...devices['Pixel 7'] } },
  ],
  webServer: {
    command: `npx vite build --outDir "${IZLAZ}" --emptyOutDir && npx vite preview --outDir "${IZLAZ}" --port 4173 --strictPort`,
    url: 'http://localhost:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 300_000,
  },
});
