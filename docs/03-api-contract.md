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

`POST /uredjaji` bez naziva → 400 `{"errors":{"input.Naziv":["Naziv uređaja je obavezan."]}}`.
`POST /uredjaji` sa parom (naziv, oznaka) koji već postoji → 422 `{"greske":["Uređaj „Redler 20-CR-05“ već postoji u registru."]}`.
Isto za korisnike: zauzeto korisničko ime → 422. 409 je rezervisan za konkurentnu izmenu (nema ga u skeletu).

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
| GET | `/auth/me` | Authenticated | `{ user, roles, prava }` (prava iz baze) |
| POST | `/auth/change-password` | Authenticated | `{ currentPassword, newPassword }` → 204; 400 `errors.currentPassword` / `errors.newPassword` |

### `/korisnici` (pravo `korisnici`)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/korisnici?aktivan=` | niz `KorisnikDto` |
| POST | `/korisnici` | `KorisnikInput` + `lozinka` → 201 |
| PUT | `/korisnici/{id}` | `{ ime, email?, rola }` → 200; promena role vraća podrazumevana prava nove role |
| PUT | `/korisnici/{id}/prava` | `{ prava[] }` → 200 `KorisnikDto`; 400 nepoznato pravo; 422 „samo Inženjer popunjava“, „ne možete sebi oduzeti upravljanje korisnicima“; revokuje refresh tokene |
| POST | `/korisnici/{id}/deaktiviraj` · `/aktiviraj` | 200 `KorisnikDto`; 422 guardovi |
| POST | `/korisnici/{id}/reset-lozinka` | `{ novaLozinka }` → 204; 400 politika lozinke |

Guardovi (422): „Ne možete promeniti sopstvenu rolu.", „Ne možete deaktivirati sopstveni nalog.", „Ne možete ukloniti rolu Administrator poslednjem aktivnom administratoru.", „Ne možete deaktivirati poslednjeg aktivnog administratora."

`KorisnikDto` ima i `prava[]`. `LoginResponse` (login, refresh) ima `prava[]`; JWT nosi claim `pravo` (više vrednosti).

### `/sekcije` (RequireKorisnik)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/sekcije` | `[{ id, sifra, naziv }]` po redosledu |

### `/podesavanja` (pravo `podesavanja`)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/podesavanja` | `PodesavanjaDto` (vremena „HH:mm:ss“) |
| PUT | `/podesavanja` | ceo `PodesavanjaDto` → 200; 400 van opsega (npr. granica pauze 1–240 min) |

### `/audit` (RequireAdmin)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/audit?stranica&velicina&korisnik&entitet&akcija&od&do` | `StranicaDto<AuditRedDto>`; sort `created_at desc, id desc` |
| GET | `/audit/entiteti` | `[{ entitet, labela }]` |

### `/stanje-servera` (RequireAdmin)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/stanje-servera` | `{ aplikacija, baza, host }` — vidi blueprint 10 §1 |

### Registar opreme, katalog, šabloni
Čitanje: RequireKorisnik. Izmene registra i kataloga: `Pravo:sabloni.nacrt`. Uvoz i nova dodatna polja: RequireAdmin.

| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/uredjaji?sekcijaId&status&pretraga&bezSablona` | RequireKorisnik | `UredjajDto[]` (sa `sabloni[]` — tipovi važećih verzija koje ga sadrže) |
| GET | `/uredjaji/{id}` | RequireKorisnik | `UredjajDto` / 404 |
| POST · PUT | `/uredjaji` · `/uredjaji/{id}` | sabloni.nacrt | `{ naziv, oznaka, sekcijaId, vrsta }`; 422 duplikat (naziv, oznaka) |
| POST | `/uredjaji/{id}/van-upotrebe` · `/u-upotrebi` | sabloni.nacrt | status umesto brisanja |
| GET | `/aktivnosti` | RequireKorisnik | `AktivnostDto[]` (sa `brojStavki`) |
| POST · PUT | `/aktivnosti` · `/aktivnosti/{id}` | sabloni.nacrt | `{ naziv, odgovorIspravan, odgovorNeispravan }`; 422 promena para odgovora aktivnosti iz važećeg šablona |
| GET | `/dodatna-polja` | RequireKorisnik | `DodatnoPoljeDto[]` |
| POST | `/dodatna-polja` | RequireAdmin | `{ naziv, tip, jedinica, oznake, prikaz, obavezno }` |
| GET | `/sabloni` | RequireKorisnik | šest šablona: `{ tip, vazecaVerzija, vaziOd, brojStavki, brojUredjaja }` |
| GET | `/sabloni/{tip}` · `/sabloni/{tip}/verzije/{broj}` | RequireKorisnik | `SablonVerzijaDto`: sekcije → uređaji → stavke (aktivnost, odgovori, polja, `kljuc`) |
| POST | `/sabloni/uvoz` | RequireAdmin | multipart `fajl` (.xlsx ≤ 5 MB, list „Stavke“) → `{ sabloni[], novihUredjaja, novihAktivnosti }`; 422 `greske[]` „Red N (…): …“ (sve ili ništa), 422 za već uvezen šablon |

### Raspored, kalendar, odsustva (faza 03)
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/raspored?od&do` | RequireKorisnik | `{ pregledi[], neradniDani[], odsustva[] }` — pregledi čiji planirani ili prvobitni dan pada u period |
| GET | `/raspored/inzenjeri` | RequireKorisnik | inženjeri u rotaciji `{ id, ime }` |
| POST | `/raspored/predlog` `{ od }` | raspored.uredjivanje | predlog DP-a do petka te nedelje (ne upisuje) |
| POST | `/raspored/potvrdi` `{ stavke[{datum, tip, inzenjerId}] }` | raspored.uredjivanje | pravi preglede i brojeve; 422 dan nije za DP / DP već postoji / inženjer nije aktivan |
| GET · POST | `/raspored/pz/sledeci` · `/raspored/pz` `{ datum, tip?, inzenjerId, napomena? }` | raspored.uredjivanje | sledeći PZ po redu; zakazivanje („Promeni PZ“ = drugi tip); 422 „Taj PZ je već zakazan za …“ |
| POST | `/raspored/prenesi` | raspored.uredjivanje | ručni prenos današnjeg nezapočetog DP-a (inače pozadinski posao posle kraja radnog dana) |
| PUT | `/pregledi/{id}/dodela` `{ inzenjerId?, datum? }` | raspored.uredjivanje | „Promeni u rasporedu“ |
| POST | `/pregledi/{id}/ucesnici` `{ korisnikId, sekcije[] }` | raspored.uredjivanje | drugi inženjer na PZ-u (prazne sekcije = ukloni) |
| GET · POST · DELETE | `/neradni-dani?godina` · `/neradni-dani` `{ od, do, naziv, vrsta }` · `/neradni-dani/{id}` | RequireKorisnik · neradni_dani | |
| GET | `/odsustva?korisnikId&status` | RequireKorisnik | bez prava odobravanja samo svoja |
| POST | `/odsustva` `{ korisnikId, od, do, vrsta, napomena, prebacivanja[] }` | odsustva.odobravanje | unos (odmah odobreno) sa prebacivanjem; 422 preklapanje, 422 započet pregled |
| POST | `/odsustva/zahtev` `{ od, do, vrsta, napomena }` | pregledi.popunjavanje | zahtev za sebe |
| POST | `/odsustva/{id}/odluka` `{ odobreno, razlog?, prebacivanja[] }` | odsustva.odobravanje | |
| GET | `/odsustva/{korisnikId}/pregledi?od&do` | odsustva.odobravanje | planirani pregledi u periodu + predlog zamene |
| GET · POST | `/zahtevi-prebacivanja?status` · `/zahtevi-prebacivanja` `{ pregledId, razlog, predlozenaZamenaId? }` | RequireKorisnik · pregledi.popunjavanje | svoj planiran pregled; 403 tuđi, 422 započet |
| POST | `/zahtevi-prebacivanja/{id}/odluka` `{ odobreno, zamenaId }` | raspored.uredjivanje | kod PZ-a sa dva inženjera prebacuje se samo deo podnosioca |

### `/_diag` (samo Development)
Rute kojima integracioni testovi dokazuju politike: `/_diag/korisnik` (RequireKorisnik), `/_diag/admin-only` (RequireAdmin).

## Oblici DTO-a

```jsonc
// LoginResponse
{ "accessToken": "eyJ…", "expiresAt": "2026-10-12T09:45:00Z",
  "user": { "id": "…", "userName": "admin", "fullName": "Inicijalni Admin", "email": "admin@tpm-pregledi.elixirgroup.rs" }, "roles": ["Admin"], "prava": ["analitika", "sabloni.nacrt", "neradni_dani", "korisnici", "podesavanja"] }

// KorisnikDto
{ "id": "…", "korisnickoIme": "mpetrovic", "ime": "Marko Petrović", "email": null, "rola": "Inzenjer", "prava": ["pregledi.popunjavanje", "analitika"], "aktivan": true, "poslednjaPrijava": "2026-10-12T07:12:00Z" }
// KorisnikInput (POST)
{ "korisnickoIme": "mpetrovic", "ime": "Marko Petrović", "email": null, "lozinka": "Tajna2026!", "rola": "Inzenjer" }

// UredjajDto
{ "id": 7, "naziv": "Merna vaga Sautelma", "oznaka": "10-C-07C", "sekcijaId": 1, "sekcija": "10", "vrsta": "Merna vaga", "status": "aktivan", "sabloni": ["DP1", "DP2"] }

// StranicaDto<T>
{ "stavke": [ … ], "ukupno": 123, "stranica": 1, "velicina": 50 }

// AuditRedDto
{ "id": 1, "vreme": "…", "korisnik": "admin", "akcija": "UPDATE", "entitet": "Uredjaj", "entitetLabela": "Uređaj",
  "entitetId": "1", "ip": "10.20.30.40", "stareVrednosti": "{…}", "noveVrednosti": "{…}" }

// ServerStanjeDto — blueprint 10 §1
// VersionDto
{ "verzija": "0.1.0", "gitSha": "ab12cd3", "buildUtc": "2026-10-12T09:30:00Z", "okruzenje": "Production" }
```

## Model pristupa (sažetak)

Anonimno: health, version, login, refresh. Svaki ulogovani: me, logout, change-password. Svaka od četiri role: čita sekcije, registar, katalog i šablone. Ostalo po pravu (`docs/04`).
