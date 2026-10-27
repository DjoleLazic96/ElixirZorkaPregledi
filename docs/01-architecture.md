# 01 — Arhitektura

> Opisuje **stvarno stanje koda** (`backend/src/*`, `frontend/src/*`), ne plan. Standard: Elixir blueprint, dokumenti 01, 04, 13.
> Menja se u istom PR-u sa kodom koji menja arhitekturu.

## 1. Pregled

```
  korisnik (Chrome/Edge, VPN) ──HTTPS──► [Traefik, IT] ──proxy-net──► frontend (nginx :8080, React build)
                                                                          │ /api/* → tpm-pregledi-backend:8080 (tpm-pregledi-internal)
                                                                          ▼
                                                                    backend (ASP.NET Core 10) ──► db (PostgreSQL 16 :5432)
```
- Jedan origin: browser razgovara samo sa nginx-om; API je na `/api/v1`. Backend i baza su samo u internoj mreži.
- Stateless backend: sve stanje je u Postgres-u; lokalno su samo Serilog fajlovi i Data Protection ključevi (`data/`).
- TLS terminira Traefik; backend čita `X-Forwarded-For/-Proto` kroz dva hopa (`ForwardLimit = 2`).

## 2. Backend — Clean Architecture sa vertikalnim slajsovima

| Projekat | Odgovornost | Ključni fajlovi |
|---|---|---|
| `TpmPregledi.Domain` | entiteti i bazne klase; Identity tipovi; konstante rola | `Common/AuditableEntity.cs`, `SoftDeletableEntity.cs`, `AuditableAttribute.cs`; `Entities/Kategorija.cs`, `AuditLog.cs`; `Identity/ApplicationUser.cs`, `RefreshToken.cs`, `Roles.cs` |
| `TpmPregledi.Application` | komande/upiti/handleri po feature-u; interfejsi; validacija; izuzeci; paginacija | `Features/{Auth,Korisnici,Audit,ServerStanje,Version,Kategorije}/`; `Common/Behaviors/ValidationBehavior.cs`; `Common/Exceptions/AppExceptions.cs`; `Common/Pagination/StranicaDto.cs`; `Common/DatumHelper.cs`; `Common/Interfaces/*` |
| `TpmPregledi.Infrastructure` | EF Core + Postgres; Identity/JWT; audit; hardver; vreme; verzija | `Persistence/AppDbContext.cs`, `Interceptors/AuditSaveChangesInterceptor.cs`, `Migrations/`, `Seed/SeedData.cs`; `Identity/JwtTokenService.cs`, `CurrentUserService.cs`; `Audit/AuditDogadjaji.cs`; `ServerStanje/*`; `Time/SystemDateTimeProvider.cs`; `Version/AppVersion.cs`; `DependencyInjection.cs` |
| `TpmPregledi.Api` | kompozicioni koren, endpointi, middleware | `Program.cs`; `Endpoints/*Endpoints.cs`; `Middleware/ExceptionHandlingMiddleware.cs` |

Zavisnosti: `Api → Application, Infrastructure, Domain`; `Infrastructure → Application, Domain`; `Application → Domain`. Domain ne zna za EF ni ASP.NET.
Feature = folder `Features/<Feature>/{Commands,Queries,Dtos}`; jedan fajl = `sealed record` komanda + validator + handler. Obrazac: `Features/Kategorije/`.

## 3. Tok zahteva

```
HTTP → ForwardedHeaders → HSTS → sigurnosni hederi → Serilog request log → ExceptionHandlingMiddleware (RFC 7807)
    → CORS → RateLimiter → AuthN (JWT) → AuthZ (politike) → endpoint (RequireAuthorization)
        → IMediator.Send → ValidationBehavior (400) → Handler (BusinessRuleException → 422)
            → SaveChangesAsync → AuditSaveChangesInterceptor (audit red + CreatedAt/By, UpdatedAt/By, ista transakcija)
    ← DTO (camelCase JSON, enumi camelCase stringovi)
```

## 4. Autentifikacija i autorizacija

