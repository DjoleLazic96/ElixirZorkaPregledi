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
| `predlog_izmene` | predlozi izmena šablona (R2.8) | `ToTable` |
| `fajl` | fajlovi na volumenu (slike, faze 04 i 10) | `ToTable` |
| `neradni_dan` | neradni dani i praznici | `ToTable` |
| `odsustvo` | odsustva i zahtevi za odsustvo | `ToTable` |
| `pregled`, `pregled_ucesnik`, `pregled_dogadjaj` | pregledi (DP/PZ), drugi inženjer na PZ-u, istorija | `ToTable` |
| `zahtev_prebacivanja` | zahtevi inženjera: prebacivanje pregleda i raniji početak PZ-a | `ToTable` |
| `odgovor`, `uredjaj_van_funkcije`, `fotografija` | odgovori na stavke, „nije u funkciji“, slike uz stavke | `ToTable` |
| `sinhronizacija_operacija` | primljene operacije sa klijenta (idempotentnost po `op_id`) | `ToTable` |
| `sporan_unos` | operacije stigle posle prebacivanja/poništavanja (R6.8) | `ToTable` |
| `zahtev_dorade` | zahtevi inženjera za doradu (R6.6) | `ToTable` |
| `akcija`, `akcija_dogadjaj` | potrebne akcije i njihova istorija (R7.x) | `ToTable` |
| `kvar` | kvarovi na stavkama, otvaraju se i zatvaraju iz predaje (R8.7) | `ToTable` |
| `obavestenje` | zvono i red za isporuku push-a i mejla (R9.1) | `ToTable` |
| `push_pretplata` | Web Push pretplate uređaja | `ToTable` |

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

### `neradni_dan`, `odsustvo`, `zahtev_prebacivanja`
- `neradni_dan` — `datum` unique (`ux_neradni_dan_datum`), `naziv`, `vrsta` (`drzavni`/`verski`/`fabrika`), `uneo_id`; `[Auditable]`.
- `odsustvo` — `korisnik_id` FK, `od`, `do` (date), `vrsta` (`godisnji`/`bolovanje`/`slobodan_dan`/`sluzbeni_put`/`ostalo`), `napomena`, `status` (`zahtev`/`odobreno`/`odbijeno`/`otkazano` — otkazano se ne računa u rasporedu), `zatrazio_id`, `odlucio_id`, `razlog_odbijanja`, `kreirano_at`, `odluceno_at`; `[Auditable]`.
- `zahtev_prebacivanja` — `vrsta` (`prebacivanje` | `raniji_pocetak`, podrazumevano `prebacivanje`, migracija `ZahtevVrsta`), `pregled_id`, `podneo_id`, `predlozena_zamena_id`, `razlog`, `status`, `zamena_id`, `odlucio_id`, vremena; `[Auditable]`.

### `pregled`, `pregled_ucesnik`, `pregled_dogadjaj`
- `pregled` — `broj` unique (`DP1-2026-0142`, iz `brojac` po godini planiranog datuma), `tip`, `sablon_verzija_id` null (vezuje se pri početku, faza 04), `planirani_datum`, `prvobitni_datum`, `status` (`planiran`/`u_radu`/`ceka_overu`/`overen`/`vracen_na_doradu`/`ponisten`), `dodeljen_id` FK, `napomena`, `kreirano_at`; `[Auditable]`. Kolone popunjavanja dodaje faza 04.
- `pregled_ucesnik` — PK (pregled, korisnik), `sekcije text[]` (PZ sa dva inženjera).
- `pregled_dogadjaj` — `pregled_id`, `vrsta` (`dodela`, `prebacivanje`, `prenos`, `promena_pz`, `ucesnik`, `izmena_rasporeda`), `podaci jsonb` (staro/novo), `korisnik_id` (null = sistem), `at`.

`pregled` dobija u fazi 04: `zapocet_at`, `predat_at`, `aktivno_sekundi`, `delova`, `dorada`; u fazi 05 `uredjaj_klijenta` (telefon koji je započeo); u fazi 06 `overio_id`, `overeno_at`, `overio_direktor`, `ponisten_razlog`, `ponistio_id`. `zahtev_dorade` — `pregled_id`, `podneo_id`, `razlog`, `status`, `odlucio_id`, vremena; `[Auditable]`.

