# Faza 10 — Izmene šablona u aplikaciji

**Cilj:** Direktor i Admin menjaju šablone kroz nacrt, vide razlike i (Direktor) objavljuju verziju sa datumom; Inženjer i Supervizor
predlažu izmene; istorijat šablona; uputstva za aktivnosti.
**Pravila:** R2.4, R2.5, R2.6, R2.8, R2.9, R2.10 (spisak „Nije ni u jednom pregledu“ postoji od faze 02).

## Odluke

- **Nacrt** je `sablon_verzija` sa statusom `nacrt` (jedan po šablonu), kopija poslednje objavljene ili zakazane verzije sa istim
  ključevima stavki (R2.5). Izmene: dodavanje (uređaj iz registra — samo aktivan — i aktivnost iz kataloga, uz polja; stavka ide posle
  poslednje stavke istog uređaja u sekciji), izbacivanje, izmena (druga aktivnost iz kataloga = „ispravka teksta“, i polja; ključ ostaje),
  pomeranje gore/dole unutar sekcije. Ista aktivnost dva puta na istom uređaju — 422. Odbacivanje briše nacrt.
- **Razlike** po stalnom ključu: dodato, izbačeno, izmenjeno (aktivnost ili polja, sa „bilo“), pomereno (relativni redosled zajedničkih stavki).
- **Objava** (samo `sabloni.objava`, Direktor) sa datumom od kog važi (danas ili kasnije); bez izmena — 422. Datum danas → verzija odmah postaje
  važeća (prethodna „zamenjena“); kasniji datum → status `zakazana`, a zakazana provera (jednom u minutu) je aktivira svog dana. Dok
  postoji zakazana, nova objava istog šablona čeka (422); novi nacrt kreće od zakazane. Započeti pregledi ostaju na svojoj verziji
  (`pregled.sablon_verzija_id` se postavlja pri početku). Pri početku pregleda zakazana verzija čiji je dan došao važi i pre aktivacije.
- **Predlozi (R2.8):** podnose svi (vrsta dodavanje / izbacivanje / ispravka teksta, stavka iz važeće verzije, opis, obavezan razlog);
  odlučuju Direktor i Admin (pravo `sabloni.nacrt`, R2.8 kaže „Direktor i Admin“). Novi predlog ide im u zvono i u „Čeka moju odluku“;
  ishod ide podnosiocu. Odobren predlog je zadatak u nacrtu; pri objavi se vezuje za verziju („u verziji 3“).
- **Istorijat (R2.9):** `sablon_dogadjaj` — prvi uvoz, nacrt, izmena u nacrtu, odbačen nacrt, predlog, odobren/odbijen predlog, objava,
  verzija postala važeća; filteri šablon, vrsta, korisnik, period (beogradski dan).
- **Uputstva (R2.4):** tekst (do 4000) i do 3 slike (`aktivnost.uputstvo_slike`, ID-jevi iz `fajl`) na aktivnosti — važe u svim šablonima,
  izmena ne pravi verziju. „?“ uz aktivnost u Šablonima, katalogu i popunjavanju; unos iz prozora uputstva (pravo `sabloni.nacrt`).
  Tekst ide u paket za popunjavanje, pa „?“ radi i bez interneta; slike se učitavaju kad ima interneta. Katalog označava aktivnosti „bez uputstva“.
- **Paket za rad bez interneta** za planirane preglede nosi važeću verziju; pregled započet bez interneta na dan kad zakazana verzija počne
  da važi radi po staroj verziji (rub, prihvatljivo — R2.6 to i traži za već započete).

## Tabele (migracija `IzmeneSablona`)
`predlog_izmene`; kolona `aktivnost.uputstvo_slike`; status verzije `zakazana`.

## API
`GET /sabloni/{tip}/verzije` · `GET|POST|DELETE /sabloni/{tip}/nacrt` · `POST /sabloni/{tip}/nacrt/stavke` · `PUT|DELETE /sabloni/{tip}/nacrt/stavke/{id}` ·
`POST /sabloni/{tip}/nacrt/stavke/{id}/pomeri` · `POST /sabloni/{tip}/nacrt/objavi` · `GET /sabloni/istorijat` · `GET|POST /predlozi-izmena` ·
`POST /predlozi-izmena/{id}/odluka` · `GET|PUT /aktivnosti/{id}/uputstvo` · `POST /aktivnosti/{id}/uputstvo/slike` · `GET|DELETE /aktivnosti/{id}/uputstvo/slike/{fajlId}`.

## Testovi
Unit: nacrt kao kopija i objava bez izmena, izmene i razlike (dodato, izbačeno, izmenjeno, pomereno; van upotrebe; duplikat), objava od danas
i započet pregled, zakazana verzija i aktivacija, predlog (obaveštenja, odluka jednom, vezivanje za verziju, istorijat), validacija predloga,
uputstvo (tekst, 3 slike). Matrica autorizacije: nove rute. Frontend: nacrt (otvaranje, razlike, izbacivanje, pomeranje, „Objavi“ samo uz pravo),
predlog, odluka, uputstvo bez interneta i bez teksta.
