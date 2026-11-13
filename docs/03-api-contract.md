# 03 — API ugovor (v1)

> Čitano iz `backend/src/TpmPregledi.Api/Endpoints/*.cs` — stvarno stanje. Konvencije: Elixir blueprint 06.

## Konvencije

- Osnova `/api/v1`; JSON camelCase; enumi camelCase stringovi; `Authorization: Bearer <jwt>`; refresh preko cookie-ja `tpm-pregledi.refresh` (Path `/api/v1/auth`).
- Greške RFC 7807 `{ type, title, status, detail, traceId, errors?, greske? }`: **400** = validacija oblika (`errors{ Polje: [poruke] }`),
  **422** = poslovno pravilo (`greske: [rečenice]`), 401 ista poruka za sve razloge, 403, 404, 409, 429, 500 (`detail` sa kodom za podršku), 503 kad baza nije dostupna (`detail` „Server trenutno nije dostupan…“ u svakom okruženju).
- Paginacija: `?stranica=1&velicina=50&sort=<polje>&smer=asc|desc&pretraga=` → `{ stavke, ukupno, stranica, velicina }`; `velicina` max 200.
- Datumi: trenuci ISO UTC `Z`; dani `YYYY-MM-DD`.
- Politike: `RequireAdmin` (Admin), `RequireKorisnik` (Admin ili Korisnik), `Authenticated` (bilo ko ulogovan), anonimno.

## Kako radi 400 vs 422

`POST /uredjaji` bez naziva → 400 `{"errors":{"input.Naziv":["Naziv uređaja je obavezan."]}}`.
`POST /uredjaji` sa parom (naziv, oznaka) koji već postoji → 422 `{"greske":["Uređaj „Redler 20-CR-05“ već postoji u registru."]}`.
Isto za korisnike: zauzeto korisničko ime → 422. 409 je rezervisan za konkurentnu izmenu (nema ga u skeletu).

## Rute

### `/health`, `/version` (anonimno, bez rate limita)
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/health` | anonimno | `{ "status": "ok" }` |
| GET | `/health/live` | anonimno | `{ "status": "live" }` |
| GET | `/health/ready` | anonimno | `{ "status": "ready" }` ili 503 `Baza nedostupna` (`detail` = tip izuzetka) |
| GET | `/version` | anonimno | `{ verzija, gitSha, buildUtc, okruzenje }` |

### `/auth`
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| POST | `/auth/login` | anonimno, `auth-login` 10/min | `{ userName, password, zapamtiMe? }` (zapamtiMe = refresh 180 dana) → `LoginResponse` + Set-Cookie |
| POST | `/auth/refresh` | cookie, `auth-refresh` 30/min | rotacija → `LoginResponse` |
| POST | `/auth/logout` | Authenticated | revokacija + brisanje cookie-ja → 204 |
| GET | `/auth/me` | Authenticated | `{ user, roles, prava }` (prava iz baze) |
| POST | `/auth/change-password` | Authenticated | `{ currentPassword, newPassword }` → 204; 400 `errors.currentPassword` / `errors.newPassword` |

### `/korisnici` (pravo `korisnici`)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/korisnici?aktivan=` | niz `KorisnikDto` |
| POST | `/korisnici` | `KorisnikInput` + `lozinka` → 201 |
| PUT | `/korisnici/{id}` | `{ ime, email?, rola }` → 200; promena role vraća podrazumevana prava nove role |
| PUT | `/korisnici/{id}/prava` | `{ prava[] }` → 200 `KorisnikDto`; 400 nepoznato pravo; 422 „samo Inženjer popunjava“, „ne možete sebi oduzeti upravljanje korisnicima“; revokuje refresh tokene |
| POST | `/korisnici/{id}/deaktiviraj` `{ prebacivanja? }` · `/aktiviraj` | 200 `KorisnikDto`; 422 guardovi; `prebacivanja[{pregledId, zamenaId}]` predaje planirane preglede (zamerka 54) |
| GET | `/korisnici/{id}/planirani-pregledi` | planirani pregledi sa predlogom zamene (pre deaktivacije) |
| POST | `/korisnici/{id}/reset-lozinka` | `{ novaLozinka }` → 204; 400 politika lozinke |

