# TPM pregledi — uputstvo za AI asistenta

> Ovaj repo prati **Elixir blueprint** (standard za sve Elixir aplikacije):
> https://github.com/casper96kzs/elixir-blueprint — dokumenti `01–20`, checkliste i `skeleton/` iz kog je ovaj
> repo nastao. Blueprint je autoritativan za tehnologiju, arhitekturu, sigurnost, UI, deploy i proces; ovde su samo
> stvari specifične za ovaj projekat. Ako predlažeš odstupanje — zapiši ga u `docs/odluke.md` kao predlog, ne
> implementiraj tiho.

## Projekat

| Šta | Vrednost |
|---|---|
| Naziv | TPM pregledi |
| Vlasnik | Miroslav Zec, direktor održavanja (mejl: dopuniti) |
| Git root | ovaj folder; grana `main` |
| Backend | `backend/` — .NET 10, `TpmPregledi.{Domain,Application,Infrastructure,Api}` + `tests/` |
| Frontend | `frontend/` — React 19 + TS + Vite 8 + Tailwind 4 |
| Tehnički docs | `docs/01–04`, `docs/odluke.md`, `docs/deploy/` |
| Korisnički docs | `docs/user/` |
| Spec/planovi | `docs/superpowers/specs/`, `docs/superpowers/plans/` |
| Radni dnevnik | `TODO.md` (status je OVDE, ne u planovima) |
| Produkcija | `https://tpm-pregledi.elixirgroup.rs/`, host `/opt/apps/tpm-pregledi/`, Traefik `proxy-net`; detalji u `deploy/README.md` |

## Komande

```powershell
scripts\dev-db-init.cmd                                   # jednom: privatni Postgres na 55434
scripts\dev-db-start.cmd                                  # start klastera
dotnet run --project backend\src\TpmPregledi.Api               # API na http://localhost:8080 (migracije + seed automatski)
cd frontend; npm run dev                                  # http://localhost:5173 (proxy /api → 8080)
cd backend; dotnet test                                   # unit + integracioni (integracioni traže Docker)
cd frontend; npm run lint; npm test
bash deploy/scripts/export-images.sh                      # build image-a u tar (treba Docker)
```

- Dev nalozi: `admin` / `DevTpmPregledi2026!` (Admin). Dev DB: `tpm-pregledi` / `tpm-pregledi` / `tpm-pregledi-dev-only` na `localhost:55434`.
- Scalar UI: `http://localhost:8080/swagger` (samo Development).
- Nova migracija: `cd backend; dotnet ef migrations add <Opis> --project src/TpmPregledi.Infrastructure --startup-project src/TpmPregledi.Api --output-dir Persistence/Migrations` (`dotnet tool restore` prvi put).

## Pravila projekta (uz blueprint)

1. **Sav user-facing tekst** (UI, poruke iz backenda, seed labele, uputstva, CHANGELOG) **puna srpska dijakritika Š Ć Č Ž Đ**. Kod, rute, kolone, fajlovi ASCII.
2. Komentari i XML doc objašnjavaju **zašto**; jednostavno > pametno; bez preuranjenih apstrakcija.
3. Docs (`docs/0x`, `docs/user`, `CHANGELOG [Neobjavljeno]`) se menjaju **u istom PR-u** sa kodom.
4. Validacija oblika → validator (400); poslovno pravilo → `BusinessRuleException` (422, `greske[]`).
5. Vreme kroz `IDateTimeProvider`; datumi `DateOnly`; frontend datumi/brojevi samo kroz `lib/datum.ts`, `lib/broj.ts`.
6. Novi feature = kopija obrasca `Kategorije` (backend `Features/Kategorije`, frontend `features/kategorije`), ne izmišljanje novog.
7. Forme su prezentacione; roditelj drži mutacije i toastove; `npm run lint` mora biti 0.
8. Ne push-uj u `main`, ne deploy-uj, ne diraj produkciju ni tajne — to radi čovek.
9. Testovi po blueprint dokumentu 16: pravilo koje se krši, 401/403 po ruti, guard rute na frontu.

## Šta ne dirati

- `data/`, `.env`, `*.tar` — produkcioni artefakti (gitignored).
- `backend/src/TpmPregledi.Infrastructure/Persistence/Migrations/*` već deploy-ovane — samo nova migracija.
- `preimenuj.ps1` je trebalo da bude obrisan posle inicijalizacije; ako postoji, ne pokretati ponovo.

## Projektno specifično

- **Dizajn:** `docs/superpowers/specs/2026-10-27-tpm-pregledi-design.md` — Prilog A su funkcionalna pravila (R1.1…); kad kod i Prilog A ne odgovaraju, pitaj, ne biraj sam.
- **Odstupanja od blueprinta** (mobilni prikaz/PWA, rad bez interneta, fotografije, push, četiri role + prava, duža sesija,
  dodatne biblioteke) su u `docs/odluke.md` kao PREDLOG — ne implementirati dok nisu odobrena.
- **Van repoa** (folder iznad): mockup `2026-10-26 TPM mockup.html` (svi ekrani), `Analiza napomena DP-PZ.xlsx`
  (list „Predlog polja" i „Ručne ispravke šablona"), `2026-10-20 Aplikacija.docx` (istorija dogovora). Stari prototip
  `TPM · Pregledi (DP _ PZ).html` se ne koristi.
- **Pojmovi:** DP1/DP2 (dnevni), PZ1–PZ4 (planski zastoj), šablon → sekcija → uređaj → aktivnost (dva odgovora), kvar `K-`,
  akcija `A-`, pregled `DP1-2026-0142` (dorada ne menja broj).
- **Zamke:** sve operacije sa telefona su idempotentne (`opId`); vreme odgovora je sa telefona (proveriti odstupanje sata);
  započet pregled ostaje na svojoj verziji šablona; pregled se nikad ne briše (poništava se); „nije u funkciji" se ne prenosi.
- Sledeća faza: modul za pumpe i trake (nije deo DP/PZ).
