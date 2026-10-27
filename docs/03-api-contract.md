# 03 — API ugovor (v1)

> Čitano iz `backend/src/TpmPregledi.Api/Endpoints/*.cs` — stvarno stanje. Konvencije: Elixir blueprint 06.

## Konvencije

- Osnova `/api/v1`; JSON camelCase; enumi camelCase stringovi; `Authorization: Bearer <jwt>`; refresh preko cookie-ja `tpm-pregledi.refresh` (Path `/api/v1/auth`).
- Greške RFC 7807 `{ type, title, status, detail, traceId, errors?, greske? }`: **400** = validacija oblika (`errors{ Polje: [poruke] }`),
  **422** = poslovno pravilo (`greske: [rečenice]`), 401 ista poruka za sve razloge, 403, 404, 409, 429, 500 (`detail` sa kodom za podršku).
- Paginacija: `?stranica=1&velicina=50&sort=<polje>&smer=asc|desc&pretraga=` → `{ stavke, ukupno, stranica, velicina }`; `velicina` max 200.
- Datumi: trenuci ISO UTC `Z`; dani `YYYY-MM-DD`.
- Politike: `RequireAdmin` (Admin), `RequireKorisnik` (Admin ili Korisnik), `Authenticated` (bilo ko ulogovan), anonimno.

## Kako radi 400 vs 422

`POST /kategorije` sa praznom šifrom → 400 `{"errors":{"sifra":["Šifra je obavezna."]}}` (ključevi camelCase kao u JSON-u).
`POST /kategorije` sa šifrom koja već postoji (aktivna) → 422 `{"greske":["Kategorija sa ovom šifrom već postoji."]}`.
Isto za korisnike: zauzeto korisničko ime → 422. 409 je rezervisan za konkurentnu izmenu (nema ga u tpm-preglediu).

## Rute

### `/health`, `/version` (anonimno, bez rate limita)
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/health` | anonimno | `{ "status": "ok" }` |
| GET | `/health/live` | anonimno | `{ "status": "live" }` |
| GET | `/health/ready` | anonimno | `{ "status": "ready" }` ili 503 `Baza nedostupna` (`detail` = tip izuzetka) |
| GET | `/version` | anonimno | `{ verzija, gitSha, buildUtc, okruzenje }` |

### `/auth`
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| POST | `/auth/login` | anonimno, `auth-login` 10/min | `{ userName, password }` → `LoginResponse` + Set-Cookie |
| POST | `/auth/refresh` | cookie, `auth-refresh` 30/min | rotacija → `LoginResponse` |
| POST | `/auth/logout` | Authenticated | revokacija + brisanje cookie-ja → 204 |
| GET | `/auth/me` | Authenticated | `{ user, roles }` |
| POST | `/auth/change-password` | Authenticated | `{ currentPassword, newPassword }` → 204; 400 `errors.currentPassword` / `errors.newPassword` |

### `/korisnici` (RequireAdmin)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/korisnici?aktivan=` | niz `KorisnikDto` |
| POST | `/korisnici` | `KorisnikInput` + `lozinka` → 201 |
| PUT | `/korisnici/{id}` | `{ ime, email?, rola }` → 200 |
| POST | `/korisnici/{id}/deaktiviraj` · `/aktiviraj` | 200 `KorisnikDto`; 422 guardovi |
| POST | `/korisnici/{id}/reset-lozinka` | `{ novaLozinka }` → 204; 400 politika lozinke |

Guardovi (422): „Ne možete promeniti sopstvenu rolu.", „Ne možete deaktivirati sopstveni nalog.", „Ne možete ukloniti rolu Administrator poslednjem aktivnom administratoru.", „Ne možete deaktivirati poslednjeg aktivnog administratora."

### `/audit` (RequireAdmin)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/audit?stranica&velicina&korisnik&entitet&akcija&od&do` | `StranicaDto<AuditRedDto>`; sort `created_at desc, id desc` |
| GET | `/audit/entiteti` | `[{ entitet, labela }]` |

### `/stanje-servera` (RequireAdmin)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/stanje-servera` | `{ aplikacija, baza, host }` — vidi blueprint 10 §1 |

### `/kategorije` — PRIMER
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/kategorije?stranica&velicina&pretraga&sort=sifra\|naziv&smer&aktivan` | RequireKorisnik | `StranicaDto<KategorijaDto>` |
| GET | `/kategorije/{id}` | RequireKorisnik | `KategorijaDto` / 404 |
| POST | `/kategorije` | RequireAdmin | `KategorijaInput` → 201 + Location |
| PUT | `/kategorije/{id}` | RequireAdmin | `KategorijaInput` → 200 |
| POST | `/kategorije/{id}/deaktiviraj` · `/aktiviraj` | RequireAdmin | 200 |

### `/_diag` (samo Development)
Rute kojima integracioni testovi dokazuju politike: `/_diag/korisnik` (RequireKorisnik), `/_diag/admin-only` (RequireAdmin).

## Oblici DTO-a

```jsonc
// LoginResponse
{ "accessToken": "eyJ…", "expiresAt": "2026-10-12T09:45:00Z",
  "user": { "id": "…", "userName": "admin", "fullName": "Inicijalni Admin", "email": "admin@tpm-pregledi.elixirgroup.rs" }, "roles": ["Admin"] }

// KorisnikDto
{ "id": "…", "korisnickoIme": "mpetrovic", "ime": "Marko Petrović", "email": null, "rola": "Korisnik", "aktivan": true, "poslednjaPrijava": "2026-10-12T07:12:00Z" }
// KorisnikInput (POST)
{ "korisnickoIme": "mpetrovic", "ime": "Marko Petrović", "email": null, "lozinka": "Tajna2026!", "rola": "Korisnik" }

// KategorijaDto
{ "id": 1, "sifra": "MOT", "naziv": "Motori", "opis": null, "aktivan": true, "createdAt": "…", "updatedAt": null }
// KategorijaInput
{ "sifra": "MOT", "naziv": "Motori", "opis": null }

// StranicaDto<T>
{ "stavke": [ … ], "ukupno": 123, "stranica": 1, "velicina": 50 }

// AuditRedDto
{ "id": 1, "vreme": "…", "korisnik": "admin", "akcija": "UPDATE", "entitet": "Kategorija", "entitetLabela": "Kategorija",
  "entitetId": "1", "ip": "10.20.30.40", "stareVrednosti": "{…}", "noveVrednosti": "{…}" }

// ServerStanjeDto — blueprint 10 §1
// VersionDto
{ "verzija": "0.1.0", "gitSha": "ab12cd3", "buildUtc": "2026-10-12T09:30:00Z", "okruzenje": "Production" }
```

## Model pristupa (sažetak)

Anonimno: health, version, login, refresh. Svaki ulogovani: me, logout, change-password. `Korisnik`: čita kategorije. `Admin`: sve.
