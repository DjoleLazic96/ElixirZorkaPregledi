# TPM pregledi

> Interna aplikacija za Elixir. Prati **Elixir blueprint** (standard za sve Elixir aplikacije):
> https://github.com/casper96kzs/elixir-blueprint . Uputstvo važi za verziju u `VERSION`; istorija u `CHANGELOG.md`.

<!-- POSLE preimenuj.ps1: popuni sekcije „Šta aplikacija radi", „Role" i „Deploy / produkcija". -->

## Šta aplikacija radi

<!-- 3–6 rečenica običnim jezikom: ko je koristi, koji proces pokriva, šta je rezultat. -->

### Osnovni pojmovi

| Pojam | Značenje |
|---|---|
| Kategorija | *(primer šifarnika iz tpm-pregledia — zameniti domenskim pojmovima)* |

## Ključne funkcije

- Prijava korisničkim imenom i lozinkom; promena sopstvene lozinke.
- Administracija korisnika (role, deaktivacija, reset lozinke) — samo Admin.
- Audit log: ko je, kada, šta i odakle promenio; prijave i odjave; pokretanja aplikacije.
- Stanje servera: aplikacija, baza, host, poslednji backup — samo Admin.
- „O aplikaciji": verzija, build, okruženje.
- Šifarnik **Kategorije** — *primer feature-a razrađen od baze do uputstva; kopiraj ili obriši.*

## Role

| Rola | Labela u UI | Šta sme |
|---|---|---|
| `Admin` | Administrator | sve, uključujući korisnike, audit log, stanje servera, izmene šifarnika |
| `Korisnik` | Korisnik | čita šifarnike i radi u aplikaciji; ne menja šifarnike ni korisnike |

Detalji: `docs/04-roles-and-permissions.md`.

## Tehnologije

.NET 10 Minimal API · EF Core 10 + PostgreSQL 16 (snake_case) · MediatR · FluentValidation · Serilog · ASP.NET Identity + JWT ·
React 19 · TypeScript 6 · Vite 8 · Tailwind 4 · React Router 7 · TanStack Query 5 · Vitest · xUnit + Testcontainers · Docker + Traefik.
Tačne verzije: `backend/Directory.Packages.props`, `frontend/package.json`.

## Struktura projekta

```
backend/   TpmPregledi.Domain | Application | Infrastructure | Api  (+ tests/)
frontend/  React SPA (src/features/<feature>, src/components/ui, src/lib)
deploy/    README (operativno), scripts/ (export-images.sh, deploy.sh, backup.sh)
docs/      01-architecture · 02-database-schema · 03-api-contract · 04-roles-and-permissions · odluke.md · deploy/ · user/
scripts/   dev-db-init.cmd, dev-db-start.cmd, dev-db-stop.cmd
```

## Brzi start (lokalno)

Preduslovi: .NET 10 SDK, Node 22+, PostgreSQL 16 binariji (`C:\Program Files\PostgreSQL\16\bin`, servis nije potreban), Git;
Docker Desktop za integracione testove i build image-a.

```powershell
scripts\dev-db-init.cmd                                # jednom: privatni Postgres na 55434
scripts\dev-db-start.cmd                               # svaki dan
dotnet run --project backend\src\TpmPregledi.Api            # http://localhost:8080/api/v1 (migracije + seed)
cd frontend; npm install; npm run dev                  # http://localhost:5173
```

| Nalog | Lozinka | Rola |
|---|---|---|
| `admin` | `DevTpmPregledi2026!` | Admin |

Dev baza: `localhost:55434`, `tpm-pregledi` / `tpm-pregledi` / `tpm-pregledi-dev-only` (javna dev lozinka). Scalar UI: `http://localhost:8080/swagger`.

## Testiranje

```powershell
cd backend; dotnet test                                # unit + integracioni (Testcontainers → Docker)
dotnet test tests\TpmPregledi.Application.UnitTests         # samo unit (bez Docker-a)
cd frontend; npm run lint; npm test
```

## Dokumentacija

- Tehnička: `docs/01-architecture.md`, `02-database-schema.md`, `03-api-contract.md`, `04-roles-and-permissions.md`, `odluke.md`
- Deploy: `deploy/README.md`, `docs/deploy/`
- Korisnička: `docs/user/README.md`
- Istorija izmena: `CHANGELOG.md`

## Deploy / produkcija

| | |
|---|---|
| **Vlasnik / kontakt** | <!-- ime i mejl vlasnika projekta --> |
| **Svrha** | <!-- jedna rečenica --> |
| **URL** | `https://tpm-pregledi.elixirgroup.rs/` (preko VPN-a) |
| **Host / folder** | Elixir docker host, `/opt/apps/tpm-pregledi/` |
| **Zavisnosti** | PostgreSQL 16 (u stacku); nema spoljnih servisa |
| **Health** | `GET /api/v1/health/ready` → `{"status":"ready"}` |
| **Verzija** | `GET /api/v1/version`; UI → korisnički meni → „O aplikaciji" |
| **Rollback** | `IMAGE_TAG=<prethodni> docker compose up -d` (+ restore dump-a ako je bilo migracije) |
| **Postupak** | `deploy/README.md` |

## Konvencije

- Kod, rute, kolone, enum vrednosti: srpski ASCII. Sav tekst za korisnika: puna dijakritika Š Ć Č Ž Đ.
- Vreme u bazi `timestamptz` UTC; poslovni datumi `date`.
- Matični podaci se ne brišu, deaktiviraju se (`aktivan`).
- Greške: 400 validacija (`errors`), 422 poslovno pravilo (`greske[]`).
- Sve ostalo: Elixir blueprint.
