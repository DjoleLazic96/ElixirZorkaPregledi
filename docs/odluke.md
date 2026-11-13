# Odluke — TPM pregledi

> Dnevnik odluka koje nisu očigledne iz koda: izbor između opcija, odstupanja od Elixir blueprinta, dogovori
> sa klijentom i IT-om. Jedna sekcija po odluci, najnovija na vrhu. Odluka koja odstupa od blueprinta se
> **mora** naći ovde pre implementacije (blueprint 19 §7).

---

> Odluke označene **PREDLOG** odstupaju od blueprinta i čekaju odobrenje vlasnika projekta (Miroslav Zec) i vlasnika
> blueprinta (blueprint 19 §7). Funkcionalne odluke su u spec-u `docs/superpowers/specs/2026-10-27-tpm-pregledi-design.md` (Prilog A).

## 2026-11-13 — Zamerke 23–37: lozinka, verzija, raspored kao niz, sekcije, predlozi (odobreno)

**Kontekst:** testiranje 13.11. (zamerke 23–37 u „Zamerke za doradu.md“).
**Odluka:**
- Zaboravljena lozinka ide preko administratora (bez mejla), uz obaveznu promenu pri prvoj prijavi posle reseta (R1.13).
- „O aplikaciji“: tehnički podaci samo za Admina (R1.13). Blueprint 11 §4 traži dijalog sa verzijom i upozorenje na razliku verzija; oba ostaju za sve.
- PWA prelazi sa automatskog na ručno osvežavanje uz „Dostupna je nova verzija“ (R10.12).
- Raspored DP-a je jedan niz (R3.28–R3.32): otkazivanje, povlačenje, kvačice, „Promeni DP“ i podsetnik četvrtkom i petkom.
- Sekcije uređuje Admin (R2.16); predlozi iz pregleda, saglasnost i duplikati (R2.17); rezime pre predaje (R4.8).
**Posledice:**
- Migracija `Zamerke23do37`: `pregled.propusten`, `sekcija.aktivna`, `predlog_izmene.pregled_id` i `original_id`, tabela `predlog_saglasnost`, `AspNetUsers.mora_promeniti_lozinku`.
- Nove rute: `/pregledi/{id}/otkazi`, `/pregledi/{id}/tip`, `/raspored/povuci`, `/sekcije` (POST/PUT), `/predlozi-izmena/{id}/slazem-se`.
- Nova operacija sinhronizacije: `predlog_izmene`.
**Odlučio:** vlasnik projekta (2026-11-13).

## 2026-11-13 — Playwright za testove kroz pregledač; odbijene slike na telefonu (odobreno)

**Kontekst:** predlozi 19 i 20 posle pregleda koda: slika koju server trajno odbije ostajala je na telefonu bez ikakvog
izlaza (nije se videla, nije mogla da se pošalje ponovo ni obriše), a ceo tok kroz pregledač (prijava, rad bez interneta,
predaja) proveravao se samo ručno.
**Odluka:** (1) odbijene slike se prikazuju na svakom ekranu inženjera sa razlogom, uz „Pošalji ponovo“ (dok se pregled još
popunjava; prevelika slika se pre toga još jednom smanji), „Sačuvaj na telefon“ i „Odbaci“ (uz potvrdu). (2) Nova razvojna
biblioteka `@playwright/test` 1.64.1 (samo za testove, ne ulazi u aplikaciju); CI posao „E2E (Playwright)“ pokreće API nad
praznom bazom i produkcijski build fronta, sa nasumičnim lozinkama za svako pokretanje.
**Posledice:** `frontend/e2e/`, `playwright.config.ts`, `npm run e2e`; komponenta `OdbijeneSlike`; odbijena slika čuva stavku
i izvor. R10.11.
**Odlučio:** vlasnik projekta (2026-11-13).

## 2026-11-13 — Kvar određuje najnoviji predat pregled; zakazivanje u prošlost zabranjeno (odobreno)

**Kontekst:** testiranje 13.11. (zamerke 11 i 18): dorada ili kasna predaja starijeg pregleda je ponovo otvarala kvar koji je noviji pregled zatvorio, a pregled se mogao zakazati za prošli dan.
**Odluka:** stanje kvara za stavku (uređaj + aktivnost) menja samo najnoviji predat pregled (po planiranom danu, pa po vremenu predaje); stariji samo ostaje u istoriji pregleda. Zakazivanje i pomeranje na prošli dan je zabranjeno (R3.27).
**Posledice:** `ObradiKvaroveAsync` preskače stavke za koje postoji noviji predat pregled; 422 „Ne može se zakazati za dan koji je prošao.“ na potvrdi predloga, PZ-u i promeni datuma.
**Odlučio:** vlasnik projekta (2026-11-13).

## 2026-11-10 — Izmena i otkazivanje odsustva; spisak svih odsustava (odobreno)