Guardovi (422): „Ne možete promeniti sopstvenu rolu.", „Ne možete deaktivirati sopstveni nalog.", „Ne možete ukloniti rolu Administrator poslednjem aktivnom administratoru.", „Ne možete deaktivirati poslednjeg aktivnog administratora."

`KorisnikDto` ima i `prava[]`. `LoginResponse` (login, refresh) ima `prava[]`; JWT nosi claim `pravo` (više vrednosti).

### `/sekcije` (čitanje RequireKorisnik, izmene RequireAdmin)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/sekcije` | `[{ id, sifra, naziv, aktivna }]` po redosledu (i neaktivne) |
| POST | `/sekcije` `{ sifra, naziv? }` | nova sekcija na kraju redosleda; 422 šifra postoji |
| PUT | `/sekcije/{id}` `{ naziv, aktivna }` | naziv i aktivnost (šifra se ne menja) |
| PUT | `/sekcije/redosled` `{ ids[] }` | redosled; 422 ako nije svaka sekcija tačno jednom |

### `/podesavanja` (pravo `podesavanja`)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/podesavanja` | `PodesavanjaDto` (vremena „HH:mm:ss“) |
| PUT | `/podesavanja` | ceo `PodesavanjaDto` → 200; 400 van opsega (npr. granica pauze 1–240 min) |

### `/audit` (RequireAdmin)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/audit?stranica&velicina&korisnik&entitet&akcija&od&do` | `StranicaDto<AuditRedDto>`; sort `created_at desc, id desc` |
| GET | `/audit/entiteti` | `[{ entitet, labela }]` |

### `/stanje-servera` (RequireAdmin)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/stanje-servera` | `{ aplikacija, baza, host }` — vidi blueprint 10 §1 |

### Registar opreme, katalog, šabloni
Čitanje: RequireKorisnik. Izmene registra i kataloga: `Pravo:sabloni.nacrt`. Uvoz i nova dodatna polja: RequireAdmin.

| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/uredjaji?sekcijaId&status&pretraga&bezSablona` | RequireKorisnik | `UredjajDto[]` (sa `sabloni[]` — tipovi važećih verzija koje ga sadrže) |
| GET | `/uredjaji/{id}` | RequireKorisnik | `UredjajDto` / 404 |
| POST · PUT | `/uredjaji` · `/uredjaji/{id}` | sabloni.nacrt | `{ naziv, oznaka, sekcijaId, vrsta }`; 422 duplikat (naziv, oznaka) |
| POST | `/uredjaji/{id}/van-upotrebe` · `/u-upotrebi` | sabloni.nacrt | status umesto brisanja |
| GET | `/aktivnosti` | RequireKorisnik | `AktivnostDto[]` (sa `brojStavki`) |
| POST · PUT | `/aktivnosti` · `/aktivnosti/{id}` | sabloni.nacrt | `{ naziv, odgovorIspravan, odgovorNeispravan }`; 422 promena para odgovora aktivnosti iz važećeg šablona |
| GET | `/dodatna-polja` | RequireKorisnik | `DodatnoPoljeDto[]` |
| POST | `/dodatna-polja` | RequireAdmin | `{ naziv, tip, jedinica, oznake, prikaz, obavezno }` |
| GET | `/sabloni` | RequireKorisnik | šest šablona: `{ tip, vazecaVerzija, vaziOd, brojStavki, brojUredjaja }` |
| GET | `/sabloni/{tip}` · `/sabloni/{tip}/verzije/{broj}` | RequireKorisnik | `SablonVerzijaDto`: sekcije → uređaji → stavke (aktivnost, odgovori, polja, `kljuc`) |
| POST | `/sabloni/uvoz` | RequireAdmin | multipart `fajl` (.xlsx ≤ 5 MB, list „Stavke“) → `{ sabloni[], novihUredjaja, novihAktivnosti }`; 422 `greske[]` „Red N (…): …“ (sve ili ništa), 422 za već uvezen šablon |

### Raspored, kalendar, odsustva (faza 03)
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/raspored?od&do` | RequireKorisnik | `{ pregledi[], neradniDani[], odsustva[] }` — pregledi čiji planirani ili prvobitni dan pada u period |
| GET | `/raspored/inzenjeri` | RequireKorisnik | inženjeri u rotaciji `{ id, ime }` |
| POST | `/raspored/predlog` `{ od }` | raspored.uredjivanje | predlog DP-a do petka te nedelje (ne upisuje) |
| POST | `/raspored/potvrdi` `{ stavke[{datum, tip, inzenjerId}], uskladi }` | raspored.uredjivanje | pravi preglede i brojeve (tip računa server od DP-a dan pre); `uskladi` = rupa usred niza se popunjava povlačenjem niza; 422 dan nije za DP / DP već postoji / inženjer nije aktivan |
| POST | `/raspored/povuci` `{ datum }` | raspored.uredjivanje | „Povuci raspored“ u oslobođen dan → `{ pomereno }`; 422 ako dan nije rupa usred niza |
| GET · POST | `/raspored/pz/sledeci` · `/raspored/pz` `{ datum, tip?, inzenjerId, napomena? }` | raspored.uredjivanje | sledeći PZ po redu; zakazivanje („Promeni PZ“ = drugi tip); 422 „Taj PZ je već zakazan za …“ |
| POST | `/raspored/prenesi` | raspored.uredjivanje | ručni prenos današnjeg nezapočetog DP-a (inače pozadinski posao posle kraja radnog dana) |
| PUT | `/pregledi/{id}/dodela` `{ inzenjerId?, datum? }` | raspored.uredjivanje | „Promeni u rasporedu“ |
| POST | `/pregledi/{id}/ucesnici` `{ korisnikId, sekcije[] }` | raspored.uredjivanje | drugi inženjer na PZ-u (prazne sekcije = ukloni) |
| POST | `/pregledi/{id}/otkazi` `{ razlog, pomeri }` | raspored.uredjivanje | otkazivanje planiranog; DP + `pomeri` = pomeri raspored; odgovor može imati `slobodanDan` (ponuditi povlačenje); 422 nije planiran |
| PUT | `/pregledi/{id}/tip` `{ tip, uskladi }` | raspored.uredjivanje | „Promeni DP“; nov broj; `uskladi` preokreće naredne planirane DP-ove |
| GET · POST · DELETE | `/neradni-dani?godina` · `/neradni-dani` `{ od, do, naziv, vrsta, pomeriDp? }` · `/neradni-dani/{id}` | RequireKorisnik · neradni_dani | `pomeriDp`: nezapočeti DP-ovi sa tih dana i naredni idu dalje u nizu (zamerka 52); `DELETE` vraća `{ slobodanDan }` za „Povuci raspored“ (zamerka 56) |
| GET | `/odsustva?korisnikId&status` | RequireKorisnik | bez prava odobravanja samo svoja |
| POST | `/odsustva` `{ korisnikId, od, do, vrsta, napomena, prebacivanja[] }` | odsustva.odobravanje | unos (odmah odobreno) sa prebacivanjem; 422 preklapanje, 422 započet pregled |
| POST | `/odsustva/zahtev` `{ od, do, vrsta, napomena }` | pregledi.popunjavanje | zahtev za sebe |
| POST | `/odsustva/{id}/odluka` `{ odobreno, razlog?, prebacivanja[] }` | odsustva.odobravanje | |
| GET | `/odsustva/{korisnikId}/pregledi?od&do` | odsustva.odobravanje | planirani pregledi u periodu + predlog zamene |
| PUT | `/odsustva/{id}` `{ od, do }` | odsustva.odobravanje | izmena perioda; 422 preklapanje, 422 odbijeno/otkazano (R3.25) |
| POST | `/odsustva/{id}/otkazi` | RequireKorisnik | status `otkazano`; bez prava odobravanja samo svoj zahtev na čekanju (403); odgovor (i kod `PUT`) nosi `zaVracanje` — pregledi prebačeni zbog odsustva na dane koji više nisu u njemu |
| POST | `/odsustva/{id}/vrati-preglede` `{ pregledIds }` | odsustva.odobravanje | vraća inženjeru preglede (zamerka 55) |
| GET · POST | `/zahtevi-prebacivanja?status` · `/zahtevi-prebacivanja` `{ pregledId, razlog, predlozenaZamenaId? }` | RequireKorisnik · pregledi.popunjavanje | svoj planiran pregled; 403 tuđi, 422 započet |
| POST | `/zahtevi-prebacivanja/raniji-pocetak` `{ pregledId, razlog }` | pregledi.popunjavanje | samo svoj planiran PZ posle današnjeg dana; 422 za DP (R3.24) |
| POST | `/zahtevi-prebacivanja/{id}/otkazi` | pregledi.popunjavanje (podnosilac) | dok čeka odluku |
| POST | `/zahtevi-prebacivanja/{id}/odluka` `{ odobreno, zamenaId }` | raspored.uredjivanje | kod PZ-a sa dva inženjera prebacuje se samo deo podnosioca; `raniji_pocetak` bez zamene — odobren PZ prelazi na danas |

