# 6. Audit log i stanje servera

> **Za koga:** administrator. **Cilj:** znate da pronađete ko je, kada i šta promenio, i da pročitate da li je serveru dobro.

## Sadržaj

- [6.1 Audit log — gde je i šta beleži](#61-audit-log--gde-je-i-šta-beleži)
- [6.2 Filtriranje](#62-filtriranje)
- [6.3 Čitanje izmene](#63-čitanje-izmene)
- [6.4 Tipični scenariji](#64-tipični-scenariji)
- [6.5 Stanje servera](#65-stanje-servera)
- [6.6 Najčešći problemi](#66-najčešći-problemi)

## 6.1 Audit log — gde je i šta beleži

Levi meni → **Audit log**. Svaki red je jedan događaj: **Vreme**, **Korisnik**, **Akcija**, **Entitet**, **ID**, **IP**.

| Akcija | Značenje |
|---|---|
| Kreiranje | nov zapis (npr. nova kategorija, nov korisnik) |
| Izmena | promena postojećeg zapisa (i deaktivacija/aktivacija) |
| Brisanje | fizičko brisanje (retko) |
| Prijava | uspešna prijava korisnika |
| Neuspešna prijava | pogrešno ime ili lozinka; u koloni **Korisnik** je uneto ime |
| Odjava | korisnik se odjavio |
| Pokretanje | aplikacija je pokrenuta (posle deploy-a ili restarta), sa verzijom |

Zapisi se ne mogu menjati ni brisati i čuvaju se trajno.

## 6.2 Filtriranje

Iznad tabele: **Korisnik** (deo imena), **Entitet** (lista), **Akcija** (lista), **Od datuma** / **Do datuma**. Filteri se primenjuju odmah i vraćaju na prvu stranicu.

## 6.3 Čitanje izmene

Kliknite na red. Za **Izmena** vidite samo promenjena polja: stara vrednost precrtana crveno, nova zeleno. Za **Kreiranje** sve vrednosti novog zapisa, za **Brisanje** sve vrednosti obrisanog.

> **Napomene:** jedna prijava pravi dva reda — „Prijava" i „Izmena" korisnika (zapisuje se vreme poslednje prijave). Ako piše „Nema prikazanih izmena", promenjena su samo zaštićena polja (npr. lozinka), koja se ne prikazuju.

## 6.4 Tipični scenariji

- **Ko je promenio kategoriju?** Entitet = Kategorija, pronađite red sa odgovarajućim **ID**, kliknite.
- **Da li je neko pokušavao da uđe na tuđi nalog?** Akcija = Neuspešna prijava; pogledajte **Korisnik** i **IP**.
- **Kad je poslednji put aplikacija ažurirana?** Akcija = Pokretanje; kliknite na red — vidi se verzija.

## 6.5 Stanje servera

Levi meni → **Stanje servera**. Osvežava se samo svakih 15 sekundi; **Osveži** za odmah.

| Kartica | Red | Kada je zabrinjavajuće |
|---|---|---|
| Aplikacija | verzija, .NET, okruženje, radi od, vreme servera (UTC) | — |
| Baza | veza, verzija, veličina, konekcije, odziv, poslednji backup | **Veza: ne** (crveno) — odmah IT; backup stariji od 3 dana ili „nepoznato" — IT |
| Host | procesor, memorija, disk | procesor stalno u crvenom (≥ 90 %), memorija ≥ 90 %, slobodan disk < 10 % — hitno IT |

Boje su samo vizuelne — aplikacija ne šalje alarme i ništa ne popravlja sama.

## 6.6 Najčešći problemi

### Audit log nema red koji očekujem
**Zašto:** filteri (datum, entitet) ga isključuju, ili je radnja čitanje (čitanja se ne beleže). **Šta da uradite:** uklonite filtere.

### U koloni **IP** je uvek ista adresa
**Zašto:** mrežna konfiguracija prosleđuje adresu posrednika umesto korisnika. **Šta da uradite:** javite programeru (podešavanje forwarded hedera).

### Stanje servera pokazuje „nedostupno" za host
**Zašto:** aplikacija ne može da pročita podatke o hostu u ovom okruženju. **Šta da uradite:** ako su baza i aplikacija u redu, nije hitno; javite programeru.
