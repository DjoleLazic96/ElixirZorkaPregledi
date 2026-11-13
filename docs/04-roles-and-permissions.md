# 04 — Role i pristupna prava

> Stvarno stanje koda. Standard: Elixir blueprint 07, uz odstupanje „četiri role i prava štikliranjem“ (`docs/odluke.md`, 2026-11-04).
> **Backend je autoritativan; frontend ogleda** (`Zasticeno` na rutama, `NAV visibleIf`, tabovi Administracije).

## 1. Role

| Rola (kod/žica) | Labela u UI | Namena |
|---|---|---|
| `Inzenjer` | Inženjer | radi preglede na telefonu (jedini koji popunjava, R1.8) |
| `Supervizor` | Supervizor | raspored, overa, dorada, odsustva, akcije |
| `Direktor` | Direktor | sve što i Supervizor, plus šabloni i objava |
| `Admin` | Administrator | korisnici, prava, podešavanja, audit log, stanje servera |

Tačno jedna rola po korisniku. Rola određuje **samo podrazumevani skup prava** pri kreiranju naloga i pri promeni role;
šta korisnik sme proverava se po pravu. Konstante: `Domain/Identity/Roles.cs`, `Domain/Identity/Prava.cs`.

## 2. Prava i podrazumevani skup (R1.5)

| Pravo | Labela | Inženjer | Supervizor | Direktor | Admin |
|---|---|---|---|---|---|
| `pregledi.popunjavanje` | Popunjavanje pregleda | ✔ | — | — | — |
| `analitika` | Analitika | ✔ | ✔ | ✔ | ✔ |
| `overa` | Overa, dorada i poništavanje pregleda (ne sopstvenog — zamerka 57) | — | ✔ | ✔ | — |
| `raspored.uredjivanje` | Raspored i PZ | — | ✔ | ✔ | — |
| `odsustva.odobravanje` | Odsustva | — | ✔ | ✔ | — |
| `akcije.zatvaranje` | Zatvaranje i dodela akcija | — | ✔ | ✔ | — |
| `sabloni.nacrt` | Registar opreme i nacrt šablona | — | — | ✔ | ✔ |
| `sabloni.objava` | Objava šablona | — | — | ✔ | — |
| `neradni_dani` | Neradni dani | — | — | ✔ | ✔ |
| `korisnici` | Korisnici i prava | — | — | — | ✔ |
| `podesavanja` | Podešavanja | — | — | — | ✔ |

Admin štiklira prava po korisniku (`PUT /korisnici/{id}/prava`). Čitanje pregleda, neispravnosti, uređaja i analitike je za sve
ulogovane (R1.7).

## 3. Politike (`Program.cs`)

| Politika | Uslov |
|---|---|
| `Pravo:<pravo>` | claim `pravo` sa tom vrednošću (po jedna politika za svako pravo iz `Prava.Sva`) |
| `RequireAdmin` | rola Admin — sistemski ekrani skeleta (audit log, stanje servera), uvoz šablona, nova dodatna polja |
| `RequireKorisnik` | bilo koja od četiri role |
| `Authenticated` | bilo koji ulogovan |

Prava su u JWT-u (claim `pravo`), pa ih politika proverava bez upita u bazu. Promena prava (i role) revokuje refresh tokene
korisnika: nova prava važe najkasnije kad istekne trenutni access token (15 min). Isti prihvaćen rizik kao za role.

## 4. Matrica ruta

