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
| `uredjaj` | registar opreme; identitet (naziv, oznaka) | `ToTable` |
| `aktivnost` | katalog aktivnosti (naziv + dva odgovora) | `ToTable` |
| `dodatno_polje` | dodatna polja uz aktivnosti (seed tri polja) | `ToTable` |
| `sablon`, `sablon_verzija`, `sablon_stavka`, `sablon_stavka_polje` | šest šablona, verzije, stavke i polja na stavkama | `ToTable` |
| `sablon_dogadjaj` | istorijat šablona | `ToTable` |
| `fajl` | fajlovi na volumenu (slike, faze 04 i 10) | `ToTable` |

```
AspNetUsers 1──* refresh_token (user_id, CASCADE)
AspNetUsers ·· audit_log.user_id (bez FK — snapshot)
AspNetUsers 1──* korisnik_pravo (korisnik_id, CASCADE)
sekcija 1──* uredjaj; sablon 1──* sablon_verzija 1──* sablon_stavka *──1 uredjaj, *──1 aktivnost
sablon_stavka 1──* sablon_stavka_polje *──1 dodatno_polje; sablon 1──* sablon_dogadjaj
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
| `entity` | varchar(100) | CLR ime (`Uredjaj`), `Auth`, `Sistem` |
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

### `uredjaj`
| Kolona | Tip | Napomena |
|---|---|---|
| `id` | int identity | stalni ID kroz sve verzije šablona |
| `naziv` | varchar(200) | kako piše na obrascu |
| `oznaka` | varchar(100) | prazan string ako obrazac nema oznaku; `ux_uredjaj_naziv_oznaka` (naziv, oznaka) |
| `sekcija_id` | int FK → `sekcija` | |
| `vrsta` | varchar(100) | pri uvozu iz naziva („Merna vaga“, „Transporter“…) |
| `status` | varchar(20) | `aktivan` / `van_upotrebe` (umesto brisanja) |
| `qr_token` | uuid, `ux_uredjaj_qr_token` | QR nalepnica (faza 08) |
| `created_*`, `updated_*` | | puni interceptor; `[Auditable]` |

Ključ je par (naziv, oznaka), ne sama oznaka: DP1, DP2 i PZ1 su prepisani doslovno, pa ista oznaka ponegde ima dva naziva.

### `aktivnost`
| Kolona | Tip | Napomena |
|---|---|---|
| `naziv` | varchar(300) | `ux_aktivnost_naziv_odgovori` (naziv, odgovor_ispravan, odgovor_neispravan) |
| `odgovor_ispravan`, `odgovor_neispravan` | varchar(50) | tačno dva odgovora (R2.1) |
| `uputstvo_tekst`, `uputstvo_azurirao_id`, `uputstvo_azurirano_at` | | faza 10 |

### `dodatno_polje`
| Kolona | Tip | Napomena |
|---|---|---|
| `naziv` | varchar(200), unique | seed: „Broj rolni koje ne okreću“, „Temperatura ležaja“, „Broj nedostajućih / oštećenih komada“ |
| `tip` | varchar(20) | `broj` / `merenje` / `dve_vrednosti` / `da_ne` |
| `jedinica` | varchar(20) null | |
| `oznake` | text[] | oznake vrednosti za `dve_vrednosti` |
| `prikaz` | varchar(20) | `uvek` / `uz_neispravan` |
| `obavezno` | boolean | |

### `sablon`, `sablon_verzija`, `sablon_stavka`, `sablon_stavka_polje`, `sablon_dogadjaj`
- `sablon` — `tip` unique (seed DP1…PZ4).
- `sablon_verzija` — `sablon_id`, `broj` (`ux_sablon_verzija_broj`), `status` (`nacrt`/`objavljena`/`zamenjena`), `vazi_od`, `objavio_id`, `objavljeno_at`; `[Auditable]`. Objavljena se ne menja.
- `sablon_stavka` — `verzija_id`, `sekcija_id`, `uredjaj_id`, `aktivnost_id`, `redosled`, `kljuc uuid` (stalni ID stavke kroz verzije); jedinstveno (verzija, uređaj, aktivnost) i (verzija, ključ).
- `sablon_stavka_polje` — PK (stavka, dodatno polje).
- `sablon_dogadjaj` — `sablon_id`, `verzija_id`, `vrsta` (`prvi_uvoz`…), `opis`, `korisnik_id`, `at`.

### `fajl`
`putanja`, `mime`, `velicina`, `sha256`, `kreirano_at` — za slike (faze 04 i 10).

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
Sekcije, šabloni i tri dodatna polja su seed u migracijama (`HasData`); registar i katalog nastaju prvim uvozom šablona. Ostali šifarnici: `Application/Common/Seed/<Sta>SeedSource.cs` + poziv u `SeedData` (samo ako je tabela prazna).

## 6. Migracije

| Ime | Sadržaj |
|---|---|
| `InitialCreate` | Identity, `refresh_token`, `audit_log`, `kategorija` |
| `Osnova` | `korisnik_pravo`, `podesavanje`, `brojac`, `sekcija` (+ seed sekcija) |
| `OpremaISabloni` | registar, katalog, dodatna polja, šabloni i verzije, istorijat, `fajl`; briše `kategorija` |
