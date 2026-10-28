# Odluke — TPM pregledi

> Dnevnik odluka koje nisu očigledne iz koda: izbor između opcija, odstupanja od Elixir blueprinta, dogovori
> sa klijentom i IT-om. Jedna sekcija po odluci, najnovija na vrhu. Odluka koja odstupa od blueprinta se
> **mora** naći ovde pre implementacije (blueprint 19 §7).

---

> Odluke označene **PREDLOG** odstupaju od blueprinta i čekaju odobrenje vlasnika projekta (Miroslav Zec) i vlasnika
> blueprinta (blueprint 19 §7). Funkcionalne odluke su u spec-u `docs/superpowers/specs/2026-10-27-tpm-pregledi-design.md` (Prilog A).

## 2026-10-28 — PDF izveštaji: QuestPDF

**Kontekst:** izveštaj pregleda i liste se izvoze u PDF.
**Odluka:** PDF se pravi bibliotekom `QuestPDF`, kako predviđa blueprint 02; ne traži se komercijalna licenca.
**Posledice:** nema odstupanja od blueprinta; image dobija `fontconfig` + DejaVu (blueprint 02).
**Alternative odbijene:** PDF konverzijom iz Word-a — složenije, a nije potrebno.
**Odlučio:** naručilac, 2026-10-28.

## 2026-10-27 — PREDLOG: dodatne biblioteke

**Kontekst:** blueprint 02: biblioteka van liste = zapisana odluka.
**Odluka:** backend `DocumentFormat.OpenXml` (Word izvoz), `WebPush` (Web Push/VAPID); frontend `vite-plugin-pwa` (service worker), `dexie` (IndexedDB), `qrcode` (QR nalepnice). `ClosedXML` i `recharts` su već u blueprintu.
**Posledice:** zavisnosti u `Directory.Packages.props` i `package.json`; pokrivene `npm audit` / `dotnet list package --vulnerable`.
**Alternative odbijene:** ručni service worker i IndexedDB bez biblioteke — više koda i grešaka za istu stvar.
**Odlučio:** čeka vlasnika projekta + vlasnika blueprinta.

## 2026-10-27 — PREDLOG: „Zapamti me" i duža sesija na telefonu

**Kontekst:** inženjer mora da radi bez interneta i ne sme da bude izlogovan usred pregleda; blueprint 07: refresh token 7 dana.
**Odluka:** uz „Zapamti me na ovom uređaju" refresh token važi 90 dana sa rotacijom (klizno); bez toga cookie traje do zatvaranja pregledača. Token se i dalje drži u memoriji, ne u `localStorage` (12 §38).
**Posledice:** odstupanje od blueprinta 07 i 12 §9 (rok); promena lozinke, deaktivacija i promena role i dalje revokuju sve tokene.
**Alternative odbijene:** 7 dana — inženjer posle odmora ne bi mogao da počne pregled bez signala.
**Odlučio:** čeka vlasnika projekta + vlasnika blueprinta.

## 2026-10-27 — PREDLOG: rad bez interneta (PWA, IndexedDB, sinhronizacija)

**Kontekst:** pregledi se rade u pogonu bez pouzdanog signala; blueprint 14 §7 ne predviđa PWA.
**Odluka:** aplikacija je PWA; telefon drži paket dodeljenih pregleda u IndexedDB i šalje idempotentne operacije (`POST /sinhronizacija`, `opId`). Podaci pregleda na telefonu nisu tokeni.
**Posledice:** odstupanje od blueprinta 14 §7 (PWA); detalji u spec-u §5.1.
**Alternative odbijene:** samo online — pregled bi stajao bez signala; native Android — druga kodna baza.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); čeka vlasnika blueprinta.

## 2026-10-27 — PREDLOG: mobilni prikaz

**Kontekst:** blueprint 14 §7 podržava širinu od 1280 px; inženjeri rade na Android telefonima.
**Odluka:** responzivan prikaz: telefon (donji meni, kartice umesto tabela), tablet, laptop i veći (kao blueprint).
**Posledice:** odstupanje od blueprinta 14 §7; komponente moraju da rade od 375 px.
**Alternative odbijene:** posebna mobilna aplikacija.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); čeka vlasnika blueprinta.

## 2026-10-27 — PREDLOG: otpremanje fotografija

