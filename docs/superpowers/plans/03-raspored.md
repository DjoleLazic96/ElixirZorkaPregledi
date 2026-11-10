# Faza 03 — Raspored i kalendar

**Cilj:** Supervizor i Direktor prave raspored DP-a za nedelju (predlog po rotaciji → potvrda, pregledi dobijaju brojeve),
zakazuju PZ, unose odsustva i neradne dane i odlučuju o zahtevima; propušten DP se sam prenosi; svi vide kalendar.
**Pravila:** R3.1–R3.23, R6.9.
**Uslov:** faze 01 i 02.

## Odluke za ovu fazu

- **Radni dan** = ponedeljak–petak koji nije u `neradni_dan`. DP se radi samo radnim danom i, ako je „Na dan PZ-a DP se ne radi“, ne na dan PZ-a.
- **Pregled nastaje potvrdom rasporeda** (status `planiran`, broj `DP1-2026-0142` iz brojača po godini planiranog datuma). Verzija
  šablona se vezuje tek pri početku pregleda (faza 04), da započet pregled ostane na svojoj verziji, a planiran dobije važeću.
- **Rotacija (R3.10):** aktivni korisnici sa pravom `pregledi.popunjavanje`, po imenu; nastavlja se od poslednjeg dodeljenog DP-a;
  ko je tog dana na odobrenom odsustvu se preskače. Tip DP-a se nastavlja od poslednjeg planiranog (DP1 → DP2 → DP1…).
- **Sledeći PZ (R3.7–R3.8):** posle poslednjeg zakazanog PZ-a po redu PZ1 → PZ4 → PZ1; prvi je PZ1. Drugi izbor je „Promeni PZ“
  (napomena opciona, ide u istoriju), a redosled se nastavlja od zakazanog. Isti PZ ne može biti dva puta planiran (422).
  PZ sme na neradni dan. Ako se PZ zakaže na dan planiranog DP-a, a DP se tog dana ne radi, DP-ovi od tog dana se pomeraju kao kod prenosa.
- **Prenos (R3.2–R3.6):** pozadinski posao posle „Kraja radnog dana“ (podešavanje) jednom dnevno uzima današnji DP koji nije
  započet. Pregledi od tog dana se pomeraju za jedno mesto: brojevi i tipovi idu napred, **inženjeri ostaju na svojim danima**
  (propušten DP1 sa ponedeljka u utorak radi inženjer raspoređen za utorak); poslednji pregled dobija sledeći radni dan i
  inženjera po rotaciji. Započet DP (`u_radu`) ide na sledeći radni dan sa istim inženjerom (R3.5). Svaki prenos je događaj
  `prenos` (stari i novi datum); kalendar na starom danu pokazuje „nije urađen – prenet na …“. Posao je idempotentan
  (pamti poslednji obrađen dan u `podesavanje._poslednjiPrenos`) i dostupan i kao `POST /raspored/prenesi` za ručno pokretanje.
  Obaveštenja o pomeranju su faza 09.
- **Odsustvo:** Supervizor/Direktor ga unosi (odmah `odobreno`), Inženjer šalje zahtev (`zahtev`), odluka nosi i
  `prebacivanja[{pregledId, zamenaId}]` (R3.20). Preklapanje sa postojećim neodbijenim odsustvom → 422. Započet pregled se ne prebacuje automatski (422).
- **Zahtev za prebacivanje (R3.11–R3.12):** Inženjer za svoj `planiran` pregled, razlog obavezan, predlog zamene opcion;
  Supervizor/Direktor bira zamenu i odobrava ili odbija. Kod PZ-a sa dva inženjera prebacuje se samo njegov deo (učesnik).
- Ručne izmene (R3.6): `PUT /pregledi/{id}/dodela` (inženjer i/ili datum) i `POST /pregledi/{id}/ucesnici` (PZ, sekcije), sve u istoriji.

## Tabele (migracija `Raspored`)

`neradni_dan` (datum unique, naziv, vrsta `drzavni`/`verski`/`fabrika`, uneo_id) · `odsustvo` (korisnik, od, do, vrsta
`godisnji`/`bolovanje`/`slobodan_dan`/`sluzbeni_put`/`ostalo`, napomena, status `zahtev`/`odobreno`/`odbijeno`, zatražio, odlučio, razlog odbijanja, vremena) ·
`pregled` (broj unique, tip, sablon_verzija_id null, planirani_datum, prvobitni_datum, status, dodeljen_id, napomena, kreirano_at) ·
`pregled_ucesnik` (pregled, korisnik, sekcije text[]) · `pregled_dogadjaj` (pregled, vrsta, podaci jsonb, korisnik, at) ·
`zahtev_prebacivanja` (pregled, podneo, predložena zamena, razlog, status, zamena, odlučio, vremena).
Kolone za popunjavanje (početak, predaja, vreme, uređaj klijenta…) dodaje faza 04.

## API

| Metod | Ruta | Pravo |
|---|---|---|
| GET | `/raspored?od&do` | ulogovan — pregledi, neradni dani, odobrena odsustva u periodu (kalendar) |
| POST | `/raspored/predlog` `{ od }` | raspored.uredjivanje — predlog za nedelju (ne upisuje) |
| POST | `/raspored/potvrdi` `{ stavke[{datum, tip, inzenjerId}] }` | raspored.uredjivanje — pravi preglede i brojeve |
| POST | `/raspored/pz` `{ datum, tip?, inzenjerId, napomena? }` · GET `/raspored/pz/sledeci` | raspored.uredjivanje |
| POST | `/raspored/prenesi` | raspored.uredjivanje — ručno pokretanje prenosa |
| PUT | `/pregledi/{id}/dodela` · POST `/pregledi/{id}/ucesnici` | raspored.uredjivanje |
| GET · POST · DELETE | `/neradni-dani` | ulogovan · neradni_dani |
| GET · POST | `/odsustva`, `/odsustva/zahtev`, `/odsustva/{id}/odluka`, `/odsustva/{korisnikId}/pregledi` | ulogovan (svoja) / odsustva.odobravanje |
| GET · POST | `/zahtevi-prebacivanja`, `/zahtevi-prebacivanja/{id}/odluka` | Inženjer (svoj pregled) / raspored.uredjivanje |

## Frontend

- `/kalendar` (svi): mesec na računaru, spisak po danima na telefonu; boje R3.22; propušten dan crven sa „prenet na …“; klik na
  planiran pregled otvara karticu (broj, datum, inženjer, status; „Zatraži prebacivanje“ za svoj, „Promeni u rasporedu“ za Supervizora/Direktora).
- `/raspored` (Supervizor, Direktor): nedelja, „Predloži raspored“ → izmena inženjera → „Potvrdi“; „Zakaži PZ“ / „Promeni PZ“;
  zahtevi za prebacivanje i odsustva na čekanju; unos odsustva sa prebacivanjem („Prebaci sve na“).
- `/moja-odsustva` (Inženjer): zahtev i spisak. `/neradni-dani` (Admin, Direktor).

## Testovi

Unit: `PomeranjeRasporedaTests` (neradni dan, dan PZ-a, redosled DP1/DP2, inženjeri ostaju na danima, započet ide sa istim
inženjerom, idempotentno), predlog (rotacija, preskače odsutne i neradne dane), sledeći PZ i „već zakazan“, preklapanje odsustava,
prebacivanje započetog → 422, brojevi po godini. Integracioni: 401/403 po ruti, predlog → potvrda → kalendar.
