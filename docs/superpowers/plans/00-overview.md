# TPM pregledi — plan rada po fazama

**Datum:** 2026-11-04
**Spec:** `docs/superpowers/specs/2026-10-27-tpm-pregledi-design.md` (Prilog A = pravila R1.1…R14.3)
**Status:** u `TODO.md`, ne ovde.

> Prva isporuka (ceo spec) je podeljena na faze koje se rade redom. Svaka faza se završava nečim što radi i može da
> se pokaže (backend + frontend + testovi + docs u istom PR-u, po blueprintu). Detaljan plan faze (`NN-*.md`) piše se
> kad faza krene, da bi uzeo u obzir ono što je naučeno u prethodnoj. Gotovi detaljni planovi: 01, 02.

## Pravila za sve faze

- Novi feature = kopija obrasca `Kategorije` (backend `Features/<X>`, frontend `features/<x>`); `Kategorije` se brišu u fazi 02.
- Svaka faza: nova EF migracija (stare se ne diraju), `docs/02` i `docs/03` ažurni, red u `CHANGELOG [Neobjavljeno]`, korisničko uputstvo za ekrane te faze.
- Testovi po blueprintu 16: pravilo koje se krši (unit), 401/403 po svakoj novoj ruti (integracioni), guard rute na frontu.
- Prikaz po §8.1 od prve faze: svaki novi ekran radi na 375 / 768 / 1366 / 1920 px bez bočnog pomeranja.
- Odstupanja od blueprinta iz `docs/odluke.md` (PREDLOG) ulaze u kod tek kad ih vlasnik odobri; faza koja ih traži to ima kao uslov za početak.
- Lokalno okruženje: Postgres na 55434 (`scripts\dev-db-start.cmd`), API 8080, Vite 5173. Integracioni testovi traže Docker.

## Faze

| # | Faza | Šta se isporučuje | Pravila | Uslov za početak |
|---|---|---|---|---|
| 01 | Osnova: role, prava, podešavanja | četiri role, prava štikliranjem, korisnici sa imenom i rolom, sekcije, brojači, tabela podešavanja; meni po pravima | R1.4–R1.12, R12.1 (deo) | PREDLOG „četiri role i prava" odobren |
| 02 | Oprema i šabloni | registar opreme, katalog aktivnosti, dodatna polja, šabloni sa verzijama, uvoz `.xlsx` (svih 6 šablona), pregled šablona i registra | R2.1–R2.3, R2.5, R2.7, R2.10–R2.15 | 01 |
| 03 | Raspored i kalendar | neradni dani, odsustva i zahtevi, predlog i potvrda rasporeda (brojevi pregleda), PZ i „Promeni PZ", prebacivanja, pomeranje propuštenog DP-a, kalendar | R3.1–R3.23, R6.9 | 02 |
| 04 | Popunjavanje (sa internetom) | ekran popunjavanja na telefonu i računaru: odgovori, napomena, dodatna polja, „Nije u funkciji", „Sve neoznačeno je ispravno", slike, PZ sa dva inženjera, predaja; merenje aktivnog vremena | R4.1–R4.6, R5.1–R5.4, R3.14–R3.15 | 03; PREDLOZI „mobilni prikaz" i „fotografije" |
| 05 | Rad bez interneta | PWA (instalacija), `moj-paket`, IndexedDB, outbox + `sinhronizacija` (idempotentno, `opId`), slike u redu za slanje, jedan telefon, sporan unos, „Zapamti me" | R10.1–R10.7, R6.8, R1.11 | 04; PREDLOZI „PWA", „duža sesija", „biblioteke" |
| 06 | Overa, dorada, arhiva, izveštaji | arhiva i detalj pregleda, overa, vrati na doradu, zahtev za doradu, poništavanje, istorija, PDF/Word pregleda, rezime problematičnih, Excel izvoz lista | R6.1–R6.10, R11.1–R11.3, R8.13 | 04 (05 može paralelno) |
| 07 | Akcije i kvarovi | akcije (broj, vrsta, hitnost, zaduženi, zatvaranje/otkazivanje), pop-up na pregledu, kvarovi (otvaranje/zatvaranje iz predaje), ekran „Neispravnosti" | R7.1–R7.15, R8.1, R8.15–R8.16 | 06 |
| 08 | Uređaj, analitika, Dashboard, pretraga | ekran uređaja (istorija, dosije, QR), analitika po uređaju i opšta, ispunjenje plana, Dashboard i „Moje aktivnosti", „Čeka moju odluku", pretraga u zaglavlju | R8.4–R8.14, R5.6 | 07 |
| 09 | Obaveštenja i pozadinski poslovi | zvono, Web Push, mejl (Graph; fake dok IT ne da nalog), primaoci, poslovi iz spec §5.2, podešavanja (pragovi, vremena) | R9.1–R9.5, R6.3, R7.8, R8.2–R8.3, R12.1 | 08; PREDLOG „push i zakazane provere" |
| 10 | Izmene šablona u aplikaciji | nacrt, razlike, objava sa datumom, predlozi izmena, istorijat šablona, „Nije ni u jednom pregledu", uputstva za aktivnosti (tekst + slike) | R2.4, R2.6, R2.8–R2.10 | 02 (može posle 04) |
| 11 | Završna provera i isporuka | test prikaza (svaki ekran × rola × širina × sa/bez interneta), korisnička uputstva, check-lista PWA/push na Android Chrome-u, deploy paket | R10.8–R10.9, §10–§11 spec-a | sve iznad; IT (domen, HTTPS, Graph) |

Redosled je izabran tako da se što pre dođe do popunjavanja pregleda (04) na pravim šablonima, jer je to ono što inženjeri
koriste svaki dan; overa i analitika se grade na podacima koje popunjavanje proizvodi.

## Ulazni podaci

| Šta | Gde | Koristi faza |
|---|---|---|
| Šabloni za uvoz (6 šablona, 867 stavki) | `../Šabloni – za uvoz.xlsx` → u repo kao `docs/sabloni/sabloni-za-uvoz.xlsx` | 02 |
| Predlog dodatnih polja | već u koloni „Dodatno polje" šablona za uvoz | 02 |
| Mockup svih ekrana | `docs/mockup/index.html` | sve |

Šabloni DP1, DP2 i PZ1 prepisani su tačno kao na obrascu (jedina izmena: Merna vaga Sautelma 10-C-07C na DP-u);
PZ2–PZ4 imaju ispravke koje je zadao korisnik. Kako se isti uređaj zapisan različito na raznim obrascima vezuje u
registru — u planu faze 02.

## Van ove isporuke

Pumpe, Trake i Plansko stajanje (druga faza, spec §12) — registar opreme iz faze 02 mora da primi i uređaje koji nisu u
DP/PZ šablonima, ništa više se ne priprema unapred.
