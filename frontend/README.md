# TPM pregledi — frontend

React 19 + TypeScript + Vite 8 + Tailwind 4 SPA. Prati Elixir blueprint (13–16).
API klijent cilja `/api/v1` na istom origin-u (dev: Vite proxy → `http://localhost:8080`).

## Skripte

| Komanda | Šta radi |
|---|---|
| `npm install` | instalira zavisnosti (Node ≥ 22) |
| `npm run dev` | dev server na http://localhost:5173 sa proxy-jem ka backendu |
| `npm run lint` | ESLint (0 grešaka je uslov za merge) |
| `npm test` | Vitest, jedan prolaz (CI) |
| `npm run test:watch` | Vitest u watch režimu |
| `npm run build` | `tsc -b && vite build` → `dist/` |
| `npm run preview` | servira `dist/` lokalno |

## Struktura `src/`

- `api/` — axios klijent (token u memoriji, 401 → refresh → retry) i ručno pisani DTO tipovi
- `app/` — rute i guardovi (`AppRoutes`), layout (sidebar + header), provideri (Query, Toast, Confirm)
- `components/ui/` — UI kit (Button, Input, Select, Modal, DatePicker, Pagination, Tabs…)
- `components/` — ErrorBoundary, O aplikaciji, Promena lozinke, 404, Zasticeno
- `features/` — po jedan folder po funkcionalnosti (`XxxPage`, `XxxForm`, `hooks.ts`, testovi pored koda).
  `features/kategorije/` je **primer feature-a** — obrazac za sve ostale; kopiraj ili obriši.
- `lib/` — čiste funkcije: `datum`, `broj`, `period`, `permissions`, `apiError`, `download`, `verzija`

Verzija aplikacije dolazi iz `VERSION` fajla u korenu repoa (`vite.config.ts` → `__APP_VERSION__`);
u Docker build-u iz `APP_VERSION`/`GIT_SHA` promenljivih.