### Popunjavanje (faza 04, pravo `pregledi.popunjavanje`)
| Metod | Ruta | Svrha |
|---|---|---|
| GET | `/moj-paket` | `{ pregledi[], generisanoAt }` — dodeljeni za danas + 7 dana i započeti; `ETag`, `If-None-Match` → 304 |
| POST | `/pregledi/{id}/zapocni` | status `u_radu`, vreme početka, važeća verzija šablona; samo dodeljeni ili učesnik (403); najranije na planirani dan — ranije 422 „Pregled može da se započne od …“ (važi i za `zapocni` u sinhronizaciji, R3.24) |
| GET | `/pregledi/{id}/popunjavanje` | sekcije (`moja`, `radi`) → uređaji (`vanFunkcije`, `prosliPutVanFunkcije`) → stavke (polja, odgovor, ID-jevi slika) |
| POST | `/sinhronizacija` `{ uredjajKlijenta, klijentSada, operacije[{opId, vrsta, pregledId, klijentAt, podaci}] }` | `{ prihvacene[], odbijene[{opId, razlog}], serverSada }`; vrste `akcija` (`stavkaId`, `vrsta`, `hitnost`, `opis`), `akcija_odgovor` (`akcijaId`, `uradjeno`), `zapocni` (`verzijaId`; vreme početka = `klijentAt`, vezuje `uredjajKlijenta`), `odgovor` (`stavkaId`, `vrednost`, `napomena`, `polja`), `van_funkcije` (`uredjajId`, `vanFunkcije`), `sve_ispravno` (`sekcija`), `obrisi_sliku` (`fotografijaId`, `stavkaId`; već obrisana = prihvaćeno), `predlog_izmene` (`vrsta`, `opis`, `razlog`; predlog izmene šablona koji pamti pregled, zamerka 34), `predaja`; ponovljen `opId` = već primenjeno |
| POST | `/fotografije` multipart (`fajl`, `klijentId`, `pregledId`, `stavkaId`, `izvor`) | ≤ 3 MB (400 „Slika je prevelika.“), JPEG/PNG/WebP, najviše 5 po stavci (422) |
| GET | `/fotografije/{id}` | slika (RequireKorisnik — svi vide sve preglede) |

Odbijene operacije: „Pregled još nije potpun: nedostaje N odgovora i M obaveznih napomena.“, „PZ se predaje kad su sve sekcije gotove. Nedostaje: sekcija 70 (Ime).“, „Sekciju 20 radi Ime.“, „Predat pregled se menja samo kroz doradu.“, „Pregled je poništen.“

