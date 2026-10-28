# TPM pregledi — dizajn

**Datum:** 2026-10-27

**Status:** Predloženo — čeka odobrenje vlasnika projekta (Miroslav Zec, direktor održavanja)

**Pokrenuto kao:** „Aplikacija koja zamenjuje papirne obrasce TPM pregleda: dnevne preglede DP1 i DP2 i preglede u planskom zastoju PZ1–PZ4."

**Repo:** `tpm-pregledi`

> Ovaj spec pokriva **celu prvu isporuku** (jedna faza). Sledeća faza je modul za pumpe i trake (§12).
> Funkcionalna pravila su u **Prilogu A** (131 pravilo, isti brojevi kao u dokumentu „2026-10-26 Aplikacija – rezime.docx").
> Kad se tekst ovog spec-a i Prilog A razlikuju, važi Prilog A, a razlika se ispravlja u spec-u.

## 1. Cilj i motivacija

Pet do šest inženjera svakog radnog dana radi dnevni pregled opreme (DP1 i DP2 naizmenično), a u planskim zastojima
pregled PZ1–PZ4. Danas se to radi na papiru: obrazac se popunjava rukom, potpisuje, a napomene i kvarovi ostaju u
fasciklama. Ne vidi se ko je šta i kada pregledao, koliko dugo je uređaj u kvaru, šta treba popraviti i da li je
urađeno. Aplikacija na službenom Android telefonu (i bez interneta u pogonu) vodi pregled stavku po stavku, pamti
vreme, slike, kvarove i potrebne akcije, a Supervizor i Direktor na računaru overavaju, raspoređuju i prate stanje
opreme kroz analitiku. Papirni obrazac sa potpisom se ukida.

## 2. Trenutno stanje

| Šta | Gde | Napomena |
|---|---|---|
| Repo | ovaj folder | čist skelet iz Elixir blueprinta v1.1 (preimenovan); primer šifarnika `Kategorije` se uklanja kad stigne prvi poslovni feature |
| Papirni obrasci | `../dnevni i planski pregledi/` (240 skeniranih PDF-ova, van repoa) | DP1, DP2, PZ1–PZ4 |
| Analiza papira | `../Analiza napomena DP-PZ.xlsx` | 4305 napomena; list „Predlog polja" = gde se dodaju nova polja (§6, `dodatno_polje`); list „Ručne ispravke šablona" = ispravke pre uvoza |
| Dogovori | `../2026-10-20 Aplikacija.docx` | sva pitanja i odgovori, hronološki |
| Rezime | `../2026-10-26 Aplikacija – rezime.docx` | 131 pravilo sa sledljivošću; isti sadržaj kao Prilog A |
| Mockup | `../2026-10-26 TPM mockup.html` | klikabilni prikaz svih ekrana (telefon i laptop, sve 4 role, sa i bez interneta); izmišljeni podaci |
| Stari prototip | `../TPM · Pregledi (DP _ PZ).html` | **ne koristi se** (zamenjen mockupom) |

Šabloni još nisu sređeni: pre uvoza se primenjuju ručne ispravke iz Excela, a uvozi se poslednja verzija papirnih
obrazaca (vidi §12 „Odloženo").

## 3. Odluke iz brainstorminga

Sve odluke su zapisane u Prilogu A; ovde su one koje najviše utiču na dizajn.

| # | Odluka | Razlog |
|---|---|---|
| 1 | Četiri role: Inženjer, Supervizor, Direktor, Admin; Admin štiklira koje delove aplikacije koji korisnik vidi i koristi | tako je organizovan rad; odstupanje od blueprinta 07 (`docs/odluke.md`) |
| 2 | Pregled popunjava samo Inženjer, na telefonu, i bez interneta | pogon nema pouzdan signal |
| 3 | Aplikacija je PWA (Chrome na Androidu, „Dodaj na početni ekran"), ne native aplikacija | jedna kodna baza, push radi, nema prodavnice aplikacija |
| 4 | Svaka aktivnost ima tačno dva odgovora; sva polja odgovora su obavezna; napomena obavezna uz neispravan | kao na papiru, ali bez praznih polja |
| 5 | Tri dodatna polja (broj rolni koje ne okreću, temperatura ležaja – dve vrednosti, broj nedostajućih/oštećenih komada); Admin može da doda novo polje tog tipa | analiza 240 obrazaca; ostalo ide u napomenu |
| 6 | „Nije u funkciji" se označava za ceo uređaj, svaki put | ne sme se prenositi automatski |
| 7 | Šabloni imaju verzije; stara verzija se nikad ne menja; započet pregled se završava po svojoj verziji | sledljivost i analitika kroz verzije |
| 8 | Broj pregleda `DP1-2026-0142`, akcije `A-2026-0031`, kvara `K-2026-0087`; dorada ne menja broj | ljudi se pozivaju na broj |
| 9 | Overa je jedan korak („Overio" → „Overeno"); „Vrati na doradu" radi pre i posle overe; Supervizor ne poništava overu Direktora | jednostavno, ali sa hijerarhijom |
| 10 | Pregled se ne briše, nego poništava (uz razlog) | istorija i brojevi ostaju |
| 11 | Propušten DP se prenosi na sledeći radni dan i ceo raspored DP se pomera; inženjeri ostaju na svojim danima (opcija A) | dogovor; može se promeniti posle uhodavanja |
| 12 | PZ ima fiksni datum; aplikacija nudi sledeći po redu, „Promeni PZ" bira drugi | PZ zavisi od planskog zastoja |
| 13 | Kvar je otvoren od pregleda koji ga je prijavio do prvog „ispravno"; akcija je posebna i zatvara se ručno (pop-up ili Supervizor/Direktor) | stanje ≠ zadatak |
| 14 | Merenje aktivnog vremena po odgovorima; upozorenja kontroloru, pragovi posle 3–4 nedelje | kontrola kvaliteta pregleda bez kažnjavanja |
| 15 | Obaveštenja: push na telefon + mejl (Microsoft Graph, po blueprintu); primaoce bira Admin štikliranjem | dogovor |
| 16 | Sve tabele: sortiranje, filteri, pretraga, izvoz u Excel; pojedinačan pregled u PDF i Word | dogovor |
| 17 | Sve opisano ide u jednu fazu; pumpe i trake su sledeća faza | odluka vlasnika |
| 18 | Stari papirni pregledi se ne unose | dogovor |

## 4. Pristup

**Izabrano:** aplikacija po Elixir skeletu (.NET 10 + React 19) proširena za telefon: responzivan prikaz, PWA
(service worker + IndexedDB) i sinhronizacija kroz idempotentne operacije. Server je jedini izvor istine;
telefon drži lokalnu kopiju samo onoga što je inženjeru dodeljeno i red operacija za slanje.

Alternative:
- **A — native Android aplikacija** — odbijeno: druga kodna baza, distribucija kroz MDM/prodavnicu, a PWA pokriva sve
  potrebe (kamera, push, rad bez interneta) na Chrome-u.
- **B — samo online web aplikacija** — odbijeno: u pogonu nema pouzdanog signala; pregled ne sme da stane.
- **C — gotov CMMS proizvod** — odbijeno: obrasci, pravila overe i analitika su specifični; Elixir standard je
  sopstvena aplikacija po blueprintu.

## 5. Arhitektura

```
 TELEFON (Chrome PWA)                         RAČUNAR (Chrome/Edge)
 ┌───────────────────────────────┐             ┌──────────────────────────┐
 │ React SPA (features/*)        │             │ ista React SPA, šira     │
 │ Service worker (app shell)    │             │ (bočni meni, tabele)     │
 │ IndexedDB: paket + outbox     │             └────────────┬─────────────┘
 └──────────────┬────────────────┘                          │ HTTPS /api/v1
                │ HTTPS /api/v1 (kad ima veze)               │
                ▼                                            ▼
 ┌──────────────────────────────────────────────────────────────────────┐
 │ TpmPregledi.Api  — Minimal API, Endpoints/* po feature-u, politike   │
 │ TpmPregledi.Application — MediatR komande/upiti + validatori         │
 │   Features: Sabloni · Uredjaji · Raspored · Odsustva · Pregledi ·     │
 │   Sinhronizacija · Overa · Akcije · Kvarovi · Analitika · Obavestenja│
 │   · Pretraga · Podesavanja · Korisnici (proširen) · Audit · Version  │
 │ TpmPregledi.Domain — entiteti, pravila (statusi, brojevi, vreme)      │
 │ TpmPregledi.Infrastructure — EF Core/Postgres, fajlovi (slike),      │
 │   PDF/Word/Excel, Web Push, Microsoft Graph mejl, BackgroundService  │
 └───────────────┬──────────────────────────────────────┬───────────────┘
                 ▼                                      ▼
        PostgreSQL 16 (stack)                 data/fotografije (volume)
```

### 5.1 Rad bez interneta (telefon)

1. **Paket.** Kad ima veze, telefon povlači `GET /api/v1/moj-paket` (ETag): dodeljeni pregledi za danas i narednih
   7 dana i započeti pregledi; njihove verzije šablona (sekcije, uređaji, aktivnosti, odgovori, dodatna polja,
   uputstva — tekst; slike uputstva keširaju se po potrebi); za uređaje iz tih pregleda: status, otvorene akcije,
   „prošli put nije u funkciji" i kratak dosije. Paket se čuva u IndexedDB.
2. **Outbox.** Svaka radnja u pregledu je operacija sa `opId` (UUID sa telefona) i vremenom sa telefona:
   odgovor, napomena, dodatno polje, „nije u funkciji", nova akcija, odgovor na pop-up akcije, „sve neoznačeno je
   ispravno", početak, predaja. Operacije idu redom kroz `POST /api/v1/sinhronizacija`; server ih primenjuje
   idempotentno (ponovljen `opId` = već primenjeno) i vraća šta je prihvaćeno, a šta odbijeno i zašto.
3. **Slike** se pre slanja smanjuju na telefonu (JPEG, duža strana najviše 2048 px, cilj 1–2 MB) i šalju posebno
   (`POST /api/v1/fotografije`, `klijentId` = idempotentnost).
4. **Predaja bez interneta** — status na telefonu „Predato – čeka slanje"; operacija predaje ide poslednja.
   Dok se ne pošalje, pregled ne može da se menja. Kad stigne, server proverava kompletnost i tek tada
   pravi PDF/rezime i šalje obaveštenja.
5. **Jedan telefon.** Započet pregled je vezan za `uredjajKlijenta` (ID instalacije PWA). Drugi telefon dobija
   409 dok prvi ne pošalje podatke. Ako je pregled u međuvremenu dodeljen drugom, urađeno se čuva kao sporan unos,
   a Supervizor bira šta važi.
6. **Promena šablona** dok je inženjer bez interneta: pregled se završava po verziji po kojoj je započet.
7. **Prikaz:** „Poslednja sinhronizacija: HH:MM"; bez interneta i broj odgovora i slika koji čekaju slanje.
8. **Sesija:** „Zapamti me" — refresh token važi duže na tom uređaju (odluka u `docs/odluke.md`); token se i dalje
   ne čuva u `localStorage` (blueprint 12 §38); podaci pregleda u IndexedDB su na službenom telefonu sa zaključanim
   ekranom.

### 5.2 Pozadinski poslovi (`BackgroundService`, bez Hangfire/Quartz)

| Posao | Kada | Šta |
|---|---|---|
| Nije započet | DP u vreme iz podešavanja (13:00), PZ u svoje vreme | obaveštenje primaocima |
| Propušten DP | kraj radnog dana (podešavanje) | pregled koji nije započet se prenosi na sledeći radni dan; raspored DP se pomera (Prilog A, R3.2–R3.6) |
| Rok za overu | na svakih 15 min | 24 h radnih dana od predaje → podsetnik |
| Rok akcije | jednom dnevno | istekao rok → jedno obaveštenje, crveno na Dashboardu |
| Ponovljeni kvar | posle svake predaje | ista stavka neispravna N puta u M dana → obaveštenje |
| Dugo van funkcije | jednom dnevno | uređaj van funkcije duže od X dana → obaveštenje, ponavlja se na svakih X dana |
| Predlog rasporeda | jednom nedeljno (i na zahtev) | predlog za sledeću nedelju po rotaciji, preskačući odsutne i neradne dane |

Svi poslovi su idempotentni (pamte šta su već poslali) i rade po `Europe/Belgrade` kroz `IDateTimeProvider`.

### 5.3 Izveštaji i izvoz

Posle predaje server pravi **ceo izveštaj u PDF-u** i **rezime problematičnih stavki** (sa linkovima na slike).
Pojedinačan pregled se izvozi u PDF i Word; liste u Excel (izvozi se ono što je filtrirano). PDF: `QuestPDF`
(u blueprintu — **proveriti licencu**, vidi §12); Word: `DocumentFormat.OpenXml`; Excel: `ClosedXML`.

## 6. Šema baze

Konvencije po blueprintu 05: `snake_case`, `id bigint identity`, `timestamptz` UTC, poslovni datumi `date`,
matični podaci se ne brišu (`aktivan`). Postojeće tabele skeleta (Identity, `refresh_token`, `audit_log`,
`email_log`) ostaju; tabela `kategorija` se uklanja.

### 6.1 Korisnici i prava

| Tabela | Kolone | Obrazloženje |
|---|---|---|
| `AspNetUsers` (+) | `ime_prezime text`, `aktivan bool` | skelet; korisničko ime = službeni mejl |
| `korisnik_pravo` | `korisnik_id`, `pravo text` (PK oba) | prava štikliranjem; podrazumevani skup po roli se upisuje pri kreiranju naloga |

Role (`Roles.cs`): `Inzenjer`, `Supervizor`, `Direktor`, `Admin` — tačno jedna po korisniku. Prava (`Prava.cs`),
npr. `pregledi.popunjavanje` (samo Inženjer, ne može se dodeliti drugim rolama), `overa`, `raspored.uredjivanje`,
`odsustva.odobravanje`, `akcije.zatvaranje`, `sabloni.nacrt`, `sabloni.objava`, `neradni_dani`, `korisnici`,
`podesavanja`, `analitika`. Matrica podrazumevanih prava je u `docs/04-roles-and-permissions.md`.

### 6.2 Oprema i šabloni

| Tabela | Ključne kolone | Obrazloženje |
|---|---|---|
| `sekcija` | `sifra text unique` („10"…„70"), `naziv` | kao na obrascima |
| `uredjaj` | `oznaka text unique` (npr. `10-C-07B`), `naziv`, `sekcija_id`, `vrsta text`, `status text` (`aktivan`/`van_upotrebe`), `qr_token uuid` | centralni registar; svaki uređaj postoji jednom |
| `aktivnost` | `naziv`, `odgovor_ispravan`, `odgovor_neispravan`, `uputstvo_tekst null`, `uputstvo_azurirao_id`, `uputstvo_azurirano_at` | katalog; aktivnost se bira, ne kuca |
| `aktivnost_slika` | `aktivnost_id`, `fajl_id`, `redosled` | do 3 slike uputstva |
| `dodatno_polje` | `naziv`, `tip text` (`broj`/`merenje`/`dve_vrednosti`/`da_ne`), `jedinica`, `oznake jsonb` (npr. „ležaj do rotora", „ležaj do remenice/spojnice"), `prikaz text` (`uvek`/`uz_neispravan`), `obavezno bool` | tri početna polja + ono što Admin doda |
| `sablon` | `tip text unique` (`DP1`,`DP2`,`PZ1`…`PZ4`) | šest šablona |
| `sablon_verzija` | `sablon_id`, `broj int`, `status text` (`nacrt`/`objavljena`/`zamenjena`), `vazi_od date`, `objavio_id`, `objavljeno_at` | važeća se nikad ne menja |
| `sablon_stavka` | `verzija_id`, `sekcija_id`, `uredjaj_id`, `aktivnost_id`, `redosled`, `kljuc uuid` | `kljuc` je stalni ID stavke kroz verzije (analitika) |
| `sablon_stavka_polje` | `stavka_id`, `dodatno_polje_id` | gde se polje prikazuje (Excel „Predlog polja") |
| `predlog_izmene` | `sablon_id`, `uredjaj_id null`, `vrsta`, `opis`, `razlog`, `predlozio_id`, `status`, `odlucio_id`, `komentar`, `u_verziji_id null` | Inženjer/Supervizor predlažu, Direktor/Admin odlučuju |
| `sablon_dogadjaj` | `sablon_id`, `verzija_id`, `vrsta`, `opis`, `korisnik_id`, `at` | „Istorijat šablona" |

### 6.3 Raspored, odsustva, neradni dani

| Tabela | Ključne kolone | Obrazloženje |
|---|---|---|
| `neradni_dan` | `datum date unique`, `naziv`, `vrsta` (`drzavni`/`verski`/`fabrika`), `uneo_id` | unos od–do pravi više redova |
| `odsustvo` | `korisnik_id`, `od date`, `do date`, `vrsta`, `napomena`, `status` (`zahtev`/`odobreno`/`odbijeno`), `zatrazio_id`, `odlucio_id`, `razlog_odbijanja` | i zahtev i uneto odsustvo |
| `zahtev_prebacivanja` | `pregled_id`, `podneo_id`, `predlozena_zamena_id null`, `razlog`, `status`, `zamena_id null`, `odlucio_id` | prebacivanje pregleda |
| `brojac` | `vrsta text` (`DP1`…`PZ4`, `A`, `K`), `godina int`, `poslednji int` (PK vrsta+godina) | brojevi; dodela u istoj transakciji (`SELECT … FOR UPDATE`) |

### 6.4 Pregledi

| Tabela | Ključne kolone | Obrazloženje |
|---|---|---|
| `pregled` | `broj text unique`, `tip`, `sablon_verzija_id`, `planirani_datum date`, `prvobitni_datum date`, `status text`, `dodeljen_id`, `dorada int default 0`, `zapocet_at`, `predat_at`, `uredjaj_klijenta text null`, `aktivno_sekundi int`, `delova int`, `overio_id`, `overeno_at`, `ponisten_razlog null`, `ponistio_id null` | status: `planiran`, `u_radu`, `ceka_overu`, `overen`, `vracen_na_doradu`, `ponisten` („Predato – čeka slanje" postoji samo na telefonu; „nije započet" i „propušten" su izvedeni) |
| `pregled_ucesnik` | `pregled_id`, `korisnik_id`, `sekcije text[]` | PZ sa dva inženjera |
| `odgovor` | `pregled_id`, `stavka_id`, `vrednost smallint` (0 = ispravan, 1 = neispravan), `napomena`, `polja jsonb`, `korisnik_id`, `klijent_at`, `server_at`, `op_id uuid unique`, `dorada int` | jedan važeći red po stavci i doradi; prethodne vrednosti u `pregled_dogadjaj` (staro/novo) |
| `uredjaj_van_funkcije` | `pregled_id`, `uredjaj_id`, `korisnik_id`, `klijent_at` | važi samo za taj pregled |
| `fotografija` | `pregled_id`, `stavka_id`, `fajl_id`, `izvor` (`kamera`/`galerija`), `korisnik_id`, `klijent_id uuid unique` | najviše 5 po stavci |
| `fajl` | `putanja`, `mime`, `velicina`, `sha256`, `kreirano_at` | slike pregleda i uputstava; na volumenu `data/fotografije` |
| `pregled_dogadjaj` | `pregled_id`, `vrsta`, `podaci jsonb`, `korisnik_id`, `at` | istorija: dodela, prebacivanje, prenos (stari/novi datum), početak, „sve neoznačeno je ispravno" (sekcija, vreme), izmena odgovora (staro/novo), predaja, overa, dorada, zahtev za doradu, poništavanje, upozorenja o vremenu, sporan unos |

### 6.5 Akcije, kvarovi, obaveštenja, podešavanja

| Tabela | Ključne kolone | Obrazloženje |
|---|---|---|
| `akcija` | `broj text unique`, `pregled_id` (izvor), `uredjaj_id`, `aktivnost_id`, `stavka_kljuc uuid`, `vrsta`, `hitnost`, `opis`, `rok date null`, `status` (`otvorena`/`zatvorena`/`otkazana`), `zaduzen text null`, `zatvorio_id`, `zatvoreno_at`, `razlog` | vrsta: Očistiti / Podmazati ili dopuniti / Zameniti / Popraviti ili sanirati / Ostalo; hitnost: Hitno / U narednih 7 dana / U sledećem planskom zastoju / Pratiti |
| `akcija_dogadjaj` | `akcija_id`, `vrsta`, `tekst`, `korisnik_id`, `pregled_id null`, `at` | pop-up „ostaje otvorena", dodela zaduženog, zatvaranje |
| `kvar` | `broj text unique`, `uredjaj_id`, `aktivnost_id`, `stavka_kljuc`, `otvorio_pregled_id`, `zatvorio_pregled_id null`, `otvoren date`, `zatvoren date null`, `prijava int`, `poslednja_napomena` | otvara se i zatvara iz predatih pregleda (izvedeno, ali materijalizovano zbog analitike i broja) |
| `obavestenje` | `korisnik_id`, `vrsta`, `naslov`, `tekst`, `link`, `procitano_at`, `kreirano_at` | zvono u aplikaciji |
| `push_pretplata` | `korisnik_id`, `endpoint`, `p256dh`, `auth`, `uredjaj_klijenta` | Web Push (VAPID) |
| `primalac_obavestenja` | `vrsta`, `korisnik_id` | štikliranje u Podešavanjima |
| `podesavanje` | `kljuc text pk`, `vrednost jsonb` | vremena upozorenja, kraj radnog dana, rok overe, pauza, pragovi, „na dan PZ-a DP se ne radi/radi" |

Migracije: `Pocetna` (sve iznad) + seed (role, prava, sekcije, `podesavanje`); uvoz šablona je posebna komanda
(Admin, `.xlsx`, vidi §7).

## 7. API ugovor

Konvencije po blueprintu 06: `/api/v1`, 400 = oblik (`errors`), 422 = poslovno pravilo (`greske[]`), 409 = konkurentna
izmena/zaključan pregled, 404 = ne postoji ili nije dostupan. Politike: `Authenticated` + provera prava (§6.1).

| Metod | Ruta | Politika / pravo | Svrha |
|---|---|---|---|
| GET | `/moj-paket` | Inženjer | paket za rad bez interneta (ETag) |
| POST | `/sinhronizacija` | Inženjer | `{ uredjajKlijenta, klijentSada, operacije[] }` → `{ prihvacene[], odbijene[{opId, razlog}], serverSada }` |
| POST | `/fotografije` | Inženjer | multipart, `klijentId`, `pregledId`, `stavkaId`, `izvor`; ≤ 3 MB |
| GET | `/pregledi` | ulogovan | arhiva: filteri (datum od–do, tip, status, inženjer, šablon), sortiranje, pretraga, stranice |
| GET | `/pregledi/{id}` | ulogovan | detalj sa odgovorima, slikama, vremenom, upozorenjima, istorijom |
| POST | `/pregledi/{id}/overi` | `overa` | → `overen` |
| POST | `/pregledi/{id}/vrati-na-doradu` | `overa` | `{ inzenjerId, razlog }`; pravilo: Supervizor ne vraća pregled koji je overio Direktor |
| POST | `/pregledi/{id}/zahtev-za-doradu` | Inženjer (svoj pregled) | `{ razlog }` |
| POST | `/pregledi/{id}/zahtev-za-doradu/odluka` | `overa` | `{ odobreno }` |
| POST | `/pregledi/{id}/ponisti` | `overa` | `{ razlog }` |
| POST | `/pregledi/{id}/sporan-unos/odluka` | `overa` | koji unos važi |
| GET | `/pregledi/{id}/izvoz?format=pdf\|docx` | ulogovan | izveštaj |
| GET | `/raspored?od=&do=` | ulogovan | kalendar i raspored |
| POST | `/raspored/predlog` | `raspored.uredjivanje` | predlog za nedelju |
| POST | `/raspored/potvrdi` | `raspored.uredjivanje` | kreira preglede i brojeve |
| PUT | `/pregledi/{id}/dodela` | `raspored.uredjivanje` | promena inženjera / datuma |
| POST | `/pregledi/{id}/ucesnici` | `raspored.uredjivanje` | „Dodaj inženjera" na PZ, sa sekcijama |
| POST | `/raspored/pz` | `raspored.uredjivanje` | `{ datum, tip, napomena? }` — „Promeni PZ" |
| GET/POST | `/zahtevi-prebacivanja`, `/{id}/odluka` | Inženjer / `raspored.uredjivanje` | zahtev i odluka (`{ odobreno, zamenaId }`) |
| GET/POST | `/odsustva`, `/odsustva/zahtev`, `/odsustva/{id}/odluka` | ulogovan / `odsustva.odobravanje` | odluka nosi `prebacivanja[{pregledId, zamenaId}]` |
| GET | `/odsustva/{korisnikId}/pregledi?od=&do=` | `odsustva.odobravanje` | pregledi u periodu + predlog zamene |
| GET/POST/DELETE | `/neradni-dani` | `neradni_dani` | od–do, naziv, vrsta |
| GET | `/akcije`, `/akcije/{id}` | ulogovan | lista (filteri) i detalj sa istorijom |
| POST | `/akcije/{id}/zatvori`, `/otkazi` | `akcije.zatvaranje` | `{ razlog }` |
| PUT | `/akcije/{id}/zaduzen` | `akcije.zatvaranje` | `{ zaduzen }` |
| GET | `/kvarovi`, `/kvarovi/{id}` | ulogovan | lista i detalj |
| GET | `/uredjaji`, `/uredjaji/{id}` | ulogovan | registar; ekran uređaja: status, istorija (`?od=`), pregledi, dosije, kvarovi i akcije, analitika |
| POST/PUT | `/uredjaji`, `/uredjaji/{id}` | `sabloni.nacrt` | registar (status `van_upotrebe` umesto brisanja) |
| GET/POST/PUT | `/aktivnosti`, `/aktivnosti/{id}/uputstvo` | ulogovan / `sabloni.nacrt` | katalog i uputstvo (tekst + do 3 slike) |
| GET | `/sabloni`, `/sabloni/{tip}/verzije/{broj}` | ulogovan | verzije |
| POST/PUT | `/sabloni/{tip}/nacrt…` | `sabloni.nacrt` | nacrt: stavke, polja, redosled, razlike |
| POST | `/sabloni/{tip}/nacrt/objavi` | `sabloni.objava` | `{ vaziOd }` |
| POST | `/sabloni/uvoz` | Admin | prvi uvoz iz `.xlsx` |
| GET/POST | `/predlozi-izmena`, `/{id}/odluka` | ulogovan / `sabloni.objava` | predlozi |
| GET | `/sabloni/istorijat` | ulogovan | filteri: šablon, vrsta, korisnik, period |
| GET | `/analitika/*` | `analitika` | ispunjenje plana, neispravnosti, kvarovi, trajanje, van funkcije, trend |
| GET | `/pretraga?q=` | ulogovan | broj pregleda/akcije/kvara ili deo naziva uređaja |
| GET | `/obavestenja`, POST `/obavestenja/procitano` | ulogovan | zvono |
| POST/DELETE | `/push/pretplata` | ulogovan | Web Push |
| GET | `/odluke` | `overa` | „Čeka moju odluku": overa, dorada, prebacivanje, odsustva, predlozi šablona (Direktor) |
| GET/PUT | `/podesavanja`, `/podesavanja/primaoci` | `podesavanja` | |
| — | `/auth/*`, `/korisnici/*`, `/audit-log`, `/server-stanje`, `/version`, `/health*` | skelet | korisnici dobijaju rolu iz četiri i prava; deaktivacija umesto brisanja |
| GET | `/izvoz/{lista}.xlsx` | isto kao lista | izvoz filtrirane liste |

Primer operacije u sinhronizaciji:

```json
{ "opId": "8f1c…", "vrsta": "odgovor", "pregledId": 1042, "stavkaId": 55871,
  "klijentAt": "2026-10-26T07:41:12+02:00",
  "podaci": { "vrednost": 1, "napomena": "7 rolni ne okreće", "polja": { "broj": 7 } } }
```

## 8. Frontend

Rute (`Zasticeno` + prava, kao u skeletu). Raspored: telefon < 768 px — donji meni (4 stavke + „Više"), kartice umesto
tabela, jedan uređaj u fokusu, velika dugmad; tablet — nedeljni prikaz; laptop i veći — bočni meni i pune tabele.
Klik na logo vodi na Početnu; svaki ekran detalja ima „Nazad" na ekran sa kog je otvoren.

| Ruta | Ekran (mockup) | Ko |
|---|---|---|
| `/` | Početna: „Moje aktivnosti" (inženjer: danas + 7 dana, zahtevi, akcije, poslednja sinhronizacija), „Čeka moju odluku" (Supervizor/Direktor), KPI, danas i sutra, otvorene akcije, van funkcije | svi |
| `/pregled/:id/popuni` | Popunjavanje: sekcije, uređaji, aktivnosti sa dva odgovora, Napomena · Akcija · Foto, „?" uputstvo, dodatna polja, „Nije u funkciji", pop-up otvorene akcije, „Sve neoznačeno u sekciji je ispravno", PZ sa dva inženjera | Inženjer |
| `/pregledi`, `/pregledi/:id` | Arhiva i detalj: vreme i upozorenja, rezime, van funkcije (buleti), istorija, Overio / Vrati na doradu / Poništi / Zatraži doradu, izvoz | svi |
| `/raspored` | nedelja, PZ i „Promeni PZ", zahtevi za prebacivanje, odsustva sa prebacivanjem | Supervizor, Direktor |
| `/kalendar` | mesec (telefon: spisak po danima); klik otvara pregled ili karticu planiranog | svi |
| `/akcije`, `/akcije/:id` | lista sa kolonom „Pregled", „Kvar na stavci", „Zadužen"; detalj sa istorijom | svi |
| `/neispravnosti`, `/neispravnosti/:id` | kvarovi sa kolonom „Pregled" i „Akcije"; detalj | svi |
| `/uredjaji`, `/uredjaji/:id` | registar; ekran uređaja (istorija sa klizačem 30 dana – 1 godina, „Svi pregledi" / „Dosije", kvarovi i akcije, analitika, QR) | svi |
| `/analitika` | ispunjenje plana i kartice (Prilog A, A.8) | po pravu |
| `/sabloni` | Verzije · Nacrt · Predlozi izmena · Istorijat šablona · Nije ni u jednom pregledu · Uputstva | Direktor, Admin |
| `/moja-odsustva` | zahtevi i odsustva inženjera | Inženjer |
| `/neradni-dani`, `/korisnici`, `/podesavanja` | administracija | Admin (neradni dani i Direktor) |

Pretraga (u zaglavlju i u svakoj tabeli) žuto obeležava pronađeni deo teksta u rezultatima.

Stanja po ekranu (učitavanje / prazno / greška / podaci) po skeletu (`FetchingOverlay`, skeleton kartice).
Datumi i brojevi samo kroz `lib/datum.ts` i `lib/broj.ts` (blueprint 15). Nove biblioteke (zapisano u `docs/odluke.md`):
`vite-plugin-pwa` (service worker), `dexie` (IndexedDB), `qrcode` (QR nalepnice). Grafikoni: `recharts` (u blueprintu).
Prijava: „Zapamti me na ovom uređaju", dugme „oko" uz lozinku (prikazano dok se ponovo ne klikne). „Moj nalog" (klik
na ime; na telefonu ikonica 👤) — promena lozinke.

### 8.1 Osnovna pravila prikaza (važe za sve ekrane)

1. Aplikacija radi na svim širinama ekrana, od telefona (360 px) do velikog monitora (1920 px i više), u Chrome-u i Edge-u.
2. Ni na jednom ekranu stranica ne sme da se pomera bočno i ništa ne sme da izađe iz okvira ekrana, kartice ili dijaloga — ni sa ni bez interneta, ni za jednu rolu.
3. Dugačak tekst se prelama u novi red; brojevi (DP1-2026-0142, A-2026-0031, K-2026-0087) se ne lome; oznake (hitnost, status) u tabelama mogu u dva reda; trake sekcija i tabovi prelaze u novi red umesto da beže van ekrana.
4. Tabele na laptopu od 1366 px staju na ekran bez bočnog pomeranja (naslovi kolona mogu u dva reda); na telefonu su kartice, naziv polja levo, vrednost pored njega.
5. Poravnanje: polja i dugmad u istom redu su iste visine i poravnati; sadržaj kartica je poravnat po istoj levoj ivici; ekrani su centrirani u radnom delu.
6. Na telefonu: dugmad najmanje 44 × 44 px; zaglavlje ima samo logo i ikonice (🔍 🔔 👤); obaveštenje „Bez interneta“ je traka ispod zaglavlja, ne u zaglavlju.
7. Dijalozi nisu širi od ekrana; ako je sadržaj duži, pomera se unutar dijaloga. Grafikoni se šire prema kartici.
8. Pre isporuke svaki ekran se proverava za svaku rolu na širinama 375, 768, 1366 i 1920 px, sa i bez interneta: automatski test da nema bočnog pomeranja i ničega van okvira, plus ručni pregled.

## 9. Ponašanje pri greškama

| Situacija | Status | Poruka korisniku |
|---|---|---|
| Predaja sa praznim odgovorima ili bez obavezne napomene | 422 | „Pregled još nije potpun: nedostaje N odgovora i M obaveznih napomena." |
| PZ predaja dok druge sekcije nisu gotove | 422 | „PZ se predaje kad su sve sekcije gotove. Nedostaje: sekcija 70 (Stefan Ilić)." |
| Pregled započet na drugom telefonu | 409 | „Pregled je započet na drugom telefonu. Nastaviće se kad taj telefon pošalje podatke." |
| Supervizor vraća ili poništava pregled koji je overio Direktor | 422 | „Pregled je overio Direktor; samo Direktor može da poništi tu overu." |
| Izmena predatog pregleda bez odobrene dorade | 422 | „Predat pregled se menja samo kroz doradu." |
| Šesta slika na stavci | 422 | „Najviše 5 slika po stavci." |
| Slika veća od 3 MB posle smanjivanja | 400 | „Slika je prevelika." |
| Sat telefona odstupa više od dozvoljenog | 200 + upozorenje kontroloru | (inženjer ne vidi poruku; kontrolor vidi oznaku) |
| Prebacivanje već započetog pregleda | 422 | „Pregled je već započet; o prebacivanju odlučuje Supervizor ili Direktor." |
| Odsustvo se preklapa sa postojećim | 422 | „Za taj period već postoji odsustvo." |
| Objava nacrta bez izmena | 422 | „Nacrt nema izmena u odnosu na važeću verziju." |
| Promena PZ-a na PZ koji je već zakazan | 422 | „Taj PZ je već zakazan za <datum>." |
| Operacija sinhronizacije za pregled koji je poništen | odbijena operacija | „Pregled je poništen." (lokalni podaci se čuvaju i šalju kao sporan unos) |

## 10. Testiranje

Backend unit (Application):
- `BrojacTests` — brojevi po tipu i godini; dorada ne menja broj; poništen broj se ne koristi ponovo.
- `PomeranjeRasporedaTests` — propušten DP → sledeći radni dan, preskaču se neradni dani i dan PZ-a, redosled DP1/DP2 ostaje, inženjeri ostaju na danima.
- `AktivnoVremeTests` — pauza > granice deli pregled; vreme dorade se ne računa.
- `KvarTests` — otvaranje na neispravnom, zatvaranje na prvom ispravnom, „nije u funkciji" se ne računa.
- `OveraTests` — Supervizor ne poništava overu Direktora; zahtev za doradu pre i posle overe.
- `SinhronizacijaTests` — idempotentnost `opId`; redosled; odbijanje posle poništavanja; sporan unos.
- `IspunjenjePlanaTests` — odsustva se ne računaju; preneti pregled ide onom kome je bio dodeljen.

Integracioni (Testcontainers): 401/403 po svakoj ruti i pravu; ceo tok popuni → predaj → overi → vrati → dorada;
uvoz šablona iz `.xlsx`; izvoz PDF/Word/Excel (postoji i nije prazan).

Frontend (Vitest): outbox i ponovno slanje; prikaz polja samo uz neispravan odgovor; guard ruta po pravima;
„Prebaci sve na" u odsustvu. Test prikaza (Playwright ili Vitest + jsdom gde je moguće, inače ručna check-lista): svaki ekran × svaka rola × 375/768/1366/1920 px × sa/bez interneta — nema bočnog pomeranja stranice i nijedan element ne izlazi iz okvira (§8.1).

**Šta NE testiramo automatski:** stvarni Web Push na telefonu i instalaciju PWA (ručna provera na Android Chrome-u
po check-listi u `docs/deploy/`), izgled PDF-a (vizuelna provera), slanje mejla kroz Graph (fake u testovima).

## 11. Dokumentacija

| Fajl | Izmena |
|---|---|
| `docs/01-architecture.md` | PWA, rad bez interneta, pozadinski poslovi, fajlovi |
| `docs/02-database-schema.md` | sve tabele iz §6 |
| `docs/03-api-contract.md` | sve rute iz §7 |
| `docs/04-roles-and-permissions.md` | četiri role, prava, matrica podrazumevanih prava |
| `docs/05-pravila-pregleda.md` | aktivno vreme, kvar, ispunjenje plana, pomeranje rasporeda — sa numeričkim primerom |
| `docs/odluke.md` | odstupanja od blueprinta (već zapisana kao predlozi) |
| `docs/user/02-popunjavanje-pregleda.md`, `03-overa-i-dorada.md`, `04-raspored-i-odsustva.md`, `05-akcije-i-kvarovi.md`, `06-uredjaji-i-analitika.md`, `07-sabloni.md` | korisnička uputstva po temama |
| `CHANGELOG.md` | red pod Neobjavljeno po feature-u |

## 12. Šta se NE radi / van obima

- **Modul za pumpe i trake** — sledeća faza; nije deo DP i PZ; sadržaj se definiše kasnije. Ekran uređaja je mesto
  gde se dodaje kao nova kartica.
- Modul održavanja i radni nalozi (akcija tada može da postane radni nalog).
- Unos starih papirnih pregleda.
- Istorija administracije van postojećeg audit loga skeleta (odluka vlasnika: za sada nije potrebna; audit log skeleta
  ionako beleži izmene korisnika i podešavanja).
- Mesečni izveštaj mejlom.
- iPhone kao radni telefon (podrška je Android + Chrome; iPhone radi samo kao instalirana PWA, bez garancije za push).
- Dorada analitike — posle prvih meseci rada.

**Odloženo (otvoreno pre isporuke):**
- Sređivanje šablona i uvoz ažurne verzije (ručne ispravke iz Excela).
- IT: domen i HTTPS, server, nalog za Microsoft Graph (mejl), prostor i bekap za slike (procena ~5 GB godišnje).
- Licenca za `QuestPDF` (Community licenca važi za firme sa prihodom ispod 1 M USD) — ili kupovina, ili druga biblioteka;
  odlučuju vlasnik projekta i vlasnik blueprinta.

## 13. Reference

- Elixir blueprint v1.1: https://github.com/casper96kzs/elixir-blueprint (02, 05, 06, 07, 12, 13, 14, 15, 16, 18, 19).
- `../2026-10-26 Aplikacija – rezime.docx` (= Prilog A), `../2026-10-20 Aplikacija.docx` (istorija dogovora).
- `../2026-10-26 TPM mockup.html` — mockup svih ekrana.
- `../Analiza napomena DP-PZ.xlsx` — analiza 240 papirnih obrazaca, predlog polja, ručne ispravke šablona.
- `docs/odluke.md` — odstupanja od blueprinta.

## Prilog A — Funkcionalna pravila

> 131 pravila, isti brojevi kao u „2026-10-26 Aplikacija – rezime.docx". Oznaka u zagradi je izvor: **Z** = Izvorni zahtev (dokument od 20.10.); **K** = Odgovor klijenta; **P** = Analiza 240 papirnih obrazaca; **O** = Odluka iz razgovora 26.10..

### A.1 Svrha, korisnici i prava

- **R1.1** Aplikacija zamenjuje papirne obrasce TPM pregleda opreme u fabrici ElixirZorka: dnevne preglede DP1 i DP2 i preglede u planskom zastoju PZ1–PZ4. *[Z]*
- **R1.2** Vlasnik projekta je Miroslav Zec, direktor održavanja; on odobrava specifikaciju. *[O]*
- **R1.3** Preglede radi 5–6 inženjera, na službenim Android telefonima. *[K]*
- **R1.4** Role su Inženjer, Supervizor, Direktor i Admin. *[Z]*
- **R1.5** Podrazumevana prava po rolama: *[O]*
  - Inženjer: svoji pregledi, arhiva, ekran uređaja, analitika i neispravnosti (pregled);
  - Supervizor: sve što i Inženjer, plus raspored, overa, dorada i akcije;
  - Direktor: sve što i Supervizor, plus objava šablona;
  - Admin: sva podešavanja.
- **R1.6** Admin štikliranjem menja koje delove aplikacije koji korisnik vidi i koristi. *[K]*
- **R1.7** Svi korisnici vide sve preglede, neispravnosti, analitiku i ekrane uređaja. *[K]*
- **R1.8** Pregled popunjava samo Inženjer. *[K]*
- **R1.9** Prijava je službenim mejlom i lozinkom. Naloge pravi Admin, a zaboravljenu lozinku resetuje Admin; link za reset na mejl dodaje se kad se poveže Microsoft 365. *[O]*
- **R1.10** Kad inženjer ode iz firme, Admin mu deaktivira nalog (ne briše ga): ne može da se prijavi, ispada iz rotacije, a njegovi pregledi, napomene i akcije ostaju pod njegovim imenom. *[O]*
- **R1.11** Prijava ima „Zapamti me na ovom uređaju“ (ostaje prijavljen do odjave; na službenom telefonu obavezno zbog rada bez interneta) i dugme „oko“ uz lozinku: klik prikazuje lozinku dok se ponovo ne klikne. Važi za sva polja za lozinku. *[O]*
- **R1.12** Svaki korisnik sam menja lozinku („Moj nalog“, klik na ime u zaglavlju): trenutna, nova (najmanje 8 znakova) i ponovljena; na ostalim uređajima treba nova prijava. Zaboravljenu lozinku resetuje Admin. *[O]*

### A.2 Šabloni i registar opreme

- **R2.1** Postoji šest šablona: DP1, DP2 i PZ1–PZ4. Šablon se sastoji od sekcija, sekcija od uređaja, a uređaj od aktivnosti. Svaka aktivnost ima dva ponuđena odgovora (npr. Ispravno / Neispravno, Okreće / Ne okreće, Ima ulja / Nema ulja). *[Z]*
- **R2.2** Postoji centralni registar opreme: svaki uređaj postoji jednom, sa stalnim ID-jem, oznakom, sekcijom i vrstom. Šabloni pokazuju na uređaje iz registra. *[Z]*
- **R2.3** Registar opreme i katalog aktivnosti održavaju Admin i Direktor. Nova aktivnost u katalogu dobija naziv i par odgovora. Uređaj se ne briše, nego dobija status „van upotrebe“: istorija ostaje, a ne nudi se za nove šablone. *[O]*
- **R2.4** Uz svaku aktivnost stoji ikonica „?“ koja otvara uputstvo (tekst i do 3 referentne slike); ako ga nema, piše „Uputstvo još nije uneto“. Unose ga Admin i Direktor u Šablonima („Uputstva za aktivnosti“) ili direktno iz prozora uputstva („Unesi / Izmeni uputstvo“). Važi u svim šablonima, izmena ne pravi novu verziju. Može kasnije da se ukine. *[O]*
- **R2.5** Svaka izmena šablona pravi novu verziju; stara verzija se nikad ne menja. Svaki popunjen pregled pamti po kojoj je verziji urađen. Stavke i uređaji imaju stalni ID kroz sve verzije. *[Z]*
- **R2.6** Izmena ide kroz nacrt: Direktor ili Admin pripremaju nacrt (dodaju uređaje iz registra, biraju aktivnosti iz kataloga, izbacuju, menjaju redosled), pre objave vide razlike, a novu verziju objavljuje samo Direktor, sa datumom od kog važi. Započeti pregledi se završavaju po staroj verziji. *[K]*
- **R2.7** Aktivnosti se biraju iz kataloga aktivnosti, ne kucaju se, pa ne mogu nastati različito napisane iste aktivnosti. *[Z]*
- **R2.8** Inženjer i Supervizor mogu da predlože izmenu (dodavanje, izbacivanje, ispravku teksta) uz obavezan razlog; Direktor i Admin odobravaju ili odbijaju; odobren predlog ulazi u nacrt sledeće verzije. *[K]*
- **R2.9** „Istorijat šablona“ prikazuje sve događaje sa šablonima (prvi uvoz, izmene u nacrtu, odobreni i odbijeni predlozi, objave verzija): datum, šablon, verzija, opis, ko; filteri po šablonu, vrsti događaja, korisniku i periodu. *[O]*
- **R2.10** Izbacivanje ne briše ništa: uređaj ostaje u registru i u istoriji; uređaj koji nije ni u jednom šablonu vidi se na spisku „Nije ni u jednom pregledu“. *[Z]*
- **R2.11** Pored odgovora, aktivnost može da ima dodatno polje. Uvode se tri polja, sva opciona: *[O]*
  - Broj rolni koje ne okreću – DP1 i DP2, „Pregled rolni“ na svim transporterima i mernim vagama; polje se pojavljuje samo uz odgovor „Ne okreće“;
  - Temperatura ležaja (°C) – DP, ventilatori 30-B, 40-B, 50-B i 60-B i mlinovi 30-S-02A/B; dve vrednosti: ležaj do rotora i ležaj do remenice/spojnice; polje se prikazuje samo uz „Greje se“;
  - Broj nedostajućih/oštećenih komada – staza čekića na Sušnici, poluge vibromotora na procesnim sitima (DP); lanci redlera i mlinova, lifteri Sušnice i Hladnjaka, vijci zupčaste spojnice (PZ).
- **R2.12** Tačan spisak gde se polja dodaju je u listu „Predlog polja“ fajla „Analiza napomena DP-PZ.xlsx“ (izveden iz 240 skeniranih obrazaca). *[P]*
- **R2.13** Admin u aplikaciji može da doda novo polje ovog tipa na aktivnost: naziv, tip (broj, merenje sa jedinicom, da/ne) i da li je obavezno. *[O]*
- **R2.14** Početni šabloni su papirni obrasci sa poslednjim datumom. Pre uvoza ažurne verzije sprovode se ručne ispravke sa obrazaca („Sautelma“ umesto „ATM“, „redler“ umesto „Transporter“, izbacivanje aktivnosti koje uređaj nema) i ostale nedoslednosti iz dokumenta od 20.10. *[O]*
- **R2.15** Šabloni se prvi put unose uvozom; posle toga se menjaju u aplikaciji. *[Z]*

### A.3 Raspored i kalendar

- **R3.1** DP se radi svakog radnog dana, naizmenično DP1 pa DP2. PZ se radi redom PZ1 → PZ4. *[K]*
- **R3.2** Propušten DP se prenosi na sledeći radni dan, a ceo raspored DP se pomera za jedan dan. *[K]*
- **R3.3** DP je propušten ako do kraja radnog dana nije ni započet (vreme podešava Admin); aplikacija ga automatski prenosi, preskačući neradne dane i dan PZ-a. Redosled DP1/DP2 ostaje, broj ide sa pregledom. *[O]*
- **R3.4** Inženjeri ostaju na svojim danima, pomeraju se samo pregledi (propušten DP1 sa ponedeljka u utorak radi inženjer raspoređen za utorak). Ako u radu bude trebalo drugačije, menja se. *[O]*
- **R3.5** Započet, a nezavršen pregled nije propušten: ostaje „U radu“, završava ga isti inženjer sledećeg radnog dana, a raspored DP se pomera. *[O]*
- **R3.6** Posle pomeranja Supervizor i Direktor mogu ručno da menjaju raspored. Pomeranje ide u istoriju; obaveštenje dobijaju Supervizor, Direktor i inženjeri kojima se promenio pregled. U kalendaru propušten dan ostaje crven, uz „nije urađen – prenet na …“. *[O]*
- **R3.7** PZ dobija fiksni datum, koji zadaje Supervizor ili Direktor; aplikacija sama zna koji PZ je sledeći na redu. *[K]*
- **R3.8** Pri zakazivanju aplikacija ponudi sledeći PZ po redu; Supervizor ili Direktor može da klikne „Promeni PZ“, izabere drugi i upiše napomenu (nije obavezna). Promena ide u istoriju, a redosled se nastavlja od zakazanog PZ-a. *[O]*
- **R3.9** Na dan PZ-a DP se ne radi. To je podešavanje u admin panelu („Na dan PZ-a DP se: ne radi / radi“). *[O]*
- **R3.10** Aplikacija nedelju dana unapred predlaže raspored: DP dodeljuje redom aktivnim inženjerima (rotacija) i preskače odsutne. Supervizor ili Direktor potvrđuje ili menja. *[O]*
- **R3.11** Inženjer može da zatraži prebacivanje pregleda na drugog (i da predloži na koga); tek kad Supervizor ili Direktor odobri, drugi inženjer može da počne. *[Z]*
- **R3.12** Zahtev za prebacivanje inženjer šalje iz „Moje aktivnosti“ (dugme uz svaki planirani DP ili PZ), sa razlogom (obavezan) i predlogom zamene (nije obavezan); kod PZ-a sa dva inženjera prebacuju se samo njegove sekcije. Zahtev stiže u „Čeka moju odluku“; Supervizor ili Direktor bira zamenu i odobrava ili odbija. *[O]*
- **R3.13** Ko započne pregled, taj ga i završava. Ako inženjer ode na odmor ili bolovanje, Supervizor ili Direktor dodeljuje pregled drugom; pamte se obojica. *[K]*
- **R3.14** PZ uglavnom radi jedan inženjer. Supervizor ili Direktor može da doda još jednog („Dodaj inženjera“) i da svakom dodeli sekcije, uz dugme „Označi sve sekcije“. Svako radi svoje sekcije; pregled se predaje kad su sve gotove, a pamti se ko je šta radio. *[O]*
- **R3.15** Kod PZ-a sa dva inženjera svako na svom telefonu vidi sve sekcije, a upisuje samo u svoje; napredak drugog vidi kad se podaci sinhronizuju. Predati može bilo koji od njih kad su sve sekcije gotove; do tada aplikacija pokazuje šta nedostaje i kod koga. *[O]*
- **R3.16** Neradne dane i praznike unose Admin i Direktor. DP se ne radi neradnim danima; PZ može, ako je zakazan. *[K]*
- **R3.17** Neradni dan se unosi sa datumom od–do, nazivom i vrstom (državni praznik, verski praznik, neradni dan fabrike). *[O]*
- **R3.18** Odsustva inženjera (godišnji odmor, bolovanje, slobodan dan, službeni put, ostalo) unose Supervizor i Direktor na ekranu „Raspored“: inženjer, od–do, vrsta, napomena (nije obavezna). Predlog rasporeda preskače odsutne; ako je odsutni već raspoređen, aplikacija to označi i predloži zamenu. *[O]*
- **R3.19** Inženjer šalje zahtev za odsustvo sa ekrana „Moja odsustva“ (telefon: „Više“, računar: levi meni), gde vidi i status zahteva i ranija odsustva. Odobrava ili odbija Supervizor ili Direktor (razlog odbijanja nije obavezan); odobren zahtev ulazi u odsustva. Obaveštenje o zahtevu dobijaju Supervizor i Direktor, a inženjer o ishodu. *[O]*
- **R3.20** Pri odobravanju ili unosu odsustva aplikacija pokaže sve planirane preglede inženjera u tom periodu; bira se jedna zamena za sve („Prebaci sve na“) ili posebno za pojedini, uz predlog po rotaciji. „Odobri i prebaci“ prebacuje preglede i obaveštava zamene. Već započet pregled se ne prebacuje automatski. *[O]*
- **R3.21** Započet pregled sme da se završi i sledećeg dana (retko). *[K]*
- **R3.22** Kalendar: zeleno završen, plavo overen, crveno nije započet tog dana, žuto u radu, bez boje planiran. Na telefonu je spisak po danima, na tabletu nedeljni, na većim ekranima mesečni prikaz. *[Z]*
- **R3.23** Klik na pregled u kalendaru: urađen otvara pregled (uz „Nazad“), planiran otvara karticu sa brojem, datumom, inženjerom i statusom; Supervizor i Direktor tu imaju „Promeni u rasporedu“. *[O]*

### A.4 Popunjavanje pregleda

- **R4.1** Datum i vreme početka upisuju se automatski i ne mogu da se menjaju; vreme završetka upisuje se pri predaji. *[Z]*
- **R4.2** Pregled može da se radi iz delova. Pri otvaranju aplikacija pita „Nastavi gde si stao“ ili „Kreni od početka“. *[Z]*
- **R4.3** Sva polja odgovora su obavezna; pregled ne može da se preda dok se ne popune. Dugme „✓ Sve neoznačeno u sekciji je ispravno“ postoji, a svaka njegova upotreba se beleži. *[Z]*
- **R4.4** Ispod svake stavke stoje tri dugmeta: Napomena · Akcija · Foto. Napomena je moguća uz svaki odgovor, a obavezna uz neispravan. *[K]*
- **R4.5** Slika: „Fotografiši“ ili „Iz galerije“, nije obavezna i nema poseban komentar (ide uz napomenu stavke). Aplikacija je smanjuje na oko 1–2 MB; najviše 5 slika po stavci. Slika pamti pregled, tip, verziju šablona, sekciju, uređaj, stavku, ko ju je dodao i kada, i da li je iz kamere ili galerije. *[Z]*
- **R4.6** „Nije u funkciji“ se označava za ceo uređaj i važi samo za taj pregled; klikne se svaki put. Ako je uređaj prošli put bio van funkcije, piše „Prošli put: nije u funkciji“ uz dugme „I dalje nije u funkciji“. Pregled može da se preda, a takvi uređaji se posle predaje prikazuju posebno. *[K]*
- **R4.7** Kad inženjer dođe do uređaja sa otvorenom potrebnom akcijom, iskače pop-up: da li je akcija urađena (da / ne). *[K]*

### A.5 Merenje vremena i kontrola

- **R5.1** Telefon beleži vreme svakog odgovora (radi i bez interneta); server pri slanju proverava sat telefona, a veliko odstupanje se označava kontroloru. *[Z]*
- **R5.2** Razmak između dva odgovora veći od granice pauze (za početak 10 min) ne ulazi u aktivno vreme; tu se pregled deli na delove. *[Z]*
- **R5.3** Kontroloru se prikazuju aktivno vreme, raspon (prvo otvaranje – predaja) i broj delova. *[Z]*
- **R5.4** Kontrolori su Supervizor i Direktor. Upozorenja: aktivno vreme ispod minimuma za tip pregleda, sekcija urađena prebrzo, dugme „Sve neoznačeno je ispravno“ upotrebljeno odmah po otvaranju sekcije. *[K]*
- **R5.5** Pragove podešava Admin; određuju se posle 3–4 nedelje merenja bez upozorenja (predlog: polovina uobičajenog trajanja). *[Z]*
- **R5.6** QR kodovi na opremi se prave, ali njihovo skeniranje nije obavezno. QR otvara ekran uređaja. *[K]*

### A.6 Predaja, overa i dorada

- **R6.1** Predat pregled ide u arhivu. Automatski se pravi ceo izveštaj u PDF-u i rezime samo sa problematičnim stavkama (sa linkovima na slike). *[K]*
- **R6.2** Overa je jedan korak, „Overio“; pregled dobija status „Overeno“. Overava Supervizor ili Direktor; pamti se ko je i kada. *[O]*
- **R6.3** Rok za overu je 24 h, računaju se samo radni dani; po isteku ide podsetnik. *[K]*
- **R6.4** „Vrati na doradu“ je jedina akcija za vraćanje pregleda i radi pre i posle overe: *[O]*
  - predlaže se isti inženjer, ali može da se izabere drugi; razlog je obavezan;
  - obaveštenje dobijaju inženjer i onaj ko je overio;
  - prethodna verzija ostaje u istoriji; posle dorade pregled ponovo ide na overu.
- **R6.5** Supervizor ne može da vrati na doradu pregled koji je overio Direktor; samo Direktor može da poništi tuđu overu. *[O]*
- **R6.6** Inženjer može da zatraži doradu svog pregleda („Zatraži doradu“) uz obavezan razlog: *[O]*
  - pre overe odobrava Supervizor ili Direktor, posle overe onaj ko je overio ili Direktor;
  - bez odobrenja ne može sam da menja predat pregled, a dok pregled nije poslat na server ne menja se;
  - vreme dorade ne ulazi u aktivno vreme pregleda.
- **R6.7** Istorija promena beleži sve: dodelu, prebacivanje, overu, doradu, izmene odgovora (staro i novo) – ko, šta i kada. *[Z]*
- **R6.8** Ako je, dok je inženjer bio bez interneta, pregled dodeljen drugom, urađeno se čuva, a Supervizor odlučuje šta važi. *[K]*
- **R6.9** Svaki pregled ima broj: tip, godina i redni broj u godini, posebno za svaki tip (npr. DP1-2026-0142). Dodeljuje se kad pregled uđe u raspored, pa postoji i bez interneta; ako se raspored pomeri, broj ide sa pregledom. Dorada ne menja broj, nego dodaje oznaku (npr. „dorada 1“). Broj se vidi svuda: liste, kalendar, Dashboard, PDF i Word, mejl i push obaveštenja, pretraga i izvoz. *[O]*
- **R6.10** Pregled se ne briše. Umesto toga Supervizor ili Direktor može da ga poništi („Poništi pregled“) uz obavezan razlog: *[O]*
  - važi za pregled koji je u radu, čeka overu, overen ili vraćen na doradu; Supervizor ne može da poništi pregled koji je overio Direktor;
  - pregled ostaje u arhivi kao „Poništen“ (ko, kada, zašto), ne ulazi u analitiku, a broj se ne koristi ponovo;
  - akcije i kvarovi iz tog pregleda ostaju; obaveštenje dobijaju inženjer i onaj ko je overio.

### A.7 Potrebne akcije

- **R7.1** Akcija ima vrstu (Očistiti / Podmazati ili dopuniti / Zameniti / Popraviti ili sanirati / Ostalo), hitnost i kratak opis (opciono). *[K]*
- **R7.2** Hitnost: Hitno (do kraja smene ili sutra; odmah ide obaveštenje) / U narednih 7 dana / U sledećem planskom zastoju / Pratiti (bez roka). *[O]*
- **R7.3** Ekran „Potrebne akcije“ (i na Dashboardu) prikazuje sve otvorene akcije, sa filterima po hitnosti, sekciji i uređaju. *[K]*
- **R7.4** Akciju zatvara inženjer kroz pop-up na sledećem pregledu koji sadrži taj uređaj (i kad je akcija nastala u PZ-u); ako nije urađena, ostaje otvorena. *[K]*
- **R7.5** Supervizor i Direktor mogu sa ekrana „Potrebne akcije“ da zatvore ili otkažu akciju, uz razlog. *[O]*
- **R7.6** Ekran „Potrebne akcije“ ima kolonu „Pregled“; klik na broj pregleda otvara pregled. Klik na akciju otvara ekran akcije: podaci (uređaj, aktivnost, vrsta, opis, hitnost, rok, ko je prijavio, iz kog pregleda, kvar na stavci) i istorija akcije; Supervizor i Direktor tu mogu da je zatvore ili otkažu. *[O]*
- **R7.7** Svaki ekran detalja (akcija, pregled, uređaj) ima dugme „Nazad“ koje vraća na ekran sa kog je otvoren. Klik na logo u zaglavlju uvek vodi na početnu stranu. *[O]*
- **R7.8** Kad akciji istekne rok, postaje crvena na Dashboardu i ide jedno obaveštenje. *[O]*
- **R7.9** „Urađeno tokom pregleda“ se ne vodi kao posebno polje; po potrebi ide u napomenu. *[K]*
- **R7.10** Kad se uvede modul održavanja, akcija može da postane radni nalog. *[Z]*
- **R7.11** Svaka akcija ima broj (npr. A-2026-0031) i pamti iz kog pregleda je nastala; broj se vidi u pop-up-u, na ekranu „Potrebne akcije“ i u obaveštenjima. *[O]*
- **R7.12** Akcija može da ima zaduženog (osobu ili službu: Održavanje, Elektro, Mašinska, Spoljni izvođač); nije obavezno. Dodeljuju Supervizor i Direktor na ekranu akcije; zaduženi dobija obaveštenje, dodela ide u istoriju akcije. *[O]*
- **R7.13** Akcija i kvar su vezani preko iste stavke (uređaj + aktivnost): akcija pokazuje stanje kvara na toj stavci, kvar pokazuje akcije nastale iz njega; isto se vidi u dosijeu uređaja. *[O]*
- **R7.14** Ako inženjer kroz pop-up zatvori akciju kao urađenu, a na istoj stavci izabere neispravan odgovor, aplikacija ga upozori i ponudi novu akciju (nije obavezno; kvar ostaje otvoren). *[O]*
- **R7.15** Ako se kvar zatvori, a akcija na toj stavci je još otvorena, akcija dobija oznaku „stavka sada ispravna“ (ekran „Potrebne akcije“ i Dashboard, uz filter), da Supervizor ili Direktor odluči da li je zatvara ili otkazuje. *[O]*

### A.8 Neispravnosti, ekran uređaja, analitika i Dashboard

- **R8.1** Ekran neispravnosti: filteri po sekcijama i nazivima; za isti uređaj vide se svi datumi prijave. Kvar se smatra otvorenim dok sledeći pregled ne kaže „ispravno“ (filter „trenutno otvoreno“, trajanje kvara). *[Z]*
- **R8.2** Ponovljeni kvar (npr. ista stavka neispravna 3 puta u 30 dana) šalje obaveštenje; pragove podešava Admin. *[Z]*
- **R8.3** Kad je uređaj van funkcije duže od granice (Admin podešava, predlog 30 dana), ide obaveštenje izabranim primaocima i ponavlja se na svakih toliko dana dok je van funkcije. *[O]*
- **R8.4** Ekran uređaja: status (OK, otvoren kvar, nije u funkciji, nije pregledan X dana), poslednji pregled, istorija u bojama, sve napomene i slike („dosije“), u kojim je šablonima, statistika. Radi i bez interneta za uređaje iz dodeljenih pregleda. *[K]*
- **R8.5** Na ekranu uređaja istorija ima klizač za period (od 30 dana do godinu dana unazad). Kartica „Pregledi uređaja“ ima dva taba: „Svi pregledi“ (broj, datum, tip, inženjer, stanje uređaja na tom pregledu, status pregleda; sortiranje, filteri, izvoz) i „Dosije“ (napomene, slike, akcije). Klik na pregled bilo gde na ekranu otvara taj pregled, sa stavkama ovog uređaja izdvojenim na vrhu. *[O]*
- **R8.6** Analitika po uređaju: vremenska linija (ispravan, neispravan, nije u funkciji), broj dana bez kvara (trenutno i najduže), broj dana od poslednjeg kvara, trajanje svakog kvara i prosek, broj kvarova u periodu, stavka koja se najčešće kvari. *[O]*
- **R8.7** Pravilo računanja: između dva pregleda važi stanje sa poslednjeg pregleda; kvar traje od pregleda koji ga je prijavio do prvog pregleda „ispravno“; dani „nije u funkciji“ se ne računaju ni kao ispravni ni kao kvar. *[O]*
- **R8.8** Opšta analitika: neispravnosti po sekciji, uređaju i aktivnosti kroz vreme, najčešći kvarovi, prosečno trajanje kvara, trajanje pregleda po tipu i inženjeru, „nije u funkciji“ po uređaju. *[O]*
- **R8.9** Ispunjenje plana (analitika i broj na Dashboardu): procenat pregleda urađenih na planirani dan, propušteni i preneti, završeni sledećeg dana, procenat overenih u roku i prosečno kašnjenje overe; po mesecu i po inženjeru. Dani odsustva se ne računaju inženjeru; preneti pregled se računa onom kome je bio dodeljen. *[O]*
- **R8.10** Dashboard: današnji i sutrašnji pregledi i status, pregledi koji čekaju overu, otvorene akcije po hitnosti, otvoreni kvarovi, uređaji van funkcije. Svi vide sve, uz oznake kome je dodeljeno, ko je prijavio i ko je overio; inženjeru je na vrhu „Moje aktivnosti“. *[O]*
- **R8.11** „Moje aktivnosti“ kod inženjera: današnji pregled, započeti a nezavršeni, planirani u narednih 7 dana (uz „Zatraži prebacivanje“), zahtevi na čekanju i akcije na uređajima iz njegovih pregleda. Kasniji pregledi su u Kalendaru. *[O]*
- **R8.12** Supervizor i Direktor na Dashboardu imaju karticu „Čeka moju odluku“: overa, zahtevi za doradu, prebacivanje i odsustvo, a za Direktora i predlozi izmena šablona. Klik vodi na mesto odluke; ukupan broj stoji i na zvoncu. *[O]*
- **R8.13** Sve tabele imaju sortiranje po kolonama, filtere (datum od–do, tip pregleda, sekcija, uređaj, inženjer, status, hitnost…) i pretragu. Izvoz uzima filtrirano; filteri se pamte po korisniku, uz „Poništi filtere“. Na telefonu je dugme „Filteri“. *[O]*
- **R8.14** U zaglavlju je pretraga: broj pregleda, akcije ili kvara, ili deo naziva uređaja; klik na rezultat otvara taj ekran. Pronađeni deo teksta se žuto obeležava (npr. „S12“ u S12, IS12, AS12). *[O]*
- **R8.15** Svaki kvar ima broj (npr. K-2026-0087) i pamti iz kog pregleda je prijavljen. *[O]*
- **R8.16** Ekran „Neispravnosti“ ima kolonu „Pregled“; klik na broj pregleda otvara pregled, klik na uređaj otvara uređaj, a klik na red otvara ekran kvara (podaci, akcije na toj stavci, istorija kvara, „Nazad“). *[O]*

### A.9 Obaveštenja

- **R9.1** Obaveštenja stižu na telefon (push) i mejlom. *[K]*
- **R9.2** Ako DP nije započet do 13h, ide upozorenje; za PZ važi isto, sa posebnim vremenom. Oba vremena podešava Admin. *[Z]*
- **R9.3** Inženjer dobija obaveštenje o dodeli, promeni dodele, vraćanju na doradu i ishodu svojih zahteva i predloga. *[Z]*
- **R9.4** Primaoce obaveštenja (13h, rezime, podsetnik za overu, hitne akcije, istekao rok akcije, ponovljeni kvar, dugo van funkcije) Admin bira štikliranjem; podrazumevano Supervizor i Direktor. *[O]*
- **R9.5** Bez interneta obaveštenja stižu kad se veza vrati. Upozorenje u 13h za pregled započet bez interneta je prihvatljivo. *[K]*

### A.10 Rad bez interneta i telefon

- **R10.1** Kad ima interneta, aplikacija preuzima šablone, raspored, dodeljene preglede i započete nacrte. *[Z]*
- **R10.2** Ceo pregled, uključujući napomene i slike, popunjava se bez interneta; slanje je automatsko kad se veza vrati. *[Z]*
- **R10.3** Pregled predat bez interneta ima status „Predato – čeka slanje“; stalno se vidi koliko stavki i slika nije poslato. *[Z]*
- **R10.4** Inženjer na telefonu vidi „Poslednja sinhronizacija: HH:MM“ (na početnoj strani i u pregledu), da zna da je sve stiglo na server. Kad se veza vrati, aplikacija sama šalje sve urađeno i preuzima raspored, dodeljene preglede, šablone, akcije i obaveštenja. *[O]*
- **R10.5** Započet pregled je vezan za jedan telefon; na drugom može da se nastavi tek kad prvi pošalje podatke. Ako se šablon promeni dok je inženjer bez interneta, pregled se završava po staroj verziji. *[Z]*
- **R10.6** Aplikacija se instalira na početni ekran telefona kroz Chrome (Android), čime rade push obaveštenja i ne gube se neposlati podaci. *[K]*
- **R10.7** Prva prijava je sa internetom; posle toga inženjer ostaje prijavljen i radi bez interneta. *[Z]*
- **R10.8** Aplikacija je responzivna za telefon, tablet, laptop i veliki monitor: na telefonu donji meni, kartice umesto tabela, jedan uređaj u fokusu i velika dugmad; na većim ekranima bočni meni i pune tabele. *[Z]*
- **R10.9** Osnovna pravila prikaza (važe za sve ekrane): *[O]*
  - Aplikacija radi na svim širinama ekrana, od telefona (360 px) do velikog monitora (1920 px i više), u Chrome-u i Edge-u.
  - Ni na jednom ekranu stranica ne sme da se pomera bočno i ništa ne sme da izađe iz okvira ekrana, kartice ili dijaloga — ni sa ni bez interneta, ni za jednu rolu.
  - Dugačak tekst se prelama u novi red; brojevi (DP1-2026-0142, A-2026-0031, K-2026-0087) se ne lome; oznake (hitnost, status) u tabelama mogu u dva reda; trake sekcija i tabovi prelaze u novi red umesto da beže van ekrana.
  - Tabele na laptopu od 1366 px staju na ekran bez bočnog pomeranja (naslovi kolona mogu u dva reda); na telefonu su kartice, naziv polja levo, vrednost pored njega.
  - Poravnanje: polja i dugmad u istom redu su iste visine i poravnati; sadržaj kartica je poravnat po istoj levoj ivici; ekrani su centrirani u radnom delu.
  - Na telefonu: dugmad najmanje 44 × 44 px; zaglavlje ima samo logo i ikonice (🔍 🔔 👤); obaveštenje „Bez interneta“ je traka ispod zaglavlja, ne u zaglavlju.
  - Dijalozi nisu širi od ekrana; ako je sadržaj duži, pomera se unutar dijaloga. Grafikoni se šire prema kartici.
  - Pre isporuke svaki ekran se proverava za svaku rolu na širinama 375, 768, 1366 i 1920 px, sa i bez interneta: automatski test da nema bočnog pomeranja i ničega van okvira, plus ručni pregled.

### A.11 Izvoz, štampa i čuvanje

- **R11.1** Pregled može da se odštampa (vidi se i verzija šablona), ali se realno neće štampati; papirni obrazac sa potpisom se ukida. *[K]*
- **R11.2** Pojedinačan pregled se izvozi u PDF i Word, a liste (arhiva, neispravnosti, akcije) u Excel. *[O]*
- **R11.3** Pregledi i fotografije se čuvaju trajno. *[K]*
- **R11.4** Stari papirni pregledi se ne unose u aplikaciju. *[O]*

### A.12 Admin panel

- **R12.1** Admin podešava: *[O]*
  - korisnike, role i prava pristupa (štikliranjem);
  - neradne dane i praznike (i Direktor);
  - granicu pauze, minimalno trajanje po tipu pregleda i po sekciji, dozvoljeno odstupanje sata telefona;
  - vreme upozorenja „nije započet“ za DP i za PZ i da li se DP radi na dan PZ-a;
  - pragove za ponovljeni kvar;
  - primaoce svih obaveštenja;
  - dodatna polja na aktivnostima.

### A.13 Tehnologija i faze isporuke

- **R13.1** Aplikacija se pravi po Elixir blueprint-u: .NET 10, PostgreSQL 16, React 19 + TypeScript + Tailwind, Docker, polazeći od gotovog skeleta. *[Z]*
- **R13.2** Odstupanja od blueprint-a (upisuju se u docs/odluke.md): mobilni prikaz i PWA, otpremanje fotografija, push obaveštenja, zakazane provere i rad bez interneta, prava pristupa štikliranjem. *[O]*
- **R13.3** Sve opisano u ovom dokumentu isporučuje se u prvoj fazi. Vremena odgovora se skupljaju od prvog dana; pragovi upozorenja o vremenu podešavaju se posle 3–4 nedelje podataka. *[O]*
- **R13.4** Sledeća faza je modul za pumpe i trake (nije deo DP i PZ; sadržaj se definiše kasnije). *[O]*
- **R13.5** Aplikacija je platforma: zajednički registar opreme, nalozi, slike, obaveštenja, kalendar i rad bez interneta koriste i budući moduli (održavanje, pumpe i trake), koji nisu deo ove verzije. Pumpe i trake nisu deo DP i PZ pregleda; to je poseban modul za sledeću verziju, a sadržaj se definiše kasnije. *[Z]*

### A.14 Odloženo za kraj

- **R14.1** Sređivanje šablona i uvoz ažurne verzije. *[O]*
- **R14.2** IT: slanje mejlova (Microsoft 365), server i domen sa HTTPS-om. *[O]*
- **R14.3** Mesto gde se piše specifikacija za programera (predlog: repozitorijum projekta, po šablonu blueprint-a). *[O]*