| Resurs / akcija | Politika endpointa |
|---|---|
| Prijava, refresh, odjava, `/me`, promena sopstvene lozinke | anonimno / Authenticated |
| `/version`, `/health*` | anonimno |
| Sekcije — čitanje | RequireKorisnik |
| Sekcije — dodavanje, izmena, redosled | RequireAdmin |
| Korisnici i prava — sve | `Pravo:korisnici` |
| Podešavanja — čitanje i izmena | `Pravo:podesavanja` |
| Audit log, stanje servera | RequireAdmin |
| Registar opreme, katalog, šabloni — čitanje | RequireKorisnik |
| Registar opreme i katalog aktivnosti — izmene | `Pravo:sabloni.nacrt` |
| Uvoz šablona, nova dodatna polja | RequireAdmin |
| Kalendar, inženjeri, neradni dani — čitanje | RequireKorisnik |
| Predlog, potvrda, PZ, prenos, izmena dodele, učesnici, odluka o prebacivanju, otkazivanje planiranog, povlačenje, promena tipa DP-a | `Pravo:raspored.uredjivanje` |
| Unos odsustva, odluka o odsustvu, pregledi u odsustvu | `Pravo:odsustva.odobravanje` |
| Zahtev za odsustvo, zahtev za prebacivanje | `Pravo:pregledi.popunjavanje` |
| Unos i brisanje neradnih dana | `Pravo:neradni_dani` |
| Početak, popunjavanje, sinhronizacija, slanje slika | `Pravo:pregledi.popunjavanje` + dodeljeni inženjer ili učesnik PZ-a (inače 403); na PZ-u samo svoje sekcije |
| Prikaz slike | RequireKorisnik |
| Arhiva, detalj, PDF/Word/Excel | RequireKorisnik |
| Overa, vrati na doradu, odluka o doradi, poništavanje, sporan unos | `Pravo:overa`; Supervizor ne dira overu Direktora (422) |
| Zahtev za doradu | `Pravo:pregledi.popunjavanje` + inženjer koji je radio pregled |
| Akcije i neispravnosti — čitanje, Excel | RequireKorisnik |
| Zatvaranje, otkazivanje, zaduženi, odluka o zahtevu za zatvaranje | `Pravo:akcije.zatvaranje` |
| Zahtev za zatvaranje akcije i njegovo otkazivanje; otkazivanje svog zahteva za doradu, prebacivanje i raniji početak | `Pravo:pregledi.popunjavanje` (samo podnosilac) |
| Otvaranje akcije i odgovor na pop-up | kroz `/sinhronizacija` (`Pravo:pregledi.popunjavanje`) |
| Ekran uređaja, QR, Dashboard, pretraga | RequireKorisnik |
| Opšta analitika i ispunjenje plana (`/analitika`) | `Pravo:analitika` (podrazumevano sve četiri role) |
| „Moje aktivnosti“ / „Čeka moju odluku“ na Dashboardu | pravo popunjavanja / rola Supervizor ili Direktor, stavke po pravima |
| Zvono, push pretplata, probno obaveštenje | RequireKorisnik (samo svoja obaveštenja i pretplate) |
| Primaoci obaveštenja | `Pravo:podesavanja` |
| Nacrt šablona, uputstva za aktivnosti, odluka o predlogu izmene | `Pravo:sabloni.nacrt` (Direktor, Admin) |
| Objava verzije šablona | `Pravo:sabloni.objava` (Direktor) |
| Verzije, istorijat, predlog izmene, čitanje uputstva | RequireKorisnik |

Frontend: „Administracija“ se vidi uz pravo `korisnici` ili `podesavanja`; tab „Korisnici“ uz `korisnici`, tab „Podešavanja“ uz `podesavanja`.

## 5. Zaštite pri upravljanju korisnicima (`KorisnikGuards.cs`, sve → 422 `greske[]`)

1. „Ne možete promeniti sopstvenu rolu."
2. „Ne možete deaktivirati sami sebe."
3. „Ne možete skinuti rolu Admin poslednjem aktivnom administratoru."
4. „Ne možete deaktivirati poslednjeg aktivnog administratora."
5. „Pravo „Popunjavanje pregleda“ može da ima samo Inženjer." (R1.8)
6. „Ne možete sebi oduzeti pravo upravljanja korisnicima." (sprečava da se Admin zaključa)

Redosled pri promeni role: prvo dodaj novu, pa skini staru, pa podrazumevana prava nove role. Poznato ograničenje: read-then-mutate
bez row lock-a (TOCTOU) — prihvaćen rizik (`SECURITY.md`).

## 6. Lozinke i sesija

Politika: min 8, cifra, veliko i malo slovo. Lockout 5 pokušaja / 10 min. Neuspešna prijava uvek vraća istu poruku i piše `LOGIN_FAIL` u audit.
Access token 15 min; refresh 7 dana sa rotacijom (frontend obnavlja jednim pozivom u isto vreme — `obnoviSesiju` sa Web Locks između kartica, jer bi drugi poziv sa istim, već rotiranim kolačićem dobio 401 i odjavio korisnika); reset lozinke (admin), promena lozinke, deaktivacija, promena role i promena prava revokuju
sve refresh tokene. Korisničko ime je nepromenljivo; korisnici se ne brišu nego deaktiviraju.

## 7. Inicijalni admin

`Admin__InitialUsername/Email/Password` iz `.env`; seed samo ako nijedan admin ne postoji; odbija praznu ili `Admin123!`. Pri startu nalog bez
ijednog prava dobija podrazumevana prava svoje role (tako i inicijalni admin). Posle prvog deploy-a admin menja lozinku.

## 8. Dodavanje novog prava

`Prava.cs` (konstanta + `Sva` + `Podrazumevana`) → politika se pravi sama iz `Prava.Sva` → `.RequireAuthorization(PravoPolitika.Za(...))` na rutama →
ova matrica → `AuthorizationMatrixTests` → frontend `lib/permissions.ts` (`Pravo`, `PRAVA`, `PODRAZUMEVANA_PRAVA`) → `Zasticeno`/`NAV` →
`docs/user/05-upravljanje-korisnicima.md`. Postojeći nalozi ga ne dobijaju sami — Admin ga štiklira.