### Arhiva, overa, dorada, izveštaji (faza 06)
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/pregledi?od&do&tip&status&inzenjerId&pretraga&stranica&velicina` | RequireKorisnik | `StranicaDto<PregledRedDto>` (sa brojem neispravnih) |
| GET | `/pregledi/{id}` | RequireKorisnik | detalj: zaglavlje, aktivno vreme i delovi, upozorenja, sekcije sa odgovorima/poljima/slikama, istorija, sporni unosi, zahtevi za doradu, `ucesnici` (drugi inženjer PZ-a sa sekcijama), `akcije` (otvorene na pregledu, sa `stavkaId`), `odgovoriNaAkcije`, `predlozi` |
| GET | `/pregledi/{id}/izvoz?format=pdf\|docx` | RequireKorisnik | izveštaj sa rezimeom problematičnih stavki |
| GET | `/izvoz/pregledi.xlsx?…filteri` | RequireKorisnik | arhiva u Excel (filtrirano) |
| POST | `/pregledi/{id}/overi` | overa | `ceka_overu` → `overen` |
| POST | `/pregledi/{id}/vrati-na-doradu` `{ inzenjerId, razlog }` | overa | pre i posle overe; 422 „Pregled je overio Direktor; samo Direktor može da poništi tu overu.“ |
| POST | `/pregledi/{id}/zahtev-za-doradu` `{ razlog }` | pregledi.popunjavanje (inženjer koji je radio) | |
| POST | `/pregledi/{id}/zahtev-za-doradu/odluka` `{ odobreno }` | overa | posle overe: onaj ko je overio ili Direktor |
| POST | `/pregledi/{id}/zahtev-za-doradu/otkazi` | pregledi.popunjavanje (podnosilac) | dok čeka odluku; posle toga može nov |
| POST | `/pregledi/{id}/ponisti` `{ razlog }` | overa | u radu / čeka overu / overen / vraćen |
| POST | `/pregledi/{id}/otkljucaj` | overa | briše vezu sa uređajem (u radu / dorada); detalj ima `vezanZaUredjaj` (R10.10) |
| POST | `/pregledi/{id}/sporan-unos/{unosId}/odluka` `{ primeni }` | overa | sporan odgovor se upisuje u tekuću doradu ili odbacuje |

### Akcije i neispravnosti (faza 07)
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/akcije?status&hitnost&sekcijaId&uredjajId&sadaIspravna&saZahtevom` | RequireKorisnik | `AkcijaDto[]` (rok, istekao rok, zaduženi, kvar na stavci, kako je zatvorena, zahtev za zatvaranje na čekanju) |
| GET | `/akcije/{id}` | RequireKorisnik | akcija sa istorijom |
| POST | `/akcije/{id}/zatvori` `{ uradjeno, datum, izvrsilac }` · `/otkazi` `{ razlog }` | akcije.zatvaranje | 422 ako nije otvorena ili je datum u budućnosti; zahtev na čekanju se zatvara |
| POST | `/akcije/{id}/zahtev-zatvaranja` (multipart: `klijentId`, `uradjeno`, `datum`, `izvrsilac`, `fajl?`) | pregledi.popunjavanje | idempotentno po `klijentId`; 422 kad zahtev već čeka; obaveštenje onima sa pravom `akcije.zatvaranje` |
| POST | `/akcije/zahtevi-zatvaranja/{id}/odluka` `{ odobreno, razlog? }` · `/otkazi` | akcije.zatvaranje · pregledi.popunjavanje (podnosilac) | odbijanje traži razlog |
| GET | `/akcije/{id}/slika?zahtev` | RequireKorisnik | slika uz zatvaranje ili uz zahtev |
| PUT | `/akcije/{id}/zaduzen` `{ zaduzen }` | akcije.zatvaranje | osoba ili služba, ide u istoriju |
| GET | `/kvarovi?otvoreno&sekcijaId&uredjajId&pretraga` | RequireKorisnik | `KvarDto[]` (trajanje, prijave, otvorene akcije) |
| GET | `/kvarovi/{id}` | RequireKorisnik | kvar sa svim datumima prijave i akcijama na stavci |
| GET | `/izvoz/akcije.xlsx` · `/izvoz/kvarovi.xlsx` | RequireKorisnik | Excel (filtrirano) |

Popunjavanje (`GET /pregledi/{id}/popunjavanje`, paket) uz svaki uređaj vraća `otvoreneAkcije` za pop-up.

