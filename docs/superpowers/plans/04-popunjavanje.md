# Faza 04 — Popunjavanje pregleda (sa internetom)

**Cilj:** inženjer na telefonu (i računaru) započne dodeljen pregled, popuni ga stavku po stavku — odgovor, napomena, dodatna
polja, „Nije u funkciji“, slike, „Sve neoznačeno u sekciji je ispravno“ — i preda ga; aplikacija meri aktivno vreme.
Mobilni raspored aplikacije (donji meni) ulazi u ovoj fazi.
**Pravila:** R4.1–R4.6, R5.1–R5.3, R3.14–R3.15, R10.8–R10.9 (deo).
**Uslov:** faza 03; odobreni „mobilni prikaz“ i „otpremanje fotografija“ (2026-11-04).

## Odluke za ovu fazu

- **Operacije od prvog dana.** Svaka radnja u pregledu je operacija sa `opId` (UUID sa klijenta) i vremenom sa klijenta, i ide
  kroz `POST /sinhronizacija` — isti ugovor koji faza 05 koristi za rad bez interneta. Ponovljen `opId` je „već primenjeno“
  (tabela `sinhronizacija_operacija`). U fazi 04 klijent šalje operaciju odmah; faza 05 dodaje red za slanje (outbox) i PWA.
- Vrste operacija: `odgovor` (`stavkaId`, `vrednost` 0/1, `napomena`, `polja`), `van_funkcije` (`uredjajId`, `vanFunkcije`),
  `sve_ispravno` (`sekcija`), `predaja`. Početak je `POST /pregledi/{id}/zapocni` (vezuje važeću verziju šablona, R2.5, R4.1).
- **Jedan važeći odgovor po stavci** (`odgovor`, jedinstveno pregled+stavka+dorada); promena odgovora piše staro/novo u
  `pregled_dogadjaj` (R6.7).
- **Ko upisuje:** dodeljeni inženjer; na PZ-u sa dva inženjera svako samo u svoje sekcije (R3.15) — dodeljeni radi sekcije koje
  nisu dodeljene učesnicima.
- **Predaja (R4.3, R4.6):** svaka stavka mora imati odgovor, osim stavki uređaja označenih „Nije u funkciji“; uz neispravan
  odgovor napomena je obavezna. Inače 422 „Pregled još nije potpun: nedostaje N odgovora i M obaveznih napomena.“
  PZ: 422 sa sekcijama koje nedostaju i kod koga.
- **Aktivno vreme (R5.2, R5.3):** iz vremena operacija sa klijenta; razmak veći od granice pauze (podešavanje) ne ulazi i deli
  pregled na delove. Čuva se `aktivno_sekundi` i `delova` pri predaji. Upozorenja kontroloru su faza 06.
- **Slike (R4.5):** klijent smanjuje na JPEG, duža strana ≤ 2048 px; server prima ≤ 3 MB (400), najviše 5 po stavci (422),
  `klijentId` je idempotentnost. Fajlovi na volumenu `Fajlovi:Putanja` (podrazumevano `data/fotografije`), u `fajl` putanja i SHA-256.
- „Prošli put: nije u funkciji“ (R4.6): ako je uređaj bio van funkcije na poslednjem predatom pregledu koji ga sadrži.
- Pop-up otvorene akcije (R4.7) je faza 07; provera sata telefona (R5.1) faza 05.

## Tabele (migracija `Popunjavanje`)

`pregled` + `zapocet_at`, `predat_at`, `aktivno_sekundi`, `delova`, `dorada` · `odgovor` (pregled, stavka, vrednost, napomena,
polja jsonb, korisnik, klijent_at, server_at, op_id, dorada) · `uredjaj_van_funkcije` (pregled, uređaj, korisnik, klijent_at) ·
`fotografija` (pregled, stavka, fajl, izvor, korisnik, klijent_id unique, kreirano_at) · `sinhronizacija_operacija` (op_id PK, pregled, vrsta, primljeno_at).

## API

| Metod | Ruta | Pravo |
|---|---|---|
| POST | `/pregledi/{id}/zapocni` | pregledi.popunjavanje (svoj pregled) |
| GET | `/pregledi/{id}/popunjavanje` | pregledi.popunjavanje — šablon verzije, odgovori, van funkcije, slike, moje sekcije, „prošli put“ |
| POST | `/sinhronizacija` `{ uredjajKlijenta, klijentSada, operacije[] }` | pregledi.popunjavanje → `{ prihvacene[], odbijene[{opId, razlog}], serverSada }` |
| POST · GET | `/fotografije` (multipart) · `/fotografije/{id}` | pregledi.popunjavanje · ulogovan |

## Frontend

- Mobilni raspored: ispod 768 px nema bočnog menija; donji meni (4 stavke + „Više“), zaglavlje sa logom i ikonicom korisnika.
- `/pregled/:id/popuni`: „Započni pregled“; trake sekcija (prelaze u novi red); kartica uređaja sa „Nije u funkciji“; uz aktivnost
  dva velika dugmeta; Napomena · Foto (kamera ili galerija); dodatna polja uz neispravan odgovor; „✓ Sve neoznačeno u sekciji je
  ispravno“; brojač popunjenosti; „Predaj pregled“. „Nastavi gde si stao“ skroluje do prve nepopunjene stavke (R4.2).
- Kartica pregleda u kalendaru dobija „Popuni pregled“ za svoj pregled.

## Testovi

Unit: aktivno vreme i delovi; idempotentnost `opId`; promena odgovora u istoriji; predaja nepotpunog (N i M); van funkcije ne traži
odgovore; PZ sekcije tuđe → odbijeno; šesta slika → 422. Frontend: neispravan traži napomenu i prikazuje polje; donji meni na telefonu.