**Kontekst:** blueprint 12 §28 dozvoljava upload samo `.xlsx` do 5 MB.
**Odluka:** fotografije (JPEG, smanjene na telefonu na 1–2 MB, limit 3 MB po fajlu, najviše 5 po stavci) na volumenu `data/fotografije`; uvoz šablona `.xlsx` ostaje kao u blueprintu.
**Posledice:** odstupanje od blueprinta 12 §28; nginx `client_max_body_size 4m`; provera MIME i sadržaja; bekap volumena.
**Alternative odbijene:** slike u bazi — rast baze i bekapa.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); čeka vlasnika blueprinta.

## 2026-10-27 — PREDLOG: push obaveštenja i zakazane provere

**Kontekst:** blueprint nema push; pozadinski poslovi su dozvoljeni samo kao `BackgroundService` (bez Hangfire/Quartz).
**Odluka:** Web Push (VAPID) uz mejl kroz Microsoft Graph; zakazane provere (13:00, propušten DP, rok overe, rok akcije, ponovljeni kvar, dugo van funkcije, predlog rasporeda) kao `BackgroundService`, idempotentno.
**Posledice:** odstupanje od blueprinta (push); VAPID ključevi u `.env`.
**Alternative odbijene:** samo mejl — inženjer na terenu ne čita mejl.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); čeka vlasnika blueprinta.

## 2026-10-27 — PREDLOG: četiri role i prava štikliranjem

**Kontekst:** skelet ima dve role; proces ima Inženjera, Supervizora, Direktora i Admina, a Admin želi da štiklira koje delove aplikacije koji korisnik koristi.
**Odluka:** role `Inzenjer`, `Supervizor`, `Direktor`, `Admin` (jedna po korisniku) + tabela `korisnik_pravo`; podrazumevani skup prava po roli, Admin dodaje i oduzima. Popunjavanje pregleda je samo za Inženjera i ne može se dodeliti drugima.
**Posledice:** odstupanje od blueprinta 07 §3 (prava pored role); politike proveravaju pravo; matrica u `docs/04`.
**Alternative odbijene:** više rola po korisniku — nepregledno; fiksne role bez štikliranja — vlasnik to izričito traži.
**Odlučio:** vlasnik projekta (2026-10-20); čeka vlasnika blueprinta.

## 2026-10-27 — Identitet projekta

**Kontekst:** početak projekta iz Elixir skeleta v1.1.
**Odluka:** naziv „TPM pregledi", slug `tpm-pregledi`, namespace `TpmPregledi`, radni domen `tpm-pregledi.elixirgroup.rs` (IT potvrđuje), dev port `55434` (sledeći slobodan u registru blueprinta 03 §1 — upisati u registar). Vlasnik projekta: Miroslav Zec, direktor održavanja.
**Posledice:** imena compose projekta, image-a, baze, issuer-a, cookie-ja i mrežnog aliasa izvedena iz sluga. Repo je lokalan (bez GitHub remote-a dok vlasnik ne odluči o organizaciji).
**Alternative odbijene:** „Elixir Pregledi" / „Pregledi" — manje jasno kad dođu drugi moduli (pumpe i trake).
**Odlučio:** naručilac, 2026-10-27; potvrđuje vlasnik projekta.

<!-- Šablon:
## YYYY-MM-DD — Naslov
**Kontekst:** zašto se odluka uopšte donosi.
**Odluka:** šta je odlučeno, jednom rečenicom.
**Posledice:** šta to menja u kodu/docs/procesu; ako odstupa od blueprinta — od kog dokumenta i zašto.
**Alternative odbijene:** šta smo još razmatrali i zašto ne.
**Odlučio:** ko (vlasnik projekta / vlasnik blueprinta).

Primer odstupanja:
## 2026-11-03 — Više rola po korisniku
**Kontekst:** klijent traži da šef smene bude i „Analitičar" i „Smenovođa".
**Odluka:** korisnik ima skup rola (`roles[]`) umesto jedne (`rola`); odstupanje od blueprinta 07 §3.
**Posledice:** `KorisnikDto.role: string[]`, forma sa checkbox-ovima („bar jedna"), guard „poslednji admin" gleda skup; `docs/04` ažuriran.
**Alternative odbijene:** treća kombinovana rola `SefSmene` — eksplozija kombinacija kad stigne četvrta.
**Odlučio:** vlasnik projekta + vlasnik blueprinta (2026-11-03).
-->