### Uređaj, analitika, Dashboard, pretraga (faza 08)
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/uredjaji/{id}/ekran?dana=90` | RequireKorisnik | `UredjajEkranDto`: status, poslednji pregled, šabloni, istorija po danu (`dana` 30–365), analitika, pregledi sa stanjem uređaja, dosije, akcije, kvarovi, `qrToken` |
| GET | `/uredjaji/qr/{token}` | RequireKorisnik | `{ id }` uređaja sa QR nalepnice; 404 ako ne postoji |
| GET | `/uredjaji/{id}/pregledi.xlsx` | RequireKorisnik | svi pregledi uređaja u Excel |
| GET | `/analitika/opsta?od&do` | analitika | kvarovi po sekciji, uređaju, aktivnosti i mesecu, najčešći, prosečno trajanje, trajanje pregleda po tipu i inženjeru, „nije u funkciji“ (podrazumevano 90 dana) |
| GET | `/analitika/ispunjenje?od&do` | analitika | ispunjenje plana: ukupno, po mesecu, po inženjeru (podrazumevano tekuća godina) |
| GET | `/dashboard` | RequireKorisnik | `DashboardDto`; `moje` samo uz pravo popunjavanja, `cekaOdluku` samo Supervizoru i Direktoru |
| GET | `/pretraga?q` | RequireKorisnik | `PretragaRezultatDto[]` (`vrsta`: uredjaj, pregled, akcija, kvar); prazno ispod 2 znaka |

### Izmene šablona (faza 10)
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/sabloni/{tip}/verzije` | RequireKorisnik | `VerzijaKratkoDto[]` (broj, status `objavljena`/`zakazana`/`zamenjena`/`nacrt`, važi od, ko je objavio) |
| GET | `/sabloni/{tip}/nacrt` | sabloni.nacrt | `NacrtDto` (verzija, osnovna verzija, razlike, odobreni predlozi, zakazana) ili 204 |
| POST / DELETE | `/sabloni/{tip}/nacrt` | sabloni.nacrt | otvori (kopija poslednje verzije; postojeći se vraća) / odbaci |
| POST | `/sabloni/{tip}/nacrt/stavke` `{ sekcijaId, uredjajId, aktivnostId, poljaIds }` | sabloni.nacrt | dodaj; 422 van upotrebe ili duplikat |
| PUT / DELETE | `/sabloni/{tip}/nacrt/stavke/{id}` | sabloni.nacrt | PUT `{ aktivnostId, poljaIds }` izmena (ključ ostaje) / izbaci |
| POST | `/sabloni/{tip}/nacrt/stavke/{id}/pomeri` `{ gore }` | sabloni.nacrt | jedno mesto unutar sekcije |
| POST | `/sabloni/{tip}/nacrt/objavi` `{ vaziOd }` | sabloni.objava | danas → važi odmah; kasnije → `zakazana`; 422 prošlost, bez izmena, već zakazana |
| GET | `/sabloni/istorijat?tip&vrsta&korisnikId&od&do` | RequireKorisnik | `SablonDogadjajDto[]` (do 1000; uz ime nosi i `korisnikId` za filter) |
| GET / POST | `/predlozi-izmena?status&tip&moji` | RequireKorisnik | lista (`moji` = poslao ili se složio) / predlog `{ tip, vrsta, stavkaId, opis, razlog, pregledId? }` (razlog obavezan) |
| POST | `/predlozi-izmena/{id}/odluka` `{ odobren, odgovor, duplikatOd? }` | sabloni.nacrt | `duplikatOd` = zatvori kao duplikat tog predloga; 422 ako je već odlučeno |
| POST | `/predlozi-izmena/{id}/slazem-se` | RequireKorisnik | „Slažem se sa predlogom“; 422 sopstveni ili odlučen predlog |
| GET / PUT | `/aktivnosti/{id}/uputstvo` | RequireKorisnik / sabloni.nacrt | `UputstvoDto` (tekst, slike, ko i kada) / `{ tekst }` |
| POST | `/aktivnosti/{id}/uputstvo/slike` (multipart `fajl`) | sabloni.nacrt | najviše 3, JPG/PNG/WebP ≤ 3 MB |
| GET / DELETE | `/aktivnosti/{id}/uputstvo/slike/{fajlId}` | RequireKorisnik / sabloni.nacrt | slika / ukloni |