- Identity + JWT HS256 (15 min, claims `sub`, `jti`, `preferred_username`, `role`) u telu odgovora; frontend ga drži u memoriji.
- Refresh token: 256 random bita, 7 dana, SHA-256 heš u `refresh_token.token_hash`, rotacija pri svakom refresh-u, cookie `tpm-pregledi.refresh`
  (HttpOnly, Secure, SameSite=Strict, Path `/api/v1/auth`). Revokacija svih tokena pri reset/promeni lozinke, deaktivaciji, promeni role.
- Lockout 5/10 min. Rate limit: login 10/min/IP, refresh 30/min/IP, globalno 200/min/IP.
- Fail-fast pri startu: JWT secret ≥ 32 bajta i bez `REPLACE_ME`; `Cors:Origins` u produkciji; admin lozinka ne prazna/default.
- Role `Admin`, `Korisnik`; politike `RequireAdmin`, `RequireKorisnik` (Admin ili Korisnik). Detalji: `04-roles-and-permissions.md`.

## 5. Cross-cutting

| Šta | Gde | Napomena |
|---|---|---|
| Validacija | `ValidationBehavior` | `ValidationFailedException` → 400 `errors{}` |
| Greške | `ExceptionHandlingMiddleware` | tabela u `03-api-contract.md`; `traceId` = `Activity.Current.TraceId` (W3C) — isti u Serilog `{TraceId}` |
| Audit | `AuditSaveChangesInterceptor`, `AuditDogadjaji` | CRUD na `[Auditable]`; LOGIN/LOGIN_FAIL/LOGOUT; START pri pokretanju |
| Logovanje | Serilog | konzola + `logs/log-YYYYMMDD.txt` (30 dana) |
| Stanje servera | `ServerStanjeService` + `IHardwareInfo` | aplikacija / baza (+ poslednji backup) / host |
| Verzija | `AppVersion` | `InformationalVersion` → `/api/v1/version`, start log, audit START |
| Data Protection | `AddInfrastructure` | `SetApplicationName("tpm-pregledi").PersistKeysToFileSystem(DataProtection:KeysPath)` |
| Health | `HealthEndpoints` | `/health`, `/health/live`, `/health/ready` (503 bez baze) |

## 6. Frontend

React 19 + TS + Vite 8 + Tailwind 4; React Router 7; TanStack Query 5; axios. `src/`: `api/` (klijent + tipovi), `app/` (rute, layout,
provideri), `components/` (ui kit + Zasticeno, ErrorBoundary, NijePronadjeno, OAplikaciji, PromenaLozinke), `features/` (auth, kategorije —
PRIMER, admin/korisnici, audit-log, stanje-servera), `lib/` (cn, permissions, apiError, datum, broj, period, download, verzija).
Backend je autoritativan za pristup; frontend ogleda (`Zasticeno` na svakoj ruti, `NAV visibleIf`). Dev: Vite proxy `/api` → `:8080`.

## 7. Dualnost imenovanja

| Sloj | Pismo |
|---|---|
| kod, rute, tabele/kolone (domenske srpski, sistemske engleski), enum vrednosti, imena fajlova | ASCII |
| sav tekst za korisnika (UI, poruke iz backenda, labele, uputstva, CHANGELOG) | puna dijakritika Š Ć Č Ž Đ |

## 8. Start i seed

`MigrateAndSeedAsync`: `MigrateAsync` → role → inicijalni admin (iz `Admin:*`; odbija praznu/default lozinku) → audit `START`.
Greška → `Log.Fatal` + exit 1.

## 9. Lokalni razvoj

Privatni Postgres `.local-pg-data` na `55434` (`scripts/dev-db-init.cmd`), `dotnet run --project src/TpmPregledi.Api` (:8080, Scalar na `/swagger`),
`npm run dev` (:5173). Dev nalog `admin` / `DevTpmPregledi2026!`.

## 10. Deploy

`deploy/README.md`. Compose u korenu, Traefik `proxy-net`, `data/` jedini bind mount, image kao tar, `deploy/scripts/deploy.sh`.