**Kontekst:** uneto ili odobreno odsustvo nije moglo da se promeni ni otkaže, a Direktor nije imao mesto gde vidi sva odsustva.
**Odluka:** Supervizor i Direktor (pravo `odsustva.odobravanje`) na ekranu „Raspored“ imaju karticu „Odsustva“ (tekuća i buduća; filter po inženjeru;
„Prikaži i ranija“) sa „Izmeni“ (od–do) i „Otkaži“. Inženjer u „Moja odsustva“ povlači svoj zahtev dok o njemu nije odlučeno. Otkazano odsustvo
dobija status `otkazano` (ne briše se) i ne računa se u rasporedu; prebačeni pregledi ostaju zamenama dok ih Supervizor ili Direktor ne vrati.
**Posledice:** pravilo R3.25 u Prilogu A; `PUT /odsustva/{id}`, `POST /odsustva/{id}/otkazi`; inženjer dobija obaveštenje o izmeni i otkazivanju.
**Alternative odbijene:** brisanje odsustva — gubi se trag; automatsko vraćanje prebačenih pregleda — menja raspored bez znanja supervizora.
**Odlučio:** vlasnik projekta (2026-11-10).

## 2026-11-10 — Pregled se započinje najranije na planirani dan (odobreno)

**Kontekst:** aplikacija je dozvoljavala da inženjer započne i preda pregled pre planiranog datuma (npr. DP za četvrtak u ponedeljak); Prilog A to nije uređivao.
**Odluka:** inženjer započinje pregled najranije na planirani dan (provera na serveru, po serverskom danu). Pre toga vidi „Može da se započne od …“.
DP se radi samo na svoj dan; ako inženjer tada nije tu, traži prebacivanje (R3.11). Za PZ inženjer može da pošalje **zahtev za raniji početak**
(razlog obavezan); odlučuju Supervizor i Direktor (pravo `raspored.uredjivanje`), a odobren zahtev pomera PZ na današnji dan
(planirani i prvobitni dan, jer pregled nije propušten) i inženjer odmah može da ga započne. DP planiran za taj dan ostaje kako jeste.
**Posledice:** novo pravilo R3.24 u Prilogu A; `zahtev_prebacivanja.vrsta` (`prebacivanje` / `raniji_pocetak`), migracija `ZahtevVrsta`;
zahtev se vidi u „Čeka moju odluku“ i na ekranu Raspored („Zahtevi inženjera“).
**Alternative odbijene:** raniji rad DP-a uz zamenu dana ili dva DP-a istog dana — DP je provera stanja tog dana, a zamena bi pomešala redosled DP1/DP2 i analitiku.
**Odlučio:** vlasnik projekta (2026-11-10).

## 2026-11-05 — Izgled po mockup-u (odobreno)

**Kontekst:** blueprint 14 propisuje UI kit (bela bočna traka, neutralne sive); korisnici su videli i odobrili mockup `docs/mockup/index.html`.
**Odluka:** izgled prati mockup: zelena bočna traka sa ikonicama, ElixirZorka logo i zelena linija ispod zaglavlja, tople neutralne boje
(`--ink #3d3935`, `--line #dcdad7`, `--bg #f4f5f3`), kartice 10 px, dugmad 8 px iste visine (40 px, na telefonu 44 px), oznake statusa
kao pilule u boji po značenju (Planiran sivo, U radu žuto, Čeka overu zeleno, Overeno plavo, dorada crveno; hitnost crveno/narandžasto/plavo/sivo),
stavke lista sa „›“, sadržaj centriran. Tokeni u `frontend/src/styles/global.css`, komponente u `components/ui/`.
**Posledice:** odstupanje od blueprint 14 (boje i bočna traka); komponente ostaju iste po API-ju, menja se samo izgled.
**Alternative odbijene:** blueprint izgled — korisnici očekuju ono što su videli na mockup-u.
**Odlučio:** vlasnik projekta, 2026-11-05.

## 2026-10-28 — PDF izveštaji: QuestPDF

**Kontekst:** izveštaj pregleda i liste se izvoze u PDF.
**Odluka:** PDF se pravi bibliotekom `QuestPDF`, kako predviđa blueprint 02; ne traži se komercijalna licenca.
**Posledice:** nema odstupanja od blueprinta; image dobija `fontconfig` + DejaVu (blueprint 02).
**Alternative odbijene:** PDF konverzijom iz Word-a — složenije, a nije potrebno.
**Odlučio:** naručilac, 2026-10-28.

## 2026-11-04 — dodatne biblioteke (odobreno; predlog od 2026-10-27)

**Kontekst:** blueprint 02: biblioteka van liste = zapisana odluka.
**Odluka:** backend `DocumentFormat.OpenXml` (Word izvoz), `WebPush` (Web Push/VAPID); frontend `vite-plugin-pwa` (service worker), `dexie` (IndexedDB), `qrcode` (QR nalepnice). `ClosedXML` i `recharts` su već u blueprintu.
**Posledice:** zavisnosti u `Directory.Packages.props` i `package.json`; pokrivene `npm audit` / `dotnet list package --vulnerable`.
**Alternative odbijene:** ručni service worker i IndexedDB bez biblioteke — više koda i grešaka za istu stvar.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); odobreno za implementaciju 2026-11-04 (faza 05).

