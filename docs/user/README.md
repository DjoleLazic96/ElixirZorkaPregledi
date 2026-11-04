# Korisničko uputstvo — TPM pregledi

> Uputstvo važi za verziju **0.1.0** (vidi „O aplikaciji" u aplikaciji). Namenjeno je ljudima koji svakodnevno koriste aplikaciju,
> ne IT stručnjacima. Svi nazivi dugmadi i poruka su navedeni tačno onako kako ih vidite na ekranu.

## Šta ova aplikacija radi

Aplikacija služi za TPM preglede opreme: dnevne (DP1, DP2) i u planskom zastoju (PZ1–PZ4). Inženjer pregled popunjava
na telefonu, i bez interneta, a podaci se sami pošalju kad se veza vrati. Supervizor i Direktor raspoređuju preglede,
overavaju ih i prate kvarove, potrebne akcije i stanje opreme. Pristup imaju zaposleni sa ličnim nalogom.

*Uputstvo se dopunjuje tokom razvoja; poglavlja ispod opisuju trenutno stanje skeleta.*

## Dve vrste korisnika

- **Korisnik** — radi u aplikaciji i čita šifarnike.
- **Administrator** — sve što i korisnik, plus: uređuje šifarnike, upravlja nalozima, gleda audit log i stanje servera.

## Kako se prijaviti

Otvorite adresu aplikacije u Chrome-u ili Edge-u (potreban je VPN), unesite **Korisničko ime** i **Lozinku**, kliknite **Prijavi se**.
Nalog Vam otvara administrator. Detalji: [1. Prijava i role](01-prijava-i-role.md).

## Sadržaj uputstva

| Dokument | Za koga | Šta pokriva |
|---|---|---|
| [1. Prijava i role](01-prijava-i-role.md) | svi | prijava, odjava, promena lozinke, šta koja rola vidi, „O aplikaciji" |
| [2. Popunjavanje pregleda](02-popunjavanje-pregleda.md) | inženjeri | početak, odgovori, napomene, slike, „nije u funkciji“, predaja |
| [3. Arhiva, overa i dorada](03-overa-i-dorada.md) | svi (odluke: Supervizor, Direktor) | arhiva, detalj, overa, dorada, poništavanje, PDF/Word/Excel |
| [4. Raspored, kalendar i odsustva](04-raspored-i-odsustva.md) | svi (izmene: Supervizor, Direktor) | kalendar, predlog i potvrda rasporeda, PZ, prenos, prebacivanje, odsustva, neradni dani |
| [5. Potrebne akcije i neispravnosti](05-akcije-i-kvarovi.md) | svi (zatvaranje: Supervizor, Direktor) | akcija na pregledu, pop-up, lista akcija, kvarovi |
| [9. Početna, ekran uređaja, analitika i pretraga](09-uredjaj-i-analitika.md) | svi | Početna, „Moje aktivnosti“, „Čeka moju odluku“, istorija uređaja, QR, analitika, pretraga |
| [10. Obaveštenja](10-obavestenja.md) | svi (primaoci: administrator) | zvono, obaveštenja na telefonu, ko šta dobija, primaoci |
| [8. Šabloni i uređaji](08-sabloni-i-uredjaji.md) | svi (izmene: Direktor, Administrator) | šabloni pregleda, registar opreme, katalog, uputstva, uvoz, nacrt i objava verzije, predlozi izmena, istorijat |
| [5. Upravljanje korisnicima](05-upravljanje-korisnicima.md) | administrator | nalozi, role, deaktivacija, reset lozinke |
| [6. Audit log i stanje servera](06-audit-log-i-stanje-servera.md) | administrator | ko je šta promenio; zdravlje servera |
| [7. Šta je novo](07-sta-je-novo.md) | svi | izmene po verzijama |

## Ko šta radi

| Radnja | Korisnik | Administrator |
|---|---|---|
| Prijava, promena sopstvene lozinke | ✔ | ✔ |
| Pregled šablona i uređaja | ✔ | ✔ |
| Dodavanje i izmena uređaja i aktivnosti | Direktor | ✔ |
| Uvoz šablona iz Excela | — | ✔ |
| Upravljanje nalozima | — | ✔ |
| Audit log, Stanje servera | — | ✔ |

## Napomena o pismu

Aplikacija koristi srpsku latinicu sa punim slovima Š, Ć, Č, Ž, Đ. Pretraga u listama ne pravi razliku između „c" i „č" ili „ć".
