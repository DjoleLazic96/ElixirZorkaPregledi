# Faza 08 — Uređaj, analitika, Dashboard, pretraga

**Cilj:** ekran uređaja sa istorijom i dosijeom, analitika po uređaju i opšta, ispunjenje plana, Početna (Dashboard) sa
„Moje aktivnosti“ i „Čeka moju odluku“, pretraga u zaglavlju i QR nalepnica.
**Pravila:** R5.6, R8.4–R8.12, R8.14 (R8.13 — sortiranje, filteri i izvoz na novim tabelama).

## Odluke

- **Stanje uređaja na pregledu:** „nije u funkciji“ ako je tako označen; „neispravan“ ako je bar jedna stavka važeće dorade
  neispravna; inače „ispravan“. Računa se iz predatih pregleda; poništeni se vide u listi, ali ne ulaze u istoriju ni analitiku.
- **Istorija (R8.7):** za svaki dan važi stanje sa poslednjeg pregleda (po planiranom danu). Kvar je niz dana „neispravan“;
  dani „nije u funkciji“ ga ne prekidaju i ne produžavaju. Iz toga: dana bez kvara (trenutno, najduže), dana od poslednjeg kvara,
  trajanje svakog kvara i prosek, broj kvarova. Najčešća stavka je aktivnost sa najviše kvarova (tabela `kvar`) u periodu.
- **Status uređaja (R8.4):** „nije pregledan“ ako nema predatog pregleda ili je poslednji stariji od 7 dana; inače „nije u funkciji“
  (poslednji pregled), „otvoren kvar“ (ima otvoren kvar) ili „ispravan“.
- **Klizač perioda (R8.5):** 30–365 dana; upit ide tek kad se klizač pusti.
- **Pregled sa ekrana uređaja** otvara `/pregledi/{id}?uredjaj={id}` — stavke tog uređaja su izdvojene na vrhu.
- **QR (R5.6):** nalepnica nosi `/qr/{qrToken}`; ekran posle prijave otvara uređaj. Štampa iz dijaloga (biblioteka `qrcode`, odobrena u „biblioteke“).
- **Ispunjenje plana (R8.9):** pregledi po prvobitnom danu, zaključno sa jučerašnjim, bez poništenih. Na dan = predat istog
  (beogradskog) dana ili ranije; kasnije = predat posle; propušteno = nije predat; preneto = planirani dan ≠ prvobitni.
  Preneti se računa inženjeru iz prvog događaja „prenos“; pregled na dan njegovog odsustva ne ulazi u njegov red (ulazi u zbir i mesec).
  Overa u roku = sati radnih dana od predaje do overe ≤ „Rok za overu“ iz podešavanja (vikend se ne broji).
- **Dashboard (R8.10–R8.12):** svi vide sve (danas, sutra, čeka overu, akcije po hitnosti, otvoreni kvarovi, uređaji van funkcije i
  brojevi za tekući mesec). „Moje aktivnosti“ ima ko ima pravo popunjavanja; „Čeka moju odluku“ Supervizor i Direktor, po pravima
  (overa i dorada — `overa`, prebacivanje — `raspored.uredjivanje`, odsustvo — `odsustva.odobravanje`). Predlozi izmena šablona dolaze u fazi 10,
  broj na zvoncu u fazi 09.
- **Pretraga (R8.14):** od 2 znaka; uređaji po nazivu ili oznaci, pregledi, akcije i kvarovi po broju, najviše 8 po vrsti.
- Grafikoni su jednobojni CSS stubići (bez nove biblioteke), vrednost je ispisana pored stubića.
- Početna (`/`) je sada landing za sve role.

## Tabele
Bez novih tabela i migracija.

## API
`GET /uredjaji/{id}/ekran?dana` · `GET /uredjaji/qr/{token}` · `GET /uredjaji/{id}/pregledi.xlsx` · `GET /analitika/opsta?od&do` ·
`GET /analitika/ispunjenje?od&do` (pravo `analitika`) · `GET /dashboard` · `GET /pretraga?q`.

## Frontend
`/` Početna · `/uredjaji/:id` ekran uređaja · `/qr/:token` · `/analitika` · pretraga u zaglavlju (na telefonu ikonica).

## Testovi
Unit: pravila istorije i analitike, rok overe bez vikenda, ekran uređaja, poništen pregled, ispunjenje plana sa odsustvom,
opšta analitika, Dashboard, pretraga. Matrica autorizacije: sve nove rute. Frontend: ekran uređaja, Početna, pretraga i obeležavanje, guard `/analitika`.
