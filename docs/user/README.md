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
| [2. Kategorije](02-kategorije.md) | svi (izmene: administrator) | *primer šifarnika iz skeleta — zameniti domenskim uputstvom* |
| [5. Upravljanje korisnicima](05-upravljanje-korisnicima.md) | administrator | nalozi, role, deaktivacija, reset lozinke |
| [6. Audit log i stanje servera](06-audit-log-i-stanje-servera.md) | administrator | ko je šta promenio; zdravlje servera |
| [7. Šta je novo](07-sta-je-novo.md) | svi | izmene po verzijama |

## Ko šta radi

| Radnja | Korisnik | Administrator |
|---|---|---|
| Prijava, promena sopstvene lozinke | ✔ | ✔ |
| Pregled kategorija | ✔ | ✔ |
| Dodavanje i izmena kategorija | — | ✔ |
| Upravljanje nalozima | — | ✔ |
| Audit log, Stanje servera | — | ✔ |

## Napomena o pismu

Aplikacija koristi srpsku latinicu sa punim slovima Š, Ć, Č, Ž, Đ. Pretraga u listama ne pravi razliku između „c" i „č" ili „ć".
