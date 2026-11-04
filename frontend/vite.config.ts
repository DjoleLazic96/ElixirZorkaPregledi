import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { VitePWA } from 'vite-plugin-pwa';

/**
 * Verzija aplikacije (Elixir blueprint 11). Jedini izvor je VERSION u korenu repoa.
 * U Docker build-u fajl nije u kontekstu, pa Dockerfile prosleđuje APP_VERSION/GIT_SHA kao env.
 */
function procitajVerziju(): string {
  if (process.env.APP_VERSION) return process.env.APP_VERSION;
  try {
    // `import.meta.dirname` umesto `__dirname`: Vite-ov native config loader ne podržava CJS globale.
    return readFileSync(resolve(import.meta.dirname, '../VERSION'), 'utf8').trim();
  } catch {
    return '0.0.0-dev';
  }
}

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    // PWA (odluka 2026-11-04): instalacija na početni ekran i app shell bez interneta. API se nikad ne kešira
    // u service worker-u — podaci za rad bez interneta su u IndexedDB (src/offline).
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg'],
      manifest: {
        name: 'TPM pregledi',
        short_name: 'TPM pregledi',
        lang: 'sr-Latn',
        start_url: '/',
        display: 'standalone',
        background_color: '#ffffff',
        theme_color: '#006937',
        icons: [{ src: '/favicon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' }],
      },
      workbox: {
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api\//],
        globPatterns: ['**/*.{js,css,html,svg,woff2}'],
        // Web Push (odluka 2026-11-04, faza 09): prikaz obaveštenja i klik koji otvara rutu.
        importScripts: ['push-sw.js'],
      },
    }),
  ],
  define: {
    __APP_VERSION__: JSON.stringify(procitajVerziju()),
    __GIT_SHA__: JSON.stringify(process.env.GIT_SHA ?? 'dev'),
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
  build: {
    sourcemap: false, // produkcioni bundle bez source mapa (blueprint 12 #37)
  },
  server: {
    port: 5173,
    proxy: {
      // Dev-only: isti origin kao u produkciji (nginx), pa refresh cookie radi prirodno.
      '/api': { target: 'http://localhost:8080', changeOrigin: true, secure: false },
    },
  },
});