## 2026-11-04 — „Zapamti me" i duža sesija na telefonu (odobreno; predlog od 2026-10-27)

**Kontekst:** inženjer mora da radi bez interneta i ne sme da bude izlogovan usred pregleda; blueprint 07: refresh token 7 dana.
**Odluka:** uz „Zapamti me na ovom uređaju" refresh token važi 90 dana sa rotacijom (klizno); bez toga cookie traje do zatvaranja pregledača. Token se i dalje drži u memoriji, ne u `localStorage` (12 §38).
**Posledice:** odstupanje od blueprinta 07 i 12 §9 (rok); promena lozinke, deaktivacija i promena role i dalje revokuju sve tokene.
**Alternative odbijene:** 7 dana — inženjer posle odmora ne bi mogao da počne pregled bez signala.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); odobreno za implementaciju 2026-11-04 (faza 05).

## 2026-11-04 — rad bez interneta (PWA, IndexedDB, sinhronizacija) (odobreno; predlog od 2026-10-27)

**Kontekst:** pregledi se rade u pogonu bez pouzdanog signala; blueprint 14 §7 ne predviđa PWA.
**Odluka:** aplikacija je PWA; telefon drži paket dodeljenih pregleda u IndexedDB i šalje idempotentne operacije (`POST /sinhronizacija`, `opId`). Podaci pregleda na telefonu nisu tokeni.
**Posledice:** odstupanje od blueprinta 14 §7 (PWA); detalji u spec-u §5.1.
**Alternative odbijene:** samo online — pregled bi stajao bez signala; native Android — druga kodna baza.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); odobreno za implementaciju 2026-11-04 (faza 05).

## 2026-11-04 — Mobilni prikaz (odobreno; predlog od 2026-10-27)

**Kontekst:** blueprint 14 §7 podržava širinu od 1280 px; inženjeri rade na Android telefonima.
**Odluka:** responzivan prikaz: telefon (donji meni, kartice umesto tabela), tablet, laptop i veći (kao blueprint).
**Posledice:** odstupanje od blueprinta 14 §7; komponente moraju da rade od 375 px.
**Alternative odbijene:** posebna mobilna aplikacija.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); odobreno za implementaciju 2026-11-04 (faza 04).

## 2026-11-04 — Otpremanje fotografija (odobreno; predlog od 2026-10-27)

**Kontekst:** blueprint 12 §28 dozvoljava upload samo `.xlsx` do 5 MB.
**Odluka:** fotografije (JPEG, smanjene na telefonu na 1–2 MB, limit 3 MB po fajlu, najviše 5 po stavci) na volumenu `data/fotografije`; uvoz šablona `.xlsx` ostaje kao u blueprintu.
**Posledice:** odstupanje od blueprinta 12 §28; nginx `client_max_body_size 6m` (dopuna 2026-11-09: 4m nije dovoljno za uvoz `.xlsx` do 5 MB sa multipart zaglavljima); provera MIME i sadržaja; bekap volumena.
**Alternative odbijene:** slike u bazi — rast baze i bekapa.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); odobreno za implementaciju 2026-11-04 (faza 04).

## 2026-11-04 — push obaveštenja i zakazane provere (odobreno; predlog od 2026-10-27)

**Kontekst:** blueprint nema push; pozadinski poslovi su dozvoljeni samo kao `BackgroundService` (bez Hangfire/Quartz).
**Odluka:** Web Push (VAPID) uz mejl kroz Microsoft Graph; zakazane provere (13:00, propušten DP, rok overe, rok akcije, ponovljeni kvar, dugo van funkcije, predlog rasporeda) kao `BackgroundService`, idempotentno.
**Posledice:** odstupanje od blueprinta (push); VAPID ključevi u `.env`.
**Alternative odbijene:** samo mejl — inženjer na terenu ne čita mejl.
**Odlučio:** vlasnik projekta (zahtev, 2026-10-20); odobreno za implementaciju 2026-11-04 (faza 09).

## 2026-11-04 — Četiri role i prava štikliranjem (odobreno; predlog od 2026-10-27)

**Kontekst:** skelet ima dve role; proces ima Inženjera, Supervizora, Direktora i Admina, a Admin želi da štiklira koje delove aplikacije koji korisnik koristi.
**Odluka:** role `Inzenjer`, `Supervizor`, `Direktor`, `Admin` (jedna po korisniku) + tabela `korisnik_pravo`; podrazumevani skup prava po roli, Admin dodaje i oduzima. Popunjavanje pregleda je samo za Inženjera i ne može se dodeliti drugima.
**Posledice:** odstupanje od blueprinta 07 §3 (prava pored role); politike proveravaju pravo; matrica u `docs/04`.
**Alternative odbijene:** više rola po korisniku — nepregledno; fiksne role bez štikliranja — vlasnik to izričito traži.
**Odlučio:** vlasnik projekta (2026-10-20); odobreno za implementaciju 2026-11-04 (faza 01).

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
