# 02 — Šema baze (PostgreSQL 16)

> Opisuje stvarno stanje generisano migracijama u `backend/src/TpmPregledi.Infrastructure/Persistence/Migrations/`.
> Konvencije: Elixir blueprint 05. Ažurira se u istom PR-u sa svakom migracijom.

## 1. Pregled tabela

| Tabela | Svrha | Jednina kroz |
|---|---|---|
| `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`… | ASP.NET Identity (imena tabela Identity-jeva, kolone snake_case) | Identity |
| `refresh_token` | refresh tokeni (heš) | `ToTable` |
| `audit_log` | audit zapisi | `ToTable` |
| `korisnik_pravo` | štiklirana prava korisnika (R1.6) | `ToTable` |
| `podesavanje` | podešavanja aplikacije (ključ → jsonb) | `ToTable` |
| `brojac` | poslednji redni broj po vrsti i godini (`DP1-2026-0142`, `A-…`, `K-…`) | `ToTable` |
| `sekcija` | sekcije pogona 10…70 (seed u migraciji) | `ToTable` |
| `kategorija` | **PRIMER šifarnika** — briše se u fazi 02 | `ToTable` |

```
AspNetUsers 1──* refresh_token (user_id, CASCADE)
AspNetUsers ·· audit_log.user_id (bez FK — snapshot)
AspNetUsers 1──* korisnik_pravo (korisnik_id, CASCADE)
kategorija (samostalna)
```

## 2. Sistemske tabele (engleske kolone)

### `AspNetUsers` (dodate kolone)
| Kolona | Tip | Napomena |
|---|---|---|
| `full_name` | varchar(200) | ime i prezime za prikaz |
| `is_active` | boolean | deaktiviran korisnik ne može da se prijavi; nikad se ne briše |
| `created_at` | timestamptz | UTC |
| `last_login_at` | timestamptz null | UTC; menja se pri svakoj prijavi (zato audit vidi UPDATE korisnika uz LOGIN) |

### `refresh_token`
| Kolona | Tip | Napomena |
|---|---|---|
| `id` | bigint identity | |
| `user_id` | text FK → AspNetUsers (CASCADE) | |
| `token_hash` | varchar(128) unique | SHA-256 (hex) sirovog tokena; sirovi token je samo u cookie-ju |
| `created_at`, `expires_at` | timestamptz | UTC; istek 7 dana |
| `revoked_at` | timestamptz null | rotacija, logout, reset/promena lozinke, deaktivacija, promena role |
| `created_by_ip` | varchar(64) null | |

### `audit_log`
| Kolona | Tip | Napomena |
|---|---|---|
| `id` | bigint identity | |
| `user_id` | text null | bez FK — preživi sve |
| `user_name` | text null | snapshot; za LOGIN_FAIL uneto ime |
| `action` | varchar(20) | `CREATE`, `UPDATE`, `DELETE`, `LOGIN`, `LOGIN_FAIL`, `LOGOUT`, `START` |
| `entity` | varchar(100) | CLR ime (`Kategorija`), `Auth`, `Sistem` |
| `entity_id` | varchar(64) | PK vrednosti spojene `\|`; `start` za START |
| `old_values`, `new_values` | jsonb null | pun snapshot bez osetljivih polja |
| `created_at` | timestamptz | UTC |
| `ip_address` | varchar(64) null | prava IP samo uz ispravne forwarded hedere |
Indeksi: `ix_audit_log_entity_entity_id`, `ix_audit_log_created_at` (DESC). Append-only.

## 3. Domenske tabele (srpske kolone)

### `korisnik_pravo`
| Kolona | Tip | Napomena |
|---|---|---|
| `korisnik_id` | text, PK, FK → `AspNetUsers` | |
| `pravo` | varchar(64), PK | vrednost iz `Domain/Identity/Prava.cs`; `[Auditable]` |

### `podesavanje`
| Kolona | Tip | Napomena |
|---|---|---|
| `kljuc` | varchar(64), PK | ime svojstva `PodesavanjaDto` u camelCase-u (`granicaPauzeMin`…) |
| `vrednost` | jsonb | ključ koji ne postoji uzima podrazumevanu vrednost iz `PodesavanjaDto.Podrazumevana`; `[Auditable]` |

### `brojac`
| Kolona | Tip | Napomena |
|---|---|---|
| `vrsta` | varchar(8), PK | `DP1`…`PZ4`, `A`, `K` |
| `godina` | int, PK | |
| `poslednji` | int | dodela: `INSERT … ON CONFLICT DO UPDATE … RETURNING` u transakciji zapisa (`BrojacService`) |

### `sekcija`
| Kolona | Tip | Napomena |
|---|---|---|
| `id` | int identity | |
| `sifra` | varchar(8), `ux_sekcija_sifra` | 10, 20, 21, 30, 40, 50, 60, 70 (`HasData`) |
| `naziv` | varchar(100) | „Sekcija 21“ |
| `redosled` | int | |

### `kategorija` — PRIMER
| Kolona | Tip | Napomena |
|---|---|---|
| `id` | int identity | |
| `sifra` | varchar(32) | jedinstvena među aktivnim: `ux_kategorija_sifra WHERE aktivan = true` |
| `naziv` | varchar(200) | |
| `opis` | varchar(1000) null | |
| `aktivan` | boolean | soft delete |
| `created_at`, `created_by`, `updated_at`, `updated_by` | timestamptz / text | puni interceptor; `created_by` = UserId ili `system` |

## 4. Ključne odluke (potvrđeno u kodu)

| Odluka | Razlog |
|---|---|
| `timestamptz` UTC; prikaz u lokalnoj zoni radi browser | jedan izvor istine za vreme |
| Jednina tabela kroz eksplicitan `ToTable` | `EFCore.NamingConventions` samo snake_case-uje imena; default bi bio naziv `DbSet`-a (množina) |
| Sistemske tabele engleske kolone, domenske srpske | Identity i deljeni kod su engleski; domen je srpski |
| Soft delete (`aktivan`) + filtrirani unique | deaktivirani zapis sme da deli šifru sa novim |
| `audit_log.user_id` bez FK, `user_name` snapshot | audit preživi sve |
| `token_hash` umesto sirovog tokena | krađa baze ne daje upotrebljive tokene |
| Nema concurrency tokena | dodaje se po potrebi (blueprint 05 §6) |
| Migracije pri startu | nema ručnog SQL puta; backup pre deploy-a sa migracijom |

## 5. Seed

Role `Inzenjer`, `Supervizor`, `Direktor`, `Admin`; nalozi sa starom rolom `Korisnik` prelaze u `Inzenjer`, a nalog bez ijednog prava
dobija podrazumevana prava svoje role; inicijalni admin iz `Admin:*` (samo ako nijedan admin ne postoji; odbija praznu/`Admin123!`); audit `START`.
Nema seed-a kategorija — dodaj `Application/Common/Seed/<Sta>SeedSource.cs` + poziv u `SeedData` kad domen ima šifarnike (samo ako je tabela prazna).

## 6. Migracije

| Ime | Sadržaj |
|---|---|
| `InitialCreate` | Identity, `refresh_token`, `audit_log`, `kategorija` |
| `Osnova` | `korisnik_pravo`, `podesavanje`, `brojac`, `sekcija` (+ seed sekcija) |
