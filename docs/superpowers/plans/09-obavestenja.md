# Faza 09 — Obaveštenja i pozadinski poslovi

**Cilj:** zvono u aplikaciji, Web Push na telefon i mejl; lična obaveštenja (dodela, dorada, ishod zahteva) i zakazane provere
iz spec §5.2; Admin bira primaoce.
**Pravila:** R9.1–R9.5, R6.3, R7.8, R8.2–R8.3, R8.12 (broj na zvoncu), R12.1 (primaoci). Odobreno: „push obaveštenja i zakazane provere“ (2026-11-04).

## Odluke

- **Outbox:** obaveštenje se upisuje u `obavestenje` u istoj transakciji sa događajem (`Obavesti.DodajAsync`, kao `PregledIstorija.Dodaj`,
  bez izmene konstruktora handlera). Posao isporuke (svakih 15 s) šalje push na sve uređaje korisnika i mejl, pa upisuje `isporuceno_at`;
  posle 5 neuspeha odustaje (zvono ostaje). Istekla pretplata (410/404) se briše. Bez interneta push stiže kad se telefon poveže (R9.5).
- **Idempotentnost:** zakazane provere nose ključ (`rok_overe:{pregled}:{dorada}`, `nije_zapocet:{pregled}:{dan}`…), jedinstven po korisniku,
  pa prolaz svakog minuta i restart ne dupliraju obaveštenja.
- **Zakazane provere** (jednom u minutu, beogradsko vreme): nije započet (DP i PZ, svako u svoje vreme, primaocima i dodeljenom inženjeru);
  rok za overu (sati radnih dana > „Rok za overu“, jednom po doradi); istekao rok akcije (jednom); dugo van funkcije (≥ granica, ponavlja
  se na svakih toliko dana); dnevni rezime posle kraja radnog dana (samo dani sa pregledima); četvrtkom posle kraja dana, ako DP za
  sledeću nedelju nije potvrđen — podsetnik da je predlog spreman. Hitna akcija i ponovljeni kvar idu odmah iz popunjavanja.
- **Ponovljeni kvar (R8.2):** pri predaji, za svaku neispravnu stavku: broj predatih pregleda (bez poništenih) u poslednjih M dana u kojima je
  ista stavka neispravna, uključujući ovaj; ≥ N → obaveštenje.
- **Lična (R9.3):** potvrda rasporeda (zbirno po inženjeru), zakazan PZ, promena dodele ili datuma (novi i stari inženjer), dodat drugi
  inženjer na PZ, prenos (zbirno po inženjeru), prebacivanje zbog odsustva, vraćanje na doradu i odobrena dorada, ishod zahteva za
  odsustvo, prebacivanje i doradu. Predlozi izmena šablona dolaze u fazi 10.
- **Primaoci (R9.4):** čuvaju se u `podesavanje.primaociObavestenja` (`{ vrsta: [korisnikId] }`, audit kao i ostala podešavanja) umesto
  zasebne tabele iz spec §6.5 — jedan red, ista forma i audit. Dok Admin ne sačuva vrstu, primaoci su korisnici sa pravom `overa`
  (podrazumevano tačno Supervizor i Direktor).
- **Broj na zvoncu (R8.12):** nepročitana + „čeka moju odluku“ (iste stavke kao kartica na Početnoj).
- **VAPID:** iz konfiguracije (`Push:JavniKljuc`, `Push:PrivatniKljuc`), a ako ih nema — prvi start ih napravi u `Push:Putanja`
  (`/app/data/vapid.json` na volumenu `data/fajlovi`). Biblioteka `WebPush` (odobrena u „biblioteke“).
- **Mejl:** Microsoft Graph `sendMail` sa aplikacijskom dozvolom (bez SDK-a, `HttpClient`); bez `Graph:*` samo upis u log. Telo je tekst
  obaveštenja i link na aplikaciju (`App:JavnaAdresa`).
- **Telefon:** „Obaveštenja na telefonu“ u korisničkom meniju (uključi, isključi, probno). Service worker uvozi `push-sw.js` (prikaz i klik
  otvara rutu). Odjava korisnika odjavljuje i push na tom uređaju.
- **Produkcija:** fotografije (faza 04) i VAPID ključevi nisu bili na volumenu — dodat `./data/fajlovi:/app/data`, direktorijum u
  `deploy.sh` i arhiva `fajlovi-*.tgz` u `backup.sh`.

## Tabele (migracija `Obavestenja`)
`obavestenje`, `push_pretplata`.

## API
`GET /obavestenja` · `POST /obavestenja/procitano` `{ ids? }` · `POST /obavestenja/probno` · `GET /push/kljuc` · `POST|DELETE /push/pretplata` ·
`GET|PUT /podesavanja/primaoci` (pravo `podesavanja`).

## Testovi
Unit: primaoci (podrazumevano i podešeno), nije započet (vreme i jednom), rok overe bez vikenda, rok akcije jednom, rezime i predlog rasporeda,
promena dodele i ishod odsustva, zvono i „čeka moju odluku“, isporuka (push, mejl, istekla pretplata, ponovni pokušaj), hitna akcija i
ponovljeni kvar iz popunjavanja. Matrica autorizacije: sve nove rute. Frontend: zvono, primaoci, meni. Ručno (faza 11): stvarni push na Android Chrome-u.