### `akcija`, `akcija_dogadjaj`, `kvar` (faza 07)
- `akcija` — `broj` unique (`A-2026-0031`), `pregled_id` (izvor), `uredjaj_id`, `aktivnost_id`, `stavka_kljuc`, `vrsta` (`ocistiti`/`podmazati`/`zameniti`/`popraviti`/`ostalo`), `hitnost` (`hitno`/`7_dana`/`sledeci_pz`/`pratiti`), `opis`, `rok` (iz hitnosti), `status` (`otvorena`/`zatvorena`/`otkazana`), `zaduzen`, `kreirao_id`, `kreirano_at`, `zatvorio_id`, `zatvoreno_at`, `razlog`, `stavka_sada_ispravna`; `[Auditable]`.
- `akcija_dogadjaj` — `akcija_id`, `vrsta` (`otvorena`, `ostaje_otvorena`, `zaduzen`, `zatvorena`, `otkazana`), `tekst`, `korisnik_id`, `pregled_id`, `at`.
- `predlog_izmene` (faza 10) — `sablon_id`, `vrsta` (`dodavanje`/`izbacivanje`/`ispravka`), `stavka_id` (set null), `opis`, `razlog`, `podneo_id`, `kreirano_at`, `status` (`zahtev`/`odobren`/`odbijen`), `odlucio_id`, `odluceno_at`, `odgovor`, `verzija_id` (verzija u kojoj je sproveden); `[Auditable]`. `aktivnost.uputstvo_slike bigint[]` (do 3 fajla). `sablon_verzija.status` dobija `zakazana` (objavljena sa budućim datumom).
- `obavestenje` (faza 09) — `korisnik_id` (FK, cascade), `vrsta`, `naslov`, `tekst`, `link`, `kljuc` (idempotentnost; unique sa `korisnik_id` kad nije null), `kreirano_at`, `procitano_at`, `isporuceno_at` (null = čeka push i mejl; delimični indeks), `pokusaja`. `push_pretplata` — `korisnik_id`, `endpoint` unique, `p256dh`, `auth`, `uredjaj_klijenta`, `kreirano_at`. Primaoci po vrsti su u `podesavanje.primaociObavestenja` (JSON).
- `kvar` — `broj` unique (`K-2026-0087`), `uredjaj_id`, `aktivnost_id`, `stavka_kljuc`, `otvorio_pregled_id`, `zatvorio_pregled_id`, `poslednji_pregled_id`, `otvoren`, `zatvoren` (date), `prijava`, `poslednja_napomena`. `refresh_token.dugotrajan` (faza 05) — „Zapamti me“. `sporan_unos` — `pregled_id`, `korisnik_id`, `operacija jsonb`, `razlog`, `primljeno_at`, `odluka` (null = čeka).

### `odgovor`, `uredjaj_van_funkcije`, `fotografija`, `sinhronizacija_operacija`
- `odgovor` — `pregled_id`, `stavka_id`, `vrednost` smallint (0 ispravan, 1 neispravan), `napomena`, `polja jsonb` (ključ = ID dodatnog polja), `korisnik_id`, `klijent_at`, `server_at`, `op_id`, `dorada`; jedinstveno (pregled, stavka, dorada) `ux_odgovor_stavka_dorada`; `[Auditable]`. Promena piše `izmena_odgovora` (staro/novo) u `pregled_dogadjaj`.
- `uredjaj_van_funkcije` — PK (pregled, uređaj), `korisnik_id`, `klijent_at`; važi samo za taj pregled.
- `fotografija` — `pregled_id`, `stavka_id`, `fajl_id`, `izvor` (`kamera`/`galerija`), `korisnik_id`, `klijent_id` unique, `kreirano_at`; najviše 5 po stavci.
- `sinhronizacija_operacija` — `op_id` PK, `pregled_id`, `vrsta`, `klijent_at` (aktivno vreme), `primljeno_at`.

`podesavanje._poslednjiPrenos` pamti poslednji dan koji je obradio posao prenosa propuštenog DP-a.

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
| `AkcijeIKvarovi` | `akcija`, `akcija_dogadjaj`, `kvar` |
| `Obavestenja` | `obavestenje`, `push_pretplata` |
| `IzmeneSablona` | `predlog_izmene`, `aktivnost.uputstvo_slike` |
| `Overa` | kolone overe i poništavanja na `pregled`, `zahtev_dorade` |
| `RadBezInterneta` | `pregled.uredjaj_klijenta`, `refresh_token.dugotrajan`, `sporan_unos` |
| `Popunjavanje` | kolone popunjavanja na `pregled`, `odgovor`, `uredjaj_van_funkcije`, `fotografija`, `sinhronizacija_operacija` |
| `Raspored` | neradni dani, odsustva, pregledi, učesnici, istorija pregleda, zahtevi za prebacivanje |
| `OpremaISabloni` | registar, katalog, dodatna polja, šabloni i verzije, istorijat, `fajl`; briše `kategorija` |