Paket za popunjavanje uz stavku nosi `aktivnostId`, `uputstvo` (tekst) i `uputstvoSlika` (broj slika). `AktivnostDto` ima `imaUputstvo`.

### Obaveštenja (faza 09)
| Metod | Ruta | Politika | Svrha |
|---|---|---|---|
| GET | `/obavestenja` | RequireKorisnik | `ZvonoDto`: `neprocitanih`, `cekaOdluku` (Supervizor/Direktor, po pravima), poslednjih 50 |
| POST | `/obavestenja/procitano` `{ ids? }` | RequireKorisnik | označi pročitano (bez `ids` — sva); vraća zvono |
| POST | `/obavestenja/probno` | RequireKorisnik | probno obaveštenje sebi (204) |
| GET | `/push/kljuc` | RequireKorisnik | `{ kljuc }` — javni VAPID ključ |
| POST | `/push/pretplata` `{ endpoint, p256dh, auth }` | RequireKorisnik | prijava uređaja (isti endpoint prelazi na trenutnog korisnika); 204 |
| DELETE | `/push/pretplata` `{ endpoint }` | RequireKorisnik | odjava uređaja; 204 |
| GET / PUT | `/podesavanja/primaoci` | podesavanja | `PrimaociDto` (vrste, primaoci, aktivni korisnici); PUT `{ vrsta: [korisnikId] }` čuva navedene vrste; 400 nepoznata vrsta, 422 nepoznat korisnik |

Push poruka: `{ title, body, url, tag }`. Pozadinski posao (`ObavestenjaPosao`): isporuka svakih 15 s, zakazane provere jednom u minutu.

### `/_diag` (samo Development)
Rute kojima integracioni testovi dokazuju politike: `/_diag/korisnik` (RequireKorisnik), `/_diag/admin-only` (RequireAdmin).

## Oblici DTO-a

```jsonc
// LoginResponse
{ "accessToken": "eyJ…", "expiresAt": "2026-10-12T09:45:00Z",
  "user": { "id": "…", "userName": "admin", "fullName": "Inicijalni Admin", "email": "admin@tpm-pregledi.elixirgroup.rs" }, "roles": ["Admin"], "prava": ["analitika", "sabloni.nacrt", "neradni_dani", "korisnici", "podesavanja"] }

// KorisnikDto
{ "id": "…", "korisnickoIme": "mpetrovic", "ime": "Marko Petrović", "email": null, "rola": "Inzenjer", "prava": ["pregledi.popunjavanje", "analitika"], "aktivan": true, "poslednjaPrijava": "2026-10-12T07:12:00Z" }
// KorisnikInput (POST)
{ "korisnickoIme": "mpetrovic", "ime": "Marko Petrović", "email": null, "lozinka": "Tajna2026!", "rola": "Inzenjer" }

// UredjajDto
{ "id": 7, "naziv": "Merna vaga Sautelma", "oznaka": "10-C-07C", "sekcijaId": 1, "sekcija": "10", "vrsta": "Merna vaga", "status": "aktivan", "sabloni": ["DP1", "DP2"] }

// StranicaDto<T>
{ "stavke": [ … ], "ukupno": 123, "stranica": 1, "velicina": 50 }

// AuditRedDto
{ "id": 1, "vreme": "…", "korisnik": "admin", "akcija": "UPDATE", "entitet": "Uredjaj", "entitetLabela": "Uređaj",
  "entitetId": "1", "ip": "10.20.30.40", "stareVrednosti": "{…}", "noveVrednosti": "{…}" }

// ServerStanjeDto — blueprint 10 §1
// VersionDto
{ "verzija": "0.1.0", "gitSha": "ab12cd3", "buildUtc": "2026-10-12T09:30:00Z", "okruzenje": "Production" }
```

## Model pristupa (sažetak)

Anonimno: health, version, login, refresh. Svaki ulogovani: me, logout, change-password. Svaka od četiri role: čita sekcije, registar, katalog i šablone. Ostalo po pravu (`docs/04`).
