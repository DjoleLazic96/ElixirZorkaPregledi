# TODO — TPM pregledi

> Radni dnevnik projekta. **Status je ovde**, ne u planovima. Oznake: `[ ]` otvoreno, `[~]` u toku, `[x]` gotovo.
> Odluke se zapisuju u kontekstu kao **ODLUKA** i prepisuju u `docs/odluke.md`.

## Otvoreno

### Pre početka razvoja
- [x] Vlasnik projekta (Miroslav Zec) odobrava spec `docs/superpowers/specs/2026-10-27-tpm-pregledi-design.md`
- [ ] Vlasnik blueprinta upoznat sa odstupanjima u `docs/odluke.md` — vlasnik projekta ih je odobrio 2026-11-04/05 (četiri role, mobilni prikaz, fotografije, PWA i rad bez interneta, duža sesija, biblioteke, push i zakazane provere, izgled po mockup-u)
- [ ] Port 55434 upisati u registar blueprinta (03 §1)
- [x] 2026-11-04 — GitHub remote: https://github.com/DjoleLazic96/ElixirZorkaPregledi (javan)

### Faza 01 — skelet
- [x] 2026-10-27 — kopiran skelet, `preimenuj.ps1` izvršen, zaostaci „skelet" očišćeni, git init (lokalno)
- [x] 2026-11-04 — dev klaster radi, `dotnet run` + `npm run dev`, prijava `admin`
- [ ] CI zelen na prvom PR-u (posle remote-a)

### Faza 02 — prva isporuka (ceo spec)
- [x] 2026-11-04 — plan `docs/superpowers/plans/`: `00-overview.md` (faze 01–11), detaljno `01-osnova.md` i `02-oprema-i-sabloni.md`
- [x] 2026-11-04 — faza 01: četiri role, prava štikliranjem, podešavanja, sekcije, brojači (`01-osnova.md`)
- [x] 2026-11-04 — faza 02: registar opreme, katalog, dodatna polja, šabloni sa verzijama, uvoz iz .xlsx (`02-oprema-i-sabloni.md`)
- [x] mobilni raspored (donji meni umesto bočnog) — faza 04
- [x] 2026-11-04 — faza 03: raspored, kalendar, odsustva, neradni dani, prebacivanja, prenos propuštenog DP-a (`03-raspored.md`); obaveštenja o pomeranju čekaju fazu 09
- [x] 2026-11-04 — faza 04: popunjavanje sa internetom, slike, aktivno vreme, mobilni raspored (`04-popunjavanje.md`)
- [x] 2026-11-04 — faza 05: rad bez interneta, PWA, Zapamti me, jedan telefon, sporan unos, sat telefona (`05-rad-bez-interneta.md`); odluka o spornom unosu je faza 06
- [x] 2026-11-04 — faza 06: arhiva, detalj, overa, dorada, zahtev za doradu, poništavanje, sporni unosi, PDF/Word/Excel (`06-overa-i-arhiva.md`); automatski PDF/rezime pri predaji i obaveštenja su faza 09
- [x] 2026-11-04 — faza 07: akcije, pop-up, kvarovi, Neispravnosti (`07-akcije-i-kvarovi.md`); obaveštenja (hitna akcija, istekao rok) su faza 09
- [x] 2026-11-05 — izgled po mockup-u (odluka u `docs/odluke.md`): tokeni, komponente, okvir, Početna, popunjavanje, kalendar, prijava; dugmad 40 px (44 na telefonu), razmaci 16 px; provera prikaza ponovljena (375/768/1366)
- [x] 2026-11-04 — faza 11: test prikaza (4 role × 4 širine × sa/bez interneta, `docs/deploy/test-prikaza.md`), povratak posle prijave, 44 px na telefonu, nginx CSP `blob:` i SW `no-cache`, `docs/05-pravila-pregleda.md`, check-lista telefona, lista za puštanje u rad (`11-zavrsna-provera.md`)
- [ ] Puštanje u rad po `deploy/README.md` §0 (IT #1–#9, uvoz šablona, nalozi, provera na telefonu, restore test) — radi čovek
- [x] 2026-11-04 — faza 10: nacrt, razlike, objava sa datumom i zakazana verzija, predlozi, istorijat, uputstva (`10-izmene-sablona.md`)
- [x] 2026-11-04 — faza 09: zvono, Web Push, mejl (Graph; log dok IT ne da nalog), lična obaveštenja, zakazane provere, primaoci (`09-obavestenja.md`); ručna provera push-a na Android Chrome-u ide u fazu 11
- [x] 2026-11-04 — faza 08: ekran uređaja (istorija, dosije, QR), analitika i ispunjenje plana, Početna sa „Moje aktivnosti“ i „Čeka moju odluku“, pretraga (`08-uredjaj-analitika-dashboard.md`); broj na zvoncu je faza 09
- [x] obaveštenja (push, mejl), pozadinski poslovi, pretraga, podešavanja — faze 08–09
- [x] korisnička uputstva po temama — `docs/user/` 01–10

### Faza 03 — druga faza: Pumpe, Trake, Plansko stajanje
- [~] Pumpe — opis i mockup gotovi (`../Druga faza/2026-10-28 Pumpe – druga faza.docx`), čekaju odgovore klijenta
- [~] Plansko stajanje — opis i mockup gotovi (`../Druga faza/2026-10-28 Plansko stajanje – druga faza.docx`), čekaju odgovore klijenta
- [~] Trake — opis i mockup gotovi (`../Druga faza/2026-10-28 Trake – druga faza.docx`), čekaju odgovore klijenta

### Zahtevi klijenta
- [ ] 2026-10-27 — Miroslav Zec / naručilac — dorada analitike posle prvih meseci rada

### Zahtevi ka IT-u
- [ ] domen `tpm-pregledi.elixirgroup.rs` + HTTPS, folder `/opt/apps/tpm-pregledi`, nalog u `docker` grupi, `proxy-net`, Passbolt → `docs/deploy/sysadmin-tasks.md`
- [ ] Microsoft Graph aplikacija (`Mail.Send`) za mejl obaveštenja
- [ ] prostor i bekap za fotografije (~5 GB godišnje)

### Odloženo
- [x] 2026-11-04 — šabloni prepisani sa najsvežijih obrazaca → `docs/sabloni/sabloni-za-uvoz.xlsx` (DP1, DP2, PZ1 doslovno + Sautelma; PZ2–PZ4 sa ispravkama korisnika); uvoz je faza 02

## Završeno

- [x] 2026-10-27 — projekat započet iz Elixir skeleta v1.1; spec napisan

## Produkcija (istorija deploy-a)

| Datum | Verzija | Image tag | Napomena |
|---|---|---|---|
| | | | |
