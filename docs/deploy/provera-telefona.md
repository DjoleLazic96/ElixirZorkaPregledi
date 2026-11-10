# Provera na telefonu (Android + Chrome) — pre puštanja u rad

> Ono što se ne testira automatski (spec §10): instalacija aplikacije (PWA), rad bez interneta na pravom telefonu, Web Push, QR kamera.
> Radi se na **produkcionoj adresi sa HTTPS-om** (service worker i push ne rade na `http://` adresi osim `localhost`), službenim telefonom
> inženjera, nalogom sa rolom Inženjer. Upisati datum, model telefona i ishod; neuspeh = zadatak u `TODO.md`.

| # | Korak | Očekivano | Ishod |
|---|---|---|---|
| 1 | Chrome → adresa aplikacije → prijava uz **Zapamti me na ovom uređaju** | Početna sa „Moje aktivnosti“ | |
| 2 | Meni Chrome-a → **Dodaj na početni ekran** / **Instaliraj aplikaciju** | ikonica „TPM pregledi“, otvara se bez trake pregledača | |
| 3 | Zatvoriti i ponovo otvoriti sa ikonice | ostaje prijavljen | |
| 4 | Korisnički meni → **Obaveštenja na telefonu** → **Uključi obaveštenja** → Dozvoli | „Stanje na ovom uređaju: uključeno“ | |
| 5 | **Pošalji probno**, pa zaključati telefon | obaveštenje „Probno obaveštenje“ za nekoliko sekundi | |
| 6 | Dodir na obaveštenje | aplikacija se otvara (Početna) | |
| 7 | Supervizor vraća inženjerov pregled na doradu | obaveštenje „… vraćen na doradu“; dodir otvara popunjavanje | |
| 8 | Uključiti režim rada u avionu, otvoriti današnji pregled sa ikonice | traka „Bez interneta“; pregled se otvara i popunjava | |
| 9 | Bez interneta: odgovori, napomena, slika kamerom, „nije u funkciji“, **Predaj pregled** | „Predato – čeka slanje“; brojač u traci raste | |
| 10 | Isključiti režim rada u avionu | traka nestaje; „Poslednja sinhronizacija“ se osvežava; pregled je na serveru „čeka overu“ sa slikama | |
| 11 | Uređaj → **QR nalepnica** → **Štampaj**; kamerom telefona skenirati odštampani kod | otvara se ekran tog uređaja | |
| 12 | Skenirati QR kad korisnik nije prijavljen | prijava, pa ekran uređaja (ne Početna) | |
| 13 | „?“ uz aktivnost bez interneta | tekst uputstva; slike „kad ima interneta“ | |
| 14 | **Odjavi se**, prijaviti se drugim nalogom na istom telefonu | prethodni korisnik više ne dobija obaveštenja na ovaj telefon | |
| 15 | Posle novog deploy-a: otvoriti aplikaciju sa ikonice | učitava se nova verzija („O aplikaciji“) najkasnije posle drugog otvaranja | |

Napomene:
- iPhone nije službeni telefon (spec §12); push na iPhone-u radi samo kao instalirana aplikacija (iOS 16.4+), bez garancije.
- Ako korak 5 ne uspe: proveriti da je `data/fajlovi/vapid.json` na serveru (ključevi push-a) i da firewall dozvoljava izlaz servera
  ka push servisima (`fcm.googleapis.com`, `*.push.services.mozilla.com`).
